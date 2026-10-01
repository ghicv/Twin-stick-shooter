using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Waves (prototype, no level data yet). The Start Wave button removes the practice dummies (first time only)
// and spawns a few random enemies (melee or ranged) at random spawn points away from the player.
// Killing an enemy (or knocking it off the map) gives score. When all are gone the wave is over: the player gets
// health back, travels to a new map (MapManager + PlayerRespawn, optional), the player's cores may offer
// cards, and then the button shows up again for the next one.
public class WaveManager : MonoBehaviour
{
    [Header("Enemies")]
    [SerializeField] private Dummy meleeEnemyPrefab;
    [SerializeField] private Dummy rangedEnemyPrefab;

    [Tooltip("Each wave spawns a random number of enemies between Min and Max (both included).")]
    [SerializeField] private int minEnemies = 2;
    [SerializeField] private int maxEnemies = 3;

    [SerializeField] private int meleeScore = 100;
    [SerializeField] private int rangedScore = 150;

    [Header("Spawning")]
    [Tooltip("Where enemies appear when there is no Map Manager (with one, the current map's spawn points are used).")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Enemies don't spawn closer to the player than this, as long as other spawn points are free (units).")]
    [SerializeField] private float minDistanceFromPlayer = 4f;

    [Tooltip("Played where each enemy appears (e.g. DustPuff). Optional.")]
    [SerializeField] private ParticleSystem spawnEffect;

    [Header("Between Waves")]
    [Tooltip("Health given back to the player when a wave is cleared (100 = full health).")]
    [SerializeField] private float healOnWaveClear = 100f;

    [Tooltip("Every cleared wave takes the player to another map. Optional (none = the same map all run).")]
    [SerializeField] private MapManager mapManager;

    [Tooltip("Seconds the \"WAVE CLEAR!\" message stays before the player is taken to the next map (real time).")]
    [SerializeField] private float mapChangeDelay = 1f;

    [Header("UI")]
    [Tooltip("Starts the next wave. Hidden while a wave is on. Its click is hooked up in Awake.")]
    [SerializeField] private Button startWaveButton;

    [Tooltip("Shows \"WAVE 1 CLEAR!\" after a wave.")]
    [SerializeField] private Text messageText;

    public int Score { get; private set; }
    public int Wave { get; private set; }
    public int EnemiesLeft => enemies.Count;
    public bool WaveRunning { get; private set; }

    private readonly List<Dummy> enemies = new List<Dummy>();
    private readonly List<int> enemyScores = new List<int>();
    private bool dummiesRemoved;

    // The player spawns over the network after this object wakes up, so it is looked up when needed.
    private Transform player => PlayerNetwork.Local.transform;
    private PlayerHealth playerHealth => PlayerNetwork.Local.GetComponent<PlayerHealth>();
    private PlayerRespawn playerRespawn => PlayerNetwork.Local.GetComponent<PlayerRespawn>();
    private CoreBridge cores => PlayerNetwork.Local != null ? PlayerNetwork.Local.Cores : null; // card offers between waves

    private Transform[] CurrentSpawnPoints => mapManager != null ? mapManager.CurrentMap.SpawnPoints : spawnPoints;

    private void Awake()
    {
        startWaveButton.onClick.AddListener(StartWave);
        messageText.text = "";
    }

    private void Start()
    {
        // Card offers before the first wave (if any): the Start Wave button waits for them.
        if (cores == null || !cores.OfferCardsAtStart(ShowStartButton))
            ShowStartButton();
    }

    private void OnDisable()
    {
        startWaveButton.gameObject.SetActive(false); // not playing waves (menu, LAN)
    }

    public void StartWave()
    {
        if (WaveRunning)
            return;

        if (!dummiesRemoved)
        {
            foreach (DummySpawner spawner in FindObjectsByType<DummySpawner>(FindObjectsSortMode.None))
                spawner.Stop();
            dummiesRemoved = true;
        }

        Wave++;
        WaveRunning = true;
        startWaveButton.gameObject.SetActive(false);
        messageText.text = "";

        foreach (Transform point in PickSpawnPoints(Random.Range(minEnemies, maxEnemies + 1)))
        {
            bool melee = Random.value < 0.5f;
            enemies.Add(Instantiate(melee ? meleeEnemyPrefab : rangedEnemyPrefab, point.position, Quaternion.identity));
            enemyScores.Add(melee ? meleeScore : rangedScore);
            if (spawnEffect != null)
                Instantiate(spawnEffect, point.position, Quaternion.identity);
        }
    }

    private void Update()
    {
        if (!WaveRunning)
            return;

        // An enemy is gone once it has disabled itself (exploded, or fell off the map).
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] != null && enemies[i].gameObject.activeSelf)
                continue;

            Score += enemyScores[i];
            if (enemies[i] != null)
                Destroy(enemies[i].gameObject);
            enemies.RemoveAt(i);
            enemyScores.RemoveAt(i);
        }

        if (enemies.Count == 0)
        {
            WaveRunning = false;
            messageText.text = "WAVE " + Wave + " CLEAR!";
            if (playerHealth.IsDead)
                return; // the run is over (Game Over screen): no health, no cards, no next wave

            playerHealth.Heal(healOnWaveClear);

            if (mapManager != null && playerRespawn != null)
                StartCoroutine(ChangeMap());
            else
                OfferCardsThenButton();
        }
    }

    // The map is cleared: after a moment the player is taken to a new one (a fade to black and back).
    private System.Collections.IEnumerator ChangeMap()
    {
        yield return new WaitForSecondsRealtime(mapChangeDelay);
        if (playerHealth.IsDead)
            yield break; // died meanwhile (e.g. fell off): the run is over

        playerRespawn.TravelToNewMap(() =>
        {
            mapManager.ChangeToRandomMap();
            if (cores != null)
                cores.OnMapChanged();
        }, () =>
        {
            messageText.text = mapManager.CurrentMap.DisplayName;
            OfferCardsThenButton();
        });
    }

    // Every few waves the player picks a core first; the button comes back after the pick.
    private void OfferCardsThenButton()
    {
        if (cores == null || !cores.OfferCardsAfterWave(Wave, ShowStartButton))
            ShowStartButton();
    }

    private void ShowStartButton()
    {
        startWaveButton.gameObject.SetActive(true);
    }

    // Random spawn points, never the same one twice in a wave. Points near the player are only used
    // when there aren't enough far ones.
    private List<Transform> PickSpawnPoints(int count)
    {
        var far = new List<Transform>();
        var near = new List<Transform>();
        foreach (Transform point in CurrentSpawnPoints)
            (Vector2.Distance(point.position, player.position) >= minDistanceFromPlayer ? far : near).Add(point);

        Shuffle(far);
        Shuffle(near);
        far.AddRange(near);
        return far.GetRange(0, Mathf.Min(count, far.Count));
    }

    private static void Shuffle(List<Transform> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
