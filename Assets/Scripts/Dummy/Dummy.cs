using UnityEngine;

// Target dummy for testing weapons. It takes damage from projectiles and gets knocked back by them
// (a real Rigidbody2D push; ground friction slows it down again).
// Every hit: blood, a floating damage number, a hit sound, a white flash, a squash, a small camera shake,
// and a health bar whose white "chip" trails behind to show how much was just lost.
// Killing blow (retro style): a freeze frame, then the dummy blinks white for a moment, explodes into pixels
// and disables itself; its DummySpawner then spawns a fresh one.
// Falling off the map: it explodes right away, where it left the screen (like the player).
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class Dummy : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Falling below this height (world Y) makes the dummy explode (its spawner makes a new one).")]
    [SerializeField] private float fallLimitY = -8f;

    [Tooltip("When it falls off the map, the explosion is placed at least this far inside the screen edge (units).")]
    [SerializeField] private float screenMargin = 0.8f;

    [Header("References")]
    [Tooltip("Body sprite: flashes and squashes on hit, blinks while dying.")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Tooltip("Pivot at the left edge of the health bar; its X scale is the health fraction (0..1).")]
    [SerializeField] private Transform healthBarFill;

    [Tooltip("Like the fill, drawn behind it: follows the health down after a short delay.")]
    [SerializeField] private Transform healthBarChip;

    [Header("Effects")]
    [Tooltip("Blood spurt, its +X axis pointing where the blood flies.")]
    [SerializeField] private ParticleSystem bloodEffect;

    [Tooltip("Spawned at the body's center when the dummy explodes (pixel flash, rings, pieces, smoke). Plays its own sound.")]
    [SerializeField] private ParticleSystem explosionEffect;

    [Tooltip("Blood and explosion effects are removed after this many seconds.")]
    [SerializeField] private float effectCleanupTime = 5f;

    [SerializeField] private DamageNumber damageNumberPrefab;

    [Tooltip("Damage numbers appear this far above the hit point (units).")]
    [SerializeField] private float damageNumberOffset = 0.3f;

    [Header("Hit Feedback")]
    [Tooltip("Impulse that pushes the dummy away from the shooter on every hit. With mass 1 this is the push speed (units/sec).")]
    [SerializeField] private float knockbackForce = 3f;

    [SerializeField] private Color hitFlashColor = Color.white;

    [Tooltip("Seconds the body stays flashed after a hit (and after spawning).")]
    [SerializeField] private float hitFlashDuration = 0.06f;

    [Tooltip("Body scale right after a hit (x wider, y shorter).")]
    [SerializeField] private Vector2 hitSquash = new Vector2(1.15f, 0.88f);

    [Tooltip("How quickly the body gets its normal shape back (higher = snappier).")]
    [SerializeField] private float squashRecovery = 15f;

    [Tooltip("Camera shake per hit (units). Needs a CameraShake on the main camera.")]
    [SerializeField] private float hitShakeStrength = 0.08f;

    [SerializeField] private float hitShakeDuration = 0.1f;

    [Tooltip("Seconds the white chip waits before sliding down to the real health.")]
    [SerializeField] private float chipDelay = 0.3f;

    [Tooltip("How fast the chip slides down (health bar widths per second).")]
    [SerializeField] private float chipSpeed = 1.5f;

    [SerializeField] private Color fullHealthColor = new Color(0.4f, 0.9f, 0.4f);
    [SerializeField] private Color lowHealthColor = new Color(0.95f, 0.3f, 0.25f);

    [Header("Death")]
    [Tooltip("Freeze frame on the killing blow (real-time seconds). Needs a HitStop on the main camera.")]
    [SerializeField] private float hitStopDuration = 0.06f;

    [Tooltip("Seconds the dummy blinks white after the killing blow, before it explodes.")]
    [SerializeField] private float deathBlinkTime = 0.25f;

    [Tooltip("Seconds each blink (white / normal) lasts.")]
    [SerializeField] private float deathBlinkInterval = 0.04f;

    [SerializeField] private float explosionShakeStrength = 0.3f;
    [SerializeField] private float explosionShakeDuration = 0.25f;

    [Header("Sound")]
    [SerializeField] private AudioClip hitSound;

    [Tooltip("Random pitch change per hit, so repeated hits don't sound identical.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float pitchVariation = 0.1f;

    private float health;
    private float dyingTimer;
    private float chipFraction = 1f;
    private float chipTimer;
    private float flashTimer;
    private Vector2 bodyShape = Vector2.one;
    private Color bodyColor;
    private Vector3 bodyBaseScale;
    private Vector3 bodyBasePosition;
    private float bodyHalfHeight;
    private SpriteRenderer healthBarRenderer;
    private Rigidbody2D rb;
    private Camera cam;
    private CameraShake cameraShake;
    private HitStop hitStop;
    private AudioSource audioSource;

    // Read by the enemy scripts: no acting while dying, and no steering right after a hit (knockback).
    public bool IsDying => health <= 0f;
    public float LastHitTime { get; private set; } = -100f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        cam = Camera.main;
        cameraShake = cam.GetComponent<CameraShake>(); // null → no shake
        hitStop = cam.GetComponent<HitStop>();         // null → no freeze frame

        health = maxHealth;
        bodyColor = bodyRenderer.color;
        bodyBaseScale = bodyRenderer.transform.localScale;
        bodyBasePosition = bodyRenderer.transform.localPosition;
        bodyHalfHeight = bodyRenderer.sprite.bounds.extents.y * bodyBaseScale.y;
        healthBarRenderer = healthBarFill.GetComponentInChildren<SpriteRenderer>();
        UpdateHealthBar();

        // Pop in: grow from the feet to full size, with a flash.
        bodyShape = Vector2.zero;
        flashTimer = hitFlashDuration;
    }

    // hitNormal points out of the dummy's surface at the hit point (back toward the shooter).
    public void TakeDamage(float amount, Vector2 hitPoint, Vector2 hitNormal)
    {
        if (IsDying)
            return;

        LastHitTime = Time.time;

        // Blood spurts out of the wound, starting slightly outside the body.
        SpawnEffect(bloodEffect, hitPoint + hitNormal * 0.05f, hitNormal);

        health = Mathf.Max(0f, health - amount);
        chipTimer = chipDelay;
        UpdateHealthBar();

        DamageNumber number = Instantiate(damageNumberPrefab, (Vector3)hitPoint + Vector3.up * damageNumberOffset, Quaternion.identity);
        number.Show(amount);

        // Knockback: pushed away from the shooter (the normal points back toward them).
        rb.AddForce(-hitNormal * knockbackForce, ForceMode2D.Impulse);

        PlaySound(hitSound);
        flashTimer = hitFlashDuration;
        bodyShape = hitSquash;
        if (cameraShake != null)
            cameraShake.Shake(hitShakeStrength, hitShakeDuration);

        if (IsDying)
        {
            // Killing blow: freeze frame, then blink for a moment before exploding (see Update).
            dyingTimer = deathBlinkTime;
            if (hitStop != null)
                hitStop.Freeze(hitStopDuration);
        }
    }

    private void Explode(Vector2 position)
    {
        SpawnEffect(explosionEffect, position, Vector2.up);
        if (cameraShake != null)
            cameraShake.Shake(explosionShakeStrength, explosionShakeDuration);

        gameObject.SetActive(false); // gone; the DummySpawner replaces it with a new one
    }

    private void FixedUpdate()
    {
        // Pushed off the map: explode where it left the screen, so the explosion can be seen.
        // Uses the physics position (the body's center), which the sprite can lag behind by a step.
        if (rb.position.y < fallLimitY)
            Explode(CameraView.ClampInside(cam, rb.position, screenMargin));
    }

    private void Update()
    {
        float dt = Time.deltaTime; // 0 during hitstop, so everything freezes

        // Squash eases back; the feet stay where they are.
        float t = 1f - Mathf.Exp(-squashRecovery * dt);
        bodyShape = Vector2.Lerp(bodyShape, Vector2.one, t);
        Transform body = bodyRenderer.transform;
        body.localScale = new Vector3(bodyBaseScale.x * bodyShape.x, bodyBaseScale.y * bodyShape.y, bodyBaseScale.z);
        body.localPosition = bodyBasePosition + Vector3.down * (bodyHalfHeight * (1f - bodyShape.y));

        // Chip: waits a moment after a hit, then slides down to the real health.
        if (chipTimer > 0f)
            chipTimer -= dt;
        else
            chipFraction = Mathf.MoveTowards(chipFraction, health / maxHealth, chipSpeed * dt);
        healthBarChip.localScale = new Vector3(chipFraction, 1f, 1f);

        if (IsDying)
        {
            // Retro death: blink white on and off, then explode.
            dyingTimer -= dt;
            bool white = Mathf.Repeat(dyingTimer, deathBlinkInterval * 2f) < deathBlinkInterval;
            bodyRenderer.color = white ? hitFlashColor : bodyColor;
            if (dyingTimer <= 0f)
                Explode(bodyRenderer.bounds.center);
            return;
        }

        if (flashTimer > 0f)
            flashTimer -= dt;
        bodyRenderer.color = flashTimer > 0f ? hitFlashColor : bodyColor;
    }

    private void SpawnEffect(ParticleSystem prefab, Vector2 position, Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        ParticleSystem effect = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
        Destroy(effect.gameObject, effectCleanupTime); // the effects don't destroy themselves
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null)
            return;

        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip);
    }

    private void UpdateHealthBar()
    {
        float fraction = health / maxHealth;
        healthBarFill.localScale = new Vector3(fraction, 1f, 1f);
        healthBarRenderer.color = Color.Lerp(lowHealthColor, fullHealthColor, fraction);
    }
}
