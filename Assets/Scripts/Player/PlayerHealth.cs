using UnityEngine;

// Player hit points. Damage only counts on the host: enemies (single player) and other players' bullets and
// cores (LAN PvP, through the player's HitTarget) call TakeDamage there, and the health is sent to every machine
// (PlayerNetwork). At 0 the player dies (PlayerRespawn). Pushes are applied by the machine that moves this player.
// Single player: after a hit the player can't be hurt for a moment and blinks.
// LAN: no such pause (it would make most bullets useless), just a white flash, and smaller pushes.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(PlayerRespawn))]
[RequireComponent(typeof(PlayerNetwork))]
[RequireComponent(typeof(HitTarget))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Single player: seconds the player can't be hurt after a hit (blinks meanwhile).")]
    [SerializeField] private float invulnerableTime = 0.6f;

    [Tooltip("LAN: seconds the player can't be hurt after a hit.")]
    [SerializeField] private float lanInvulnerableTime = 0f;

    [Tooltip("Seconds the player can't be hurt after respawning.")]
    [SerializeField] private float spawnProtectionTime = 1f;

    [Header("Hit Feedback")]
    [Tooltip("Body sprite that blinks while the player can't be hurt.")]
    [SerializeField] private SpriteRenderer body;

    [Tooltip("Speed the player is pushed with when hit: X away from the hit, Y up (units/sec).")]
    [SerializeField] private Vector2 knockback = new Vector2(6f, 4f);

    [Tooltip("LAN: pushes from other players are multiplied by this (cores like Hammer multiply it again).")]
    [SerializeField] private float lanKnockbackScale = 0.5f;

    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private float hitFlashTime = 0.06f;

    [SerializeField] private float hitShakeStrength = 0.2f;
    [SerializeField] private float hitShakeDuration = 0.15f;

    [Tooltip("Seconds each blink (visible / faded) lasts.")]
    [SerializeField] private float blinkInterval = 0.06f;

    [SerializeField] private AudioClip hurtSound;

    [Range(0f, 1f)]
    [SerializeField] private float hurtVolume = 0.7f;

    [Header("Crash")]
    [Tooltip("Pushed into a wall at least this fast, shortly after a hit, counts as a crash (cores can react) (units/sec).")]
    [SerializeField] private float crashSpeed = 5f;

    [SerializeField] private float crashWindow = 0.6f;

    public float Health => net.Health;
    public float MaxHealth => net.MaxHealth;
    public bool IsDead => respawn.IsDead;
    public float StartMaxHealth => maxHealth;

    // Host side: Time.time of the last hit that hurt, and the cores of the player who did it (kill credit).
    public float LastDamageTime { get; private set; } = -100f;
    public CoreBridge LastAttacker { get; private set; }

    private Rigidbody2D rb;
    private AudioSource audioSource;
    private PlayerRespawn respawn;
    private PlayerNetwork net;
    private PlayerMovement movement;
    private CameraShake cameraShake;
    private Color bodyColor;
    private float invulnerableTimer; // host side
    private float blinkTimer;        // every machine (shown)
    private float flashTimer;
    private float pushTime = -100f;  // owner side: last push (for crashes)

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        respawn = GetComponent<PlayerRespawn>();
        net = GetComponent<PlayerNetwork>();
        movement = GetComponent<PlayerMovement>();
        cameraShake = Camera.main.GetComponent<CameraShake>(); // null → no shake
        bodyColor = body.color;
    }

    // Host only. direction = the way the hit travels (from the attacker toward the player).
    // knockbackScale scales the push (Hammer); source = the attacker's cores (null for enemies).
    public void TakeDamage(float amount, Vector2 direction, float knockbackScale = 1f, CoreBridge source = null)
    {
        if (!Hurt(amount, source))
            return;

        float scale = knockbackScale * (GameMode.IsLan ? lanKnockbackScale : 1f);
        net.PushOwner(new Vector2(Mathf.Sign(direction.x) * knockback.x, knockback.y) * scale, true);
    }

    // Host only. Damage without a push (burning).
    public void TakeTickDamage(float amount, CoreBridge source)
    {
        Hurt(amount, source);
    }

    // Host only. A push added to the player's speed (e.g. a Vortex pull).
    public void Knock(Vector2 impulse, CoreBridge source)
    {
        if (IsDead)
            return;
        if (source != null)
            LastAttacker = source;
        net.PushOwner(impulse / rb.mass, false);
    }

    // Host only. Moves slower for a while.
    public void Slow(float factor, float duration)
    {
        if (!IsDead)
            net.SlowOwner(factor, duration);
    }

    // Lowers the health; false = the hit didn't count (dead, away, or can't be hurt right now).
    private bool Hurt(float amount, CoreBridge source)
    {
        if (!net.IsHostSide || IsDead || respawn.IsTraveling || invulnerableTimer > 0f)
            return false;
        MatchManager match = MatchManager.Instance;
        if (GameMode.IsLan && match != null && match.InMatch && !match.RoundOn)
            return false; // LAN: nobody gets hurt once the round is decided (bullets still flying)

        net.Health = Mathf.Max(0f, Health - amount);
        invulnerableTimer = GameMode.IsLan ? lanInvulnerableTime : invulnerableTime;
        LastDamageTime = Time.time;
        if (source != null)
            LastAttacker = source;
        net.PlayHurt(invulnerableTimer);

        if (Health <= 0f)
        {
            respawn.Die();
            return false;
        }
        return true;
    }

    // Every machine: sound, flash, blink (and a shake on the hurt player's own screen).
    public void PlayHurt(float blinkTime)
    {
        flashTimer = hitFlashTime;
        blinkTimer = Mathf.Max(blinkTimer, blinkTime);
        if (cameraShake != null && net.IsMine)
            cameraShake.Shake(hitShakeStrength, hitShakeDuration);
        if (hurtSound != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(hurtSound, hurtVolume);
        }
    }

    // Owner: pushed by a hit or a pull. set = replace the speed (hit), otherwise add to it.
    public void ApplyPush(Vector2 velocity, bool set)
    {
        pushTime = Time.time;
        rb.linearVelocity = set ? velocity : rb.linearVelocity + velocity;
    }

    // Owner: slowed down.
    public void ApplySlow(float factor, float duration)
    {
        movement.Slow(factor, duration);
    }

    // The player's color (each LAN player has its own). Blinking keeps using it.
    public void SetBodyColor(Color color)
    {
        bodyColor = color;
        body.color = color;
    }

    // Host only. Gives health back (never above Max Health). Nothing happens while dead.
    public void Heal(float amount)
    {
        if (net.IsHostSide && !IsDead)
            net.Health = Mathf.Min(MaxHealth, Health + amount);
    }

    // Host only. Raises Max Health and gives that much health right away.
    public void AddMaxHealth(float amount)
    {
        if (!net.IsHostSide)
            return;
        net.MaxHealth += amount;
        Heal(amount);
    }

    // Can't be hurt for a moment (e.g. a dash). Never shortens a longer protection. Asks the host if needed.
    public void MakeInvulnerable(float duration)
    {
        if (net.IsHostSide)
            SetInvulnerable(duration);
        else
            net.RequestInvulnerable(duration);
    }

    // Host only.
    public void SetInvulnerable(float duration)
    {
        invulnerableTimer = Mathf.Max(invulnerableTimer, duration);
    }

    // Host only. Full health again, plus a short spawn protection (blinking on every machine).
    public void Refill()
    {
        if (!net.IsHostSide)
            return;
        net.Health = MaxHealth;
        invulnerableTimer = spawnProtectionTime;
        net.PlayHurtBlinkOnly(spawnProtectionTime);
    }

    // Every machine: blink without the hit sound and flash (spawn protection).
    public void PlayBlink(float blinkTime)
    {
        blinkTimer = Mathf.Max(blinkTimer, blinkTime);
    }

    // Owner: pushed into a wall right after a hit → the host tells the attacker's cores (Crash).
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!net.IsMine || Time.time - pushTime > crashWindow)
            return;

        ContactPoint2D contact = collision.GetContact(0);
        if (Mathf.Abs(contact.normal.x) > 0.5f && collision.relativeVelocity.magnitude >= crashSpeed)
        {
            pushTime = -100f; // once per push
            net.ReportCrash(contact.point);
        }
    }

    private void Update()
    {
        if (invulnerableTimer > 0f)
            invulnerableTimer -= Time.deltaTime;
        if (blinkTimer > 0f)
            blinkTimer -= Time.deltaTime;
        if (flashTimer > 0f)
            flashTimer -= Time.deltaTime;

        // Blink (fade the body in and out) while hits are ignored; a short white flash on every hit.
        bool faded = blinkTimer > 0f && !IsDead && Mathf.Repeat(blinkTimer, blinkInterval * 2f) < blinkInterval;
        Color color = flashTimer > 0f ? hitFlashColor : bodyColor;
        body.color = faded ? new Color(color.r, color.g, color.b, 0.3f) : color;
    }
}
