using UnityEngine;

// Player hit points. Enemies (melee hits, enemy bullets) call TakeDamage. At 0 the player dies:
// PlayerRespawn plays the death + respawn and calls Refill when the player is back.
// After a hit the player can't be hurt for a moment and blinks.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(PlayerRespawn))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Seconds the player can't be hurt after a hit (blinks meanwhile).")]
    [SerializeField] private float invulnerableTime = 0.6f;

    [Tooltip("Seconds the player can't be hurt after respawning.")]
    [SerializeField] private float spawnProtectionTime = 1f;

    [Header("Hit Feedback")]
    [Tooltip("Body sprite that blinks while the player can't be hurt.")]
    [SerializeField] private SpriteRenderer body;

    [Tooltip("Speed the player is pushed with when hit: X away from the hit, Y up (units/sec).")]
    [SerializeField] private Vector2 knockback = new Vector2(6f, 4f);

    [SerializeField] private float hitShakeStrength = 0.2f;
    [SerializeField] private float hitShakeDuration = 0.15f;

    [Tooltip("Seconds each blink (visible / faded) lasts.")]
    [SerializeField] private float blinkInterval = 0.06f;

    [SerializeField] private AudioClip hurtSound;

    [Range(0f, 1f)]
    [SerializeField] private float hurtVolume = 0.7f;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead => respawn.IsDead;

    // Time.time of the last hit that hurt.
    public float LastDamageTime { get; private set; } = -100f;

    private Rigidbody2D rb;
    private AudioSource audioSource;
    private PlayerRespawn respawn;
    private CameraShake cameraShake;
    private Color bodyColor;
    private float invulnerableTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        respawn = GetComponent<PlayerRespawn>();
        cameraShake = Camera.main.GetComponent<CameraShake>(); // null → no shake
        bodyColor = body.color;
        Health = maxHealth;
    }

    // direction = the way the hit travels (from the attacker toward the player).
    public void TakeDamage(float amount, Vector2 direction)
    {
        if (IsDead || respawn.IsTraveling || invulnerableTimer > 0f)
            return;

        Health = Mathf.Max(0f, Health - amount);
        invulnerableTimer = invulnerableTime;
        LastDamageTime = Time.time;

        if (cameraShake != null)
            cameraShake.Shake(hitShakeStrength, hitShakeDuration);
        if (hurtSound != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(hurtSound, hurtVolume);
        }

        if (Health <= 0f)
        {
            respawn.Die();
            return;
        }

        rb.linearVelocity = new Vector2(Mathf.Sign(direction.x) * knockback.x, knockback.y);
    }

    // Gives health back (never above Max Health). Nothing happens while dead.
    public void Heal(float amount)
    {
        if (!IsDead)
            Health = Mathf.Min(maxHealth, Health + amount);
    }

    // Raises Max Health and gives that much health right away.
    public void AddMaxHealth(float amount)
    {
        maxHealth += amount;
        Heal(amount);
    }

    // Can't be hurt for a moment (e.g. a dash). Never shortens a longer protection.
    public void MakeInvulnerable(float duration)
    {
        invulnerableTimer = Mathf.Max(invulnerableTimer, duration);
    }

    // Full health again, plus a short spawn protection. Called by PlayerRespawn.
    public void Refill()
    {
        Health = maxHealth;
        invulnerableTimer = spawnProtectionTime;
    }

    private void Update()
    {
        if (invulnerableTimer > 0f)
            invulnerableTimer -= Time.deltaTime;

        // Blink (fade the body in and out) while hits are ignored.
        bool faded = invulnerableTimer > 0f && !IsDead && Mathf.Repeat(invulnerableTimer, blinkInterval * 2f) < blinkInterval;
        body.color = faded ? new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0.3f) : bodyColor;
    }
}
