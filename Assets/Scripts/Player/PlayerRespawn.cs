using System.Collections;
using UnityEngine;

// The player's two retro "teleport" sequences. Both share the same revive: pixels gather where the player is
// (with a sound), the player reappears where they meet, waits a moment (Revive Delay), is pulled to a random
// spawn point, and then the picture, game speed and control come back.
//
// Death (falling off the map, or PlayerHealth calls Die at 0 health):
// 1. The player explodes where it fell out of view. The game keeps running for a moment (Freeze Delay).
// 2. Time freezes: the game slows almost to a stop and the screen curves into an old CRT / fish-eye look.
// 3. With Game Over On Death (roguelite: dying ends the run) the Game Over screen shows up and time stays frozen.
//    Only an extra life (a core) or Game Over On Death off brings the player back with the revive.
//
// Map change (WaveManager calls TravelToNewMap after a cleared wave):
// 1. Time freezes and the CRT look comes on; the player bursts into pixels.
// 2. The map is swapped behind the CRT picture.
// 3. The revive brings the player back on the new map (at one of its spawn points).
//
// Meanwhile the player's physics, movement and gun are off. All timings are real seconds.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("Falling below this height (world Y) kills the player.")]
    [SerializeField] private float fallLimitY = -8f;

    [Tooltip("On: dying ends the run and the Game Over screen (GameOverScreen in the scene) shows up once time has frozen. " +
             "Off, or no Game Over screen in the scene: the player respawns.")]
    [SerializeField] private bool gameOverOnDeath = true;

    [Tooltip("Used when there is no MapManager: the player comes back at one of these, picked at random " +
             "(never the same one twice in a row). Empty = where the player started.")]
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
    [Tooltip("Game speed while time is frozen (1 = normal). Needs a HitStop on the main camera.")]
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
    private SpriteRenderer[] sprites;
    private Camera cam;
    private CameraShake cameraShake;
    private HitStop hitStop;
    private GameOverScreen gameOverScreen;
    private CoreBridge cores;
    private MapManager maps;
    private Vector2 startPosition;
    private Transform lastSpawnPoint;
    private float gameSpeed = 1f;
    private bool isDead;
    private bool traveling;

    // True from the moment of death until control is back.
    public bool IsDead => isDead;

    // True during a map change, until control is back.
    public bool IsTraveling => traveling;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        movement = GetComponent<PlayerMovement>();
        weapon = GetComponent<PlayerWeapon>();
        health = GetComponent<PlayerHealth>(); // null → nothing to refill
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        startPosition = rb.position;

        cam = Camera.main;
        cameraShake = cam.GetComponent<CameraShake>(); // null → no shake
        hitStop = cam.GetComponent<HitStop>();         // null → no slow motion
        if (crtScreen == null)
            crtScreen = cam.GetComponent<CRTScreen>(); // null → no CRT look
        gameOverScreen = FindAnyObjectByType<GameOverScreen>(); // null → respawn instead
        cores = FindAnyObjectByType<CoreBridge>();              // null → no extra life
        maps = FindAnyObjectByType<MapManager>();               // null → Spawn Points above
    }

    private void FixedUpdate()
    {
        if (rb.position.y < fallLimitY)
            Die();
    }

    public void Die()
    {
        if (isDead || traveling)
            return;

        StartCoroutine(DieRoutine());
    }

    // Takes the player to another map: freeze, burst into pixels, changeMap() swaps the map behind the CRT picture,
    // then the revive brings the player back there. onArrived runs once control is back.
    public void TravelToNewMap(System.Action changeMap, System.Action onArrived)
    {
        if (isDead || traveling)
            return;

        StartCoroutine(TravelRoutine(changeMap, onArrived));
    }

    private IEnumerator DieRoutine()
    {
        // 1. Death: no physics, no input, no shooting. The player explodes; the game keeps running for a moment.
        isDead = true;
        TakeControlAway();

        Vector3 deathPoint = CameraView.ClampInside(cam, rb.position, screenMargin);
        transform.position = deathPoint; // physics is off, so moving the transform is fine
        SetVisible(false);
        SpawnEffect(explosionEffect, deathPoint, Quaternion.Euler(0f, 0f, 90f)); // burst points up, into the arena
        if (cameraShake != null)
            cameraShake.Shake(explosionShakeStrength, explosionShakeDuration);
        PlaySound(explosionSound);
        yield return new WaitForSecondsRealtime(freezeDelay);

        // 2. Time freezes: the game slows almost to a stop and the screen curves.
        yield return FreezeTime();

        // 3. Roguelite: the run is over (unless a core gives one more life). Time stays frozen behind the Game Over screen.
        bool extraLife = cores != null && cores.TryUseExtraLife();
        if (gameOverOnDeath && gameOverScreen != null && !extraLife)
        {
            gameOverScreen.Show();
            yield break;
        }

        yield return Revive(true);
        isDead = false;
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
        yield return FreezeTime();

        // 2. New map, behind the CRT picture.
        changeMap?.Invoke();

        // 3. The player comes back on it.
        yield return Revive(false);
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

    // The game slows almost to a stop and the screen curves (CRT / fish-eye), then the frozen moment lasts a bit.
    private IEnumerator FreezeTime()
    {
        PlaySound(timeFreezeSound);
        float speedFrom = gameSpeed;
        float crtFrom = crtScreen != null ? crtScreen.Intensity : 0f;
        for (float t = 0f; t < enterTime; t += Time.unscaledDeltaTime)
        {
            SetSpeed(Mathf.Lerp(speedFrom, slowMotionSpeed, t / enterTime));
            SetCrt(Mathf.Lerp(crtFrom, 1f, t / enterTime));
            yield return null;
        }
        SetSpeed(slowMotionSpeed);
        SetCrt(1f);
        yield return new WaitForSecondsRealtime(freezeTime);
    }

    // Pixels gather where the player is; the player reappears, is pulled to a random spawn point,
    // and everything goes back to normal. refill = full health on arrival (respawn after a death).
    private IEnumerator Revive(bool refill)
    {
        if (reviveEffect != null)
        {
            SpawnEffect(reviveEffect, transform.position, Quaternion.identity);
            PlaySound(gatherSound);
            yield return new WaitForSecondsRealtime(reviveEffect.main.startLifetime.constantMax);
        }
        SetVisible(true);
        yield return new WaitForSecondsRealtime(reviveDelay);

        // Pulled to a random spawn point (eases in and out).
        PlaySound(pullSound);
        Vector3 from = transform.position;
        Vector3 to = PickSpawnPoint();
        for (float t = 0f; t < pullTime; t += Time.unscaledDeltaTime)
        {
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / pullTime));
            yield return null;
        }
        transform.position = to;
        rb.position = to;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = true;
        if (refill && health != null)
            health.Refill();
        SpawnEffect(arriveEffect, to, Quaternion.identity);
        PlaySound(respawnSound);

        // Picture and game speed ease back to normal, then control is back.
        for (float t = 0f; t < recoverTime; t += Time.unscaledDeltaTime)
        {
            SetSpeed(Mathf.Lerp(slowMotionSpeed, 1f, t / recoverTime));
            SetCrt(1f - t / recoverTime);
            yield return null;
        }
        SetSpeed(1f);
        SetCrt(0f);
        movement.enabled = true;
        weapon.enabled = true;
    }

    // Random, but never the same point twice in a row (when there is more than one).
    private Vector3 PickSpawnPoint()
    {
        Transform[] points = maps != null ? maps.CurrentMap.SpawnPoints : spawnPoints;
        if (points == null || points.Length == 0)
            return startPosition;

        int index = Random.Range(0, points.Length);
        if (points[index] == lastSpawnPoint && points.Length > 1)
            index = (index + 1 + Random.Range(0, points.Length - 1)) % points.Length;
        lastSpawnPoint = points[index];
        return points[index].position;
    }

    private void SetSpeed(float speed)
    {
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
