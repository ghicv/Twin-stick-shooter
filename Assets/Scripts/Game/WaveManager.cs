using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Waves (prototype, no level data yet). The Start Wave button removes the practice dummies (first time only)
// and spawns a few random enemies (melee or ranged) at random spawn points away from the player.
// Killing an enemy (or knocking it off the map) gives score. When all are gone the wave is over
// and the button shows up again for the next one.
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
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Enemies don't spawn closer to the player than this, as long as other spawn points are free (units).")]
    [SerializeField] private float minDistanceFromPlayer = 4f;

    [Tooltip("Played where each enemy appears (e.g. DustPuff). Optional.")]
    [SerializeField] private ParticleSystem spawnEffect;

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
    private Transform player;
    private bool dummiesRemoved;

    private void Awake()
    {
        player = GameObject.FindWithTag("Player").transform;
        startWaveButton.onClick.AddListener(StartWave);
        messageText.text = "";
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
            startWaveButton.gameObject.SetActive(true);
            messageText.text = "WAVE " + Wave + " CLEAR!";
        }
    }

    // Random spawn points, never the same one twice in a wave. Points near the player are only used
    // when there aren't enough far ones.
    private List<Transform> PickSpawnPoints(int count)
    {
        var far = new List<Transform>();
        var near = new List<Transform>();
        foreach (Transform point in spawnPoints)
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
