using System.Collections;
using UnityEngine;

// Death and coming back, kept short and quiet.
// Coming back ("appear"): a few pixels gather at the spawn point (with a soft sound), then the player blinks in
// there and has control again.
//
// Death (falling off the map, or 0 health). The host decides (the owner only reports a fall), then every machine
// plays it: the player explodes where it fell out of view, then
// - single player with Game Over On Death: the Game Over screen shows up right away (unless an Extra Life core
//   saves the run);
// - LAN round: the player is out until the next round and picks a card meanwhile;
// - otherwise it appears at a spawn point after Revive Delay.
//
// Map change (single player: WaveManager calls TravelToNewMap after a cleared wave; LAN: MatchManager calls
// BeginRoundTravel / ArriveForRound on every player at the start of a round): the screen fades to black, the map
// is swapped, and the player appears at a spawn point of the new map while the screen fades back in.
//
// Meanwhile the player's physics, movement and gun are off. All timings are real seconds.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class PlayerRespawn : MonoBehaviour
{
    // What happens after a death (sent by the host to every machine).
    // Revive: back after a moment. GameOver: single player run lost. Wait: LAN round, out until the next round
    // (picks a card meanwhile). Out: LAN, died after the round was already decided: out until the next round, no card.
    public enum DeathOutcome { Revive = 0, GameOver = 1, Wait = 2, Out = 3 }

    [Tooltip("Falling below this height (world Y) kills the player, when there is no MapManager (else the map's own).")]
    [SerializeField] private float fallLimitY = -8f;

    [Tooltip("While below Fall Limit Y and not dead yet, the fall is reported to the host again this often (seconds).")]
    [SerializeField] private float fallReportInterval = 0.5f;

    [Tooltip("Single player: dying ends the run and the Game Over screen (GameOverScreen in the scene) shows up " +
             "right away. Off, or no Game Over screen in the scene: the player respawns.")]
    [SerializeField] private bool gameOverOnDeath = true;

    [Tooltip("Used when there is no MapManager: the player comes back at one of these. Empty = where the player started.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Death")]
    [Tooltip("Pixel explosion spawned where the player died. It is kept inside the screen so it can be seen.")]
    [SerializeField] private ParticleSystem explosionEffect;

    [Tooltip("How far inside the screen edge the explosion is placed (units).")]
    [SerializeField] private float screenMargin = 1.4f;

    [SerializeField] private float explosionShakeStrength = 0.4f;
    [SerializeField] private float explosionShakeDuration = 0.35f;

    [Tooltip("Seconds between the explosion and the player appearing again (when it comes back).")]
    [SerializeField] private float reviveDelay = 1f;

    [Header("Appear")]
    [Tooltip("Pixels gathering at the spawn point. It should use Unscaled Time. Optional.")]
    [SerializeField] private ParticleSystem reviveEffect;

    [Tooltip("Size of that effect (1 = as made).")]
    [SerializeField] private float reviveEffectScale = 0.45f;

    [Tooltip("Seconds the pixels gather before the player shows up.")]
    [SerializeField] private float appearTime = 0.35f;

    [Tooltip("The player blinks this many times while it shows up.")]
    [SerializeField] private int appearBlinks = 3;

    [Tooltip("Seconds each blink (shown / hidden) lasts.")]
    [SerializeField] private float appearBlinkInterval = 0.06f;

    [Tooltip("Effects are removed after this many seconds.")]
    [SerializeField] private float effectCleanupTime = 5f;

    [Header("Sound")]
    [Tooltip("Played when the player explodes.")]
    [SerializeField] private AudioClip explosionSound;

    [Range(0f, 1f)]
    [SerializeField] private float explosionVolume = 0.9f;

    [Tooltip("Played softly when the player appears.")]
    [SerializeField] private AudioClip respawnSound;

    [Range(0f, 1f)]
    [SerializeField] private float respawnVolume = 0.45f;

    private Rigidbody2D rb;
    private AudioSource audioSource;
    private PlayerMovement movement;
    private PlayerWeapon weapon;
    private PlayerHealth health;
    private PlayerNetwork net;
    private HitTarget target;
    private CoreBridge cores;
    private SpriteRenderer[] sprites;
    private Camera cam;
    private CameraShake cameraShake;
    private GameOverScreen gameOverScreen;
    private MapManager maps;
    private Vector2 startPosition;
    private Transform lastSpawnPoint;
    private bool traveling;
    private float fallReportTimer;

    private float FallLimit => maps != null ? maps.CurrentMap.FallLimitY : fallLimitY;

    // True from the moment of death until control is back (the host decides, every machine knows).
    public bool IsDead => net.Dead;

    // True during a map change, until control is back.
    public bool IsTraveling => traveling;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        movement = GetComponent<PlayerMovement>();
        weapon = GetComponent<PlayerWeapon>();
        health = GetComponent<PlayerHealth>();
        net = GetComponent<PlayerNetwork>();
        target = GetComponent<HitTarget>();
        cores = GetComponent<CoreBridge>();
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        startPosition = rb.position;

        cam = Camera.main;
        cameraShake = cam.GetComponent<CameraShake>();          // null → no shake
        gameOverScreen = FindAnyObjectByType<GameOverScreen>(); // null → respawn instead
        maps = FindAnyObjectByType<MapManager>();               // null → Spawn Points above
    }

    // The machine that moves this player watches for falls off the map. It keeps reporting (every Fall Report
    // Interval) until the host has handled it: the host ignores a report while it is busy with this player
    // (e.g. a travel), and a single report would then be lost.
    private void FixedUpdate()
    {
        fallReportTimer -= Time.fixedDeltaTime;
        if (net.IsMine && fallReportTimer <= 0f && rb.simulated && !IsDead && !traveling && rb.position.y < FallLimit)
        {
            fallReportTimer = fallReportInterval;
            Die(true);
        }
    }

    public void Die() => Die(false);

    // On the host this kills the player; the owner only reports a fall to the host.
    public void Die(bool fellOff)
    {
        if (net.IsHostSide)
            DieOnHost(fellOff);
        else if (net.IsMine)
            net.ReportFell();
    }

    // Host: decides what happens and tells every machine.
    private void DieOnHost(bool fellOff)
    {
        if (IsDead || traveling)
            return;
        net.Dead = true;

        // Kill credit: the cores of whoever hurt this player last (Vampire, Death Blast, Void Feast...).
        CoreBridge killer = health.LastAttacker;
        if (killer != null && killer != cores)
            killer.OnEnemyDied(target, rb.position, fellOff);

        DeathOutcome outcome = DeathOutcome.Revive;
        MatchManager match = MatchManager.Instance;
        bool extraLife = cores != null && cores.TryUseExtraLife();
        if (!GameMode.IsLan && gameOverOnDeath && gameOverScreen != null && !extraLife)
            outcome = DeathOutcome.GameOver;
        else if (GameMode.IsLan && match != null && match.RoundOn && !extraLife)
        {
            outcome = DeathOutcome.Wait; // out of this round (picks a card meanwhile)
            match.OnPlayerDiedInRound(net);
        }
        else if (GameMode.IsLan && match != null && match.InMatch)
        {
            outcome = DeathOutcome.Out; // the round is already decided (e.g. fell off afterwards): no card
        }

        net.PlayDeath(rb.position, (int)outcome, PickSpawnPoint());
    }

    // Every machine: plays the death (the host sent where, what next, and where the player comes back).
    public void PlayDeath(Vector2 point, int outcome, Vector2 spawn)
    {
        StartCoroutine(DieRoutine(point, (DeathOutcome)outcome, spawn));
    }

    private IEnumerator DieRoutine(Vector2 point, DeathOutcome outcome, Vector2 spawn)
    {
        // The player explodes.
        if (net.IsMine)
            TakeControlAway();
        Vector3 deathPoint = CameraView.ClampInside(cam, point, screenMargin);
        if (net.IsMine)
            transform.position = deathPoint; // physics is off, so moving the transform is fine
        SetVisible(false);
        SpawnEffect(explosionEffect, deathPoint, Quaternion.Euler(0f, 0f, 90f), 1f); // burst points up, into the arena
        if (cameraShake != null)
            cameraShake.Shake(explosionShakeStrength, explosionShakeDuration);
        PlaySound(explosionSound, explosionVolume);

        if (outcome == DeathOutcome.GameOver)
        {
            if (net.IsMine)
                gameOverScreen.Show();
            yield break;
        }
        if (outcome == DeathOutcome.Wait)
        {
            // Out until the next round; the player picks a core while watching (with a time limit).
            MatchManager match = MatchManager.Instance;
            if (net.IsMine && !(cores != null && cores.OfferCardsNow(match != null ? match.CardTime : 10f)))
                net.ReportPickSkipped();
            yield break;
        }
        if (outcome == DeathOutcome.Out)
            yield break; // back with everybody at the next round

        yield return new WaitForSecondsRealtime(reviveDelay);
        yield return Appear(spawn, true);
    }

    // ---------- Map changes ----------

    // Single player: the screen fades to black, changeMap() swaps the map, and the player appears on it while the
    // screen fades back in. onArrived runs once control is back.
    public void TravelToNewMap(System.Action changeMap, System.Action onArrived)
    {
        if (IsDead || traveling)
            return;

        StartCoroutine(TravelRoutine(changeMap, onArrived));
    }

    private IEnumerator TravelRoutine(System.Action changeMap, System.Action onArrived)
    {
        traveling = true;
        TakeControlAway();
        ScreenFade fade = ScreenFade.Instance;
        if (fade != null)
            yield return fade.FadeOut();

        changeMap?.Invoke();
        Coroutine appear = StartCoroutine(Appear(PickSpawnPoint(), false));
        if (fade != null)
            yield return fade.FadeIn();
        yield return appear;

        traveling = false;
        onArrived?.Invoke();
    }

    // LAN, start of a round, every machine for every player (MatchManager fades the screen and swaps the map
    // in between): first the player stops where it is...
    public void BeginRoundTravel()
    {
        StopAllCoroutines(); // a death that is still playing
        traveling = true;
        if (net.IsMine)
            TakeControlAway();
    }

    // ...then, on the new map, it appears at its spawn point with full health (the dead come back too).
    public void ArriveForRound(Vector2 spawn)
    {
        StartCoroutine(ArriveRoutine(spawn));
    }

    private IEnumerator ArriveRoutine(Vector2 spawn)
    {
        yield return Appear(spawn, true);
        traveling = false;
    }

    // ---------- Appear ----------

    // The player (hidden) is put at the spawn point; pixels gather there, it blinks in and has control again
    // (the owner moves it, the other machines follow through the network).
    // afterDeath = tell the host the player is back (full health, alive again).
    private IEnumerator Appear(Vector2 spawn, bool afterDeath)
    {
        bool mine = net.IsMine;
        SetVisible(false);
        if (mine)
        {
            transform.position = spawn;
            rb.position = spawn;
            rb.linearVelocity = Vector2.zero;
        }

        SpawnEffect(reviveEffect, spawn, Quaternion.identity, reviveEffectScale);
        PlaySound(respawnSound, respawnVolume);
        yield return new WaitForSecondsRealtime(appearTime);

        for (int i = 0; i < appearBlinks * 2; i++)
        {
            SetVisible(i % 2 == 0);
            yield return new WaitForSecondsRealtime(appearBlinkInterval);
        }
        SetVisible(true);

        if (mine)
        {
            rb.simulated = true;
            movement.enabled = true;
            weapon.enabled = true;
            if (afterDeath)
                net.ReportRevived();
        }
    }

    // No physics, no input, no shooting.
    private void TakeControlAway()
    {
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;
        movement.enabled = false;
        weapon.enabled = false;
    }

    // A random spawn point (never the same one twice in a row). On LAN: the one farthest from the other players.
    private Vector3 PickSpawnPoint()
    {
        Transform[] points = maps != null ? maps.CurrentMap.SpawnPoints : spawnPoints;
        if (points == null || points.Length == 0)
            return startPosition;

        if (GameMode.IsLan)
        {
            Transform best = points[0];
            float bestDistance = -1f;
            foreach (Transform point in points)
            {
                float nearest = float.MaxValue;
                foreach (PlayerNetwork other in PlayerNetwork.All)
                    if (other != net && other.InPlay)
                        nearest = Mathf.Min(nearest, Vector2.Distance(other.transform.position, point.position));
                if (nearest > bestDistance)
                {
                    best = point;
                    bestDistance = nearest;
                }
            }
            return best.position;
        }

        int index = Random.Range(0, points.Length);
        if (points[index] == lastSpawnPoint && points.Length > 1)
            index = (index + 1 + Random.Range(0, points.Length - 1)) % points.Length;
        lastSpawnPoint = points[index];
        return points[index].position;
    }

    private void SetVisible(bool visible)
    {
        foreach (SpriteRenderer sprite in sprites)
            sprite.enabled = visible;
    }

    private void SpawnEffect(ParticleSystem prefab, Vector3 position, Quaternion rotation, float scale)
    {
        if (prefab == null)
            return;

        ParticleSystem effect = Instantiate(prefab, position, rotation);
        effect.transform.localScale = Vector3.one * scale;
        Destroy(effect.gameObject, effectCleanupTime);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        audioSource.pitch = 1f; // PlayerWeapon changes the pitch for every shot
        audioSource.PlayOneShot(clip, volume);
    }
}
