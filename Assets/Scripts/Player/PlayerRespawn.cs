using System.Collections;
using UnityEngine;

// Falling off the map or losing all health (PlayerHealth calls Die) = death. Retro death + respawn sequence:
// 1. The player explodes where it fell out of view. The game keeps running for a moment (Freeze Delay).
// 2. Time freezes: the game slows almost to a stop and the screen curves into an old CRT / fish-eye look.
// 3. Pixels gather where it exploded (with a sound) and the player reappears where they meet.
// 4. After a short pause (Revive Delay) it is pulled to a random spawn point.
// 5. Normal picture, normal speed, control back.
// While dead, the player's physics, movement and gun are off. All timings are real seconds.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("Falling below this height (world Y) kills the player.")]
    [SerializeField] private float fallLimitY = -8f;

    [Tooltip("The player comes back at one of these, picked at random (never the same one twice in a row). Empty = where the player started.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Death")]
    [Tooltip("Pixel explosion spawned where the player died. It is kept inside the screen so it can be seen.")]
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

    [Header("Respawn")]
    [Tooltip("Pixels that gather where the player exploded; the player reappears when they meet, i.e. after the " +
             "particles' Start Lifetime. It should use Unscaled Time so it plays at full speed during the slow motion. Optional.")]
    [SerializeField] private ParticleSystem reviveEffect;

    [Tooltip("Seconds the player stays where it reappeared before it is pulled to the spawn point.")]
    [SerializeField] private float reviveDelay = 0.5f;

    [Tooltip("Seconds the player takes to travel from where it died to the spawn point.")]
    [SerializeField] private float pullTime = 0.6f;

    [Tooltip("Seconds for the picture and the game speed to get back to normal after arriving. Then control is back.")]
    [SerializeField] private float recoverTime = 0.35f;

    [Tooltip("Spawned at the player when it arrives at the spawn point (e.g. DustPuff). Optional.")]
    [SerializeField] private ParticleSystem arriveEffect;

    [Tooltip("Death effects are removed after this many seconds.")]
    [SerializeField] private float effectCleanupTime = 5f;

    [Header("Sound")]
    [Tooltip("Played when the player explodes.")]
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
    private Vector2 startPosition;
    private int lastSpawnIndex = -1;
    private float gameSpeed = 1f;
    private bool isDead;

    // True from the moment of death until control is back.
    public bool IsDead => isDead;

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
    }

    private void FixedUpdate()
    {
        if (rb.position.y < fallLimitY)
            Die();
    }

    public void Die()
    {
        if (isDead)
            return;

        StartCoroutine(DieAndRespawn());
    }

    private IEnumerator DieAndRespawn()
    {
        // 1. Death: no physics, no input, no shooting. The player explodes; the game keeps running for a moment.
        isDead = true;
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;
        movement.enabled = false;
        weapon.enabled = false;

        Vector3 deathPoint = CameraView.ClampInside(cam, rb.position, screenMargin);
        transform.position = deathPoint; // physics is off, so moving the transform is fine
        SetVisible(false);
        SpawnEffect(explosionEffect, deathPoint, Quaternion.Euler(0f, 0f, 90f)); // burst points up, into the arena
        if (cameraShake != null)
            cameraShake.Shake(explosionShakeStrength, explosionShakeDuration);
        PlaySound(explosionSound);
        yield return new WaitForSecondsRealtime(freezeDelay);

        // 2. Time freezes: the game slows almost to a stop and the screen curves.
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

        // 3. Pixels gather where the player exploded; when they meet, the player is back.
        if (reviveEffect != null)
        {
            SpawnEffect(reviveEffect, transform.position, Quaternion.identity);
            PlaySound(gatherSound);
            yield return new WaitForSecondsRealtime(reviveEffect.main.startLifetime.constantMax);
        }
        SetVisible(true);
        yield return new WaitForSecondsRealtime(reviveDelay);

        // 4. Pulled to a random spawn point (eases in and out).
        PlaySound(pullSound);
        Vector3 from = transform.position;
        Vector3 to = PickSpawnPoint();
        for (float t = 0f; t < pullTime; t += Time.unscaledDeltaTime)
        {
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / pullTime));
            yield return null;
        }
        transform.position = to;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = true;
        if (health != null)
            health.Refill();
        SpawnEffect(arriveEffect, to, Quaternion.identity);
        PlaySound(respawnSound);

        // 5. Picture and game speed ease back to normal, then control is back.
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
        isDead = false;
    }

    private Vector3 PickSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return startPosition;

        // Random, but never the same point twice in a row (when there is more than one).
        int index = Random.Range(0, spawnPoints.Length);
        if (index == lastSpawnIndex && spawnPoints.Length > 1)
            index = (index + 1 + Random.Range(0, spawnPoints.Length - 1)) % spawnPoints.Length;
        lastSpawnIndex = index;
        return spawnPoints[index].position;
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
