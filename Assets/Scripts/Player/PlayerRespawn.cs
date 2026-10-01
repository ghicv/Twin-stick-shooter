using System.Collections;
using UnityEngine;

// The player's two retro "teleport" sequences. Both share the same revive: pixels gather where the player is
// (with a sound), the player reappears where they meet, waits a moment (Revive Delay), is pulled to a spawn point,
// and then the picture, game speed and control come back.
//
// Death (falling off the map, or 0 health). The host decides (the owner only reports a fall), then every machine
// plays it:
// 1. The player explodes where it fell out of view.
//    Single player with Game Over On Death: the Game Over screen shows up right away (unless an Extra Life core
//    saves the run). On LAN the player gets a card offer right away.
// 2. The game keeps running for a moment (Freeze Delay); then on the dead player's own screen time freezes
//    (single player only) and the screen curves (CRT / fish-eye).
// 3. The revive brings the player back.
//
// Map change (single player, WaveManager calls TravelToNewMap after a cleared wave):
// 1. Time freezes and the CRT look comes on; the player bursts into pixels.
// 2. The map is swapped behind the CRT picture.
// 3. The revive brings the player back on the new map (at one of its spawn points).
//
// Meanwhile the player's physics, movement and gun are off. All timings are real seconds.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class PlayerRespawn : MonoBehaviour
{
    // What happens after a death (sent by the host to every machine).
    public enum DeathOutcome { Revive = 0, GameOver = 1 }

    [Tooltip("Falling below this height (world Y) kills the player.")]
    [SerializeField] private float fallLimitY = -8f;

    [Tooltip("Single player: dying ends the run and the Game Over screen (GameOverScreen in the scene) shows up " +
             "right away. Off, or no Game Over screen in the scene: the player respawns.")]
    [SerializeField] private bool gameOverOnDeath = true;

    [Tooltip("Used when there is no MapManager: the player comes back at one of these. Empty = where the player started.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Death")]
    [Tooltip("Pixel explosion spawned where the player died (and where it bursts on a map change). " +
             "It is kept inside the screen so it can be seen.")]
    [SerializeField] private ParticleSystem explosionEffect;

    [Tooltip("How far inside the screen edge the explosion (and the reappearing player) is placed (units).")]
    [SerializeField] private float screenMargin = 1.4f;

    [SerializeField] private float explosionShakeStrength = 0.4f;
    [SerializeField] private float explosionShakeDuration = 0.35f;

    [Tooltip("Seconds after the explosion before time freezes. The game keeps running at normal speed until then.")]
    [SerializeField] private float freezeDelay = 0.5f;

    [Header("Time Freeze")]
    [Tooltip("Game speed while time is frozen (1 = normal, single player only). Needs a HitStop on the main camera.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float slowMotionSpeed = 0.1f;

    [Tooltip("Seconds for the game to slow down and the CRT / fish-eye look to switch on.")]
    [SerializeField] private float enterTime = 0.2f;

    [Tooltip("Seconds the frozen moment lasts (explosion hanging in the air) before the pixels start to gather.")]
    [SerializeField] private float freezeTime = 0.3f;

    [Tooltip("CRT / fish-eye screen effect used while time is frozen. Empty = the one on the main camera.")]
    [SerializeField] private CRTScreen crtScreen;

    [Header("Revive")]
    [Tooltip("Pixels that gather where the player exploded; the player reappears when they meet, i.e. after the " +
             "particles' Start Lifetime. It should use Unscaled Time so it plays at full speed during the slow motion. Optional.")]
    [SerializeField] private ParticleSystem reviveEffect;

    [Tooltip("Seconds the player stays where it reappeared before it is pulled to the spawn point.")]
    [SerializeField] private float reviveDelay = 0.5f;

    [Tooltip("Seconds the player takes to travel from where it reappeared to the spawn point.")]
    [SerializeField] private float pullTime = 0.6f;

    [Tooltip("Seconds for the picture and the game speed to get back to normal after arriving. Then control is back.")]
    [SerializeField] private float recoverTime = 0.35f;

    [Tooltip("Spawned at the player when it arrives at the spawn point (e.g. DustPuff). Optional.")]
    [SerializeField] private ParticleSystem arriveEffect;

    [Tooltip("Death effects are removed after this many seconds.")]
    [SerializeField] private float effectCleanupTime = 5f;

    [Header("Sound")]
    [Tooltip("Played when the player explodes (death only).")]
    [SerializeField] private AudioClip explosionSound;

    [Tooltip("Played when time starts to freeze.")]
    [SerializeField] private AudioClip timeFreezeSound;

    [Tooltip("Played while the pixels gather. Best ending on a blip right when the player reappears (as long as the gather effect).")]
    [SerializeField] private AudioClip gatherSound;

    [Tooltip("Played while the player is pulled to the spawn point (best about as long as Pull Time).")]
    [SerializeField] private AudioClip pullSound;

    [Tooltip("Played when the player arrives at the spawn point.")]
    [SerializeField] private AudioClip respawnSound;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.9f;

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
    private HitStop hitStop;
    private GameOverScreen gameOverScreen;
    private MapManager maps;
    private Vector2 startPosition;
    private Transform lastSpawnPoint;
    private float gameSpeed = 1f;
    private bool traveling;
    private bool fallReported;

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
        cameraShake = cam.GetComponent<CameraShake>(); // null → no shake
        hitStop = cam.GetComponent<HitStop>();         // null → no slow motion
        if (crtScreen == null)
            crtScreen = cam.GetComponent<CRTScreen>(); // null → no CRT look
        gameOverScreen = FindAnyObjectByType<GameOverScreen>(); // null → respawn instead
        maps = FindAnyObjectByType<MapManager>();               // null → Spawn Points above
    }

    // The machine that moves this player watches for falls off the map.
    private void FixedUpdate()
    {
        if (net.IsOwner && !fallReported && !IsDead && !traveling && rb.position.y < fallLimitY)
        {
            fallReported = true;
            Die(true);
        }
    }

    public void Die() => Die(false);

    // On the host this kills the player; the owner only reports a fall to the host.
    public void Die(bool fellOff)
    {
        if (net.IsServer)
            DieOnHost(fellOff);
        else if (net.IsOwner)
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
        if (!GameMode.IsLan && gameOverOnDeath && gameOverScreen != null && !(cores != null && cores.TryUseExtraLife()))
            outcome = DeathOutcome.GameOver;

        net.PlayDeath(rb.position, (int)outcome, PickSpawnPoint());
    }

    // Every machine: plays the death (the host sent where, what next, and where the player comes back).
    public void PlayDeath(Vector2 point, int outcome, Vector2 spawn)
    {
        StartCoroutine(DieRoutine(point, (DeathOutcome)outcome, spawn));
    }

    // Single player: takes the player to another map: freeze, burst into pixels, changeMap() swaps the map behind
    // the CRT picture, then the revive brings the player back there. onArrived runs once control is back.
    public void TravelToNewMap(System.Action changeMap, System.Action onArrived)
    {
        if (IsDead || traveling)
            return;

        StartCoroutine(TravelRoutine(changeMap, onArrived));
    }

    private IEnumerator DieRoutine(Vector2 point, DeathOutcome outcome, Vector2 spawn)
    {
        // 1. The player explodes; the game keeps running for a moment.
        if (net.IsOwner)
            TakeControlAway();
        Vector3 deathPoint = CameraView.ClampInside(cam, point, screenMargin);
        if (net.IsOwner)
            transform.position = deathPoint; // physics is off, so moving the transform is fine
        SetVisible(false);
        SpawnEffect(explosionEffect, deathPoint, Quaternion.Euler(0f, 0f, 90f)); // burst points up, into the arena
        if (cameraShake != null)
            cameraShake.Shake(explosionShakeStrength, explosionShakeDuration);
        PlaySound(explosionSound);

        // Single player: the run is over. The Game Over screen shows up right away (no screen effect).
        if (outcome == DeathOutcome.GameOver)
        {
            if (net.IsOwner)
                gameOverScreen.Show();
            yield break;
        }

        // LAN: a dead player picks a core while it comes back.
        if (net.IsOwner && GameMode.IsLan && cores != null)
            cores.OfferCardsNow();

        yield return new WaitForSecondsRealtime(freezeDelay);

        // 2. Time freezes (single player) and the dead player's screen curves.
        yield return FreezeTime(net.IsOwner);

        // 3. The player comes back.
        yield return Revive(spawn, true);
    }

    private IEnumerator TravelRoutine(System.Action changeMap, System.Action onArrived)
    {
        // 1. Time freezes; the player bursts into pixels.
        traveling = true;
        TakeControlAway();
        Vector3 point = CameraView.ClampInside(cam, rb.position, screenMargin);
        transform.position = point;
        SetVisible(false);
        SpawnEffect(explosionEffect, point, Quaternion.Euler(0f, 0f, 90f));
        yield return FreezeTime(true);

        // 2. New map, behind the CRT picture.
        changeMap?.Invoke();

        // 3. The player comes back on it.
        yield return Revive(PickSpawnPoint(), false);
        traveling = false;
        onArrived?.Invoke();
    }

    // No physics, no input, no shooting.
    private void TakeControlAway()
    {
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;
        movement.enabled = false;
        weapon.enabled = false;
    }

    // The game slows almost to a stop (single player) and, on this player's own screen, curves (CRT / fish-eye);
    // then the frozen moment lasts a bit.
    private IEnumerator FreezeTime(bool ownScreen)
    {
        if (ownScreen)
            PlaySound(timeFreezeSound);
        float speedFrom = gameSpeed;
        float crtFrom = crtScreen != null ? crtScreen.Intensity : 0f;
        for (float t = 0f; t < enterTime; t += Time.unscaledDeltaTime)
        {
            SetSpeed(Mathf.Lerp(speedFrom, slowMotionSpeed, t / enterTime));
            if (ownScreen)
                SetCrt(Mathf.Lerp(crtFrom, 1f, t / enterTime));
            yield return null;
        }
        SetSpeed(slowMotionSpeed);
        if (ownScreen)
            SetCrt(1f);
        yield return new WaitForSecondsRealtime(freezeTime);
    }

    // Pixels gather where the player is; the player reappears, is pulled to the spawn point (the owner moves it,
    // the other machines follow through the network), and everything goes back to normal.
    // afterDeath = tell the host the player is back (full health, alive again).
    private IEnumerator Revive(Vector2 spawn, bool afterDeath)
    {
        bool mine = net.IsOwner;
        if (reviveEffect != null)
        {
            SpawnEffect(reviveEffect, transform.position, Quaternion.identity);
            PlaySound(gatherSound);
            yield return new WaitForSecondsRealtime(reviveEffect.main.startLifetime.constantMax);
        }
        SetVisible(true);
        yield return new WaitForSecondsRealtime(reviveDelay);

        // Pulled to the spawn point (eases in and out).
        PlaySound(pullSound);
        Vector3 from = transform.position;
        Vector3 to = spawn;
        for (float t = 0f; t < pullTime; t += Time.unscaledDeltaTime)
        {
            if (mine)
                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / pullTime));
            yield return null;
        }
        if (mine)
        {
            transform.position = to;
            rb.position = to;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = true;
            fallReported = false;
        }
        SpawnEffect(arriveEffect, to, Quaternion.identity);
        PlaySound(respawnSound);
        if (afterDeath && mine)
            net.ReportRevived();

        // Picture and game speed ease back to normal, then control is back.
        for (float t = 0f; t < recoverTime; t += Time.unscaledDeltaTime)
        {
            SetSpeed(Mathf.Lerp(slowMotionSpeed, 1f, t / recoverTime));
            if (mine)
                SetCrt(1f - t / recoverTime);
            yield return null;
        }
        SetSpeed(1f);
        if (mine)
        {
            SetCrt(0f);
            movement.enabled = true;
            weapon.enabled = true;
        }
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

    // Slow motion is for single player only: on LAN the game speed is shared by everybody on the host.
    private void SetSpeed(float speed)
    {
        if (GameMode.IsLan)
            return;
        gameSpeed = speed;
        if (hitStop != null)
            hitStop.SetSlowMotion(speed);
    }

    private void SetCrt(float intensity)
    {
        if (crtScreen != null)
            crtScreen.Intensity = intensity;
    }

    private void SetVisible(bool visible)
    {
        foreach (SpriteRenderer sprite in sprites)
            sprite.enabled = visible;
    }

    private void SpawnEffect(ParticleSystem prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return;

        ParticleSystem effect = Instantiate(prefab, position, rotation);
        Destroy(effect.gameObject, effectCleanupTime);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null)
            return;

        audioSource.pitch = 1f; // PlayerWeapon changes the pitch for every shot
        audioSource.PlayOneShot(clip, soundVolume);
    }
}
