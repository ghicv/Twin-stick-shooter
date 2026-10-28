using UnityEngine;

// Player hit points. Damage only counts on the host: enemies (single player) and other players' bullets and
// cores (LAN PvP, through the player's HitTarget) call TakeDamage there, and the health is sent to every machine
// (PlayerNetwork). At 0 the player dies (PlayerRespawn). Pushes are applied by the machine that moves this player.
// Single player: after a hit the player can't be hurt for a moment and blinks.
// LAN: no such pause (it would make most bullets useless), just a white flash, and smaller pushes.
[RequireComponent(typeof(Rigidbody2D))]
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
    [SerializeField] private Vector2 knockback = new Vector2(7f, 5f);

    [Tooltip("LAN: pushes from other players are multiplied by this (cores like Hammer multiply it again).")]
    [SerializeField] private float lanKnockbackScale = 0.8f;

    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private float hitFlashTime = 0.1f;

    [SerializeField] private float hitShakeStrength = 0.2f;
    [SerializeField] private float hitShakeDuration = 0.15f;

    [Tooltip("Seconds each blink (visible / faded) lasts.")]
    [SerializeField] private float blinkInterval = 0.06f;

    [Tooltip("A low thud: loud for the hurt player, soft (and where it happened) for the others.")]
    [SerializeField] private AudioClip hurtSound;

    [Range(0f, 1f)]
    [SerializeField] private float hurtVolume = 0.55f;

    [Tooltip("The other players hear a hit at this part of Hurt Volume.")]
    [Range(0f, 1f)]
    [SerializeField] private float othersHurtVolume = 0.35f;

    [Tooltip("Blood spurting out of the wound when a hit lands (every machine), like the dummies'.")]
    [SerializeField] private ParticleSystem bloodEffect;

    [Tooltip("The hit sound the dummies make too, heard by everybody where the hit landed.")]
    [SerializeField] private AudioClip hitSound;

    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 0.6f;

    [Tooltip("The lines of the hit spark (an unlit glow material).")]
    [SerializeField] private Material hitSparkMaterial;

    public float Health => net.Health;
    public float MaxHealth => net.MaxHealth;
    public bool IsDead => respawn.IsDead;
    public float StartMaxHealth => maxHealth;

    // Host side: Time.time of the last hit that hurt, and the cores of the player who did it (kill credit).
    public float LastDamageTime { get; private set; } = -100f;

    public CoreBridge LastAttacker { get; private set; }

    private Rigidbody2D rb;
    private PlayerRespawn respawn;
    private PlayerNetwork net;
    private PlayerMovement movement;
    private CoreBridge cores; // this player's own cores (null in some tests)
    private CameraShake cameraShake;
    private Color bodyColor;
    private float invulnerableTimer; // host side
    private float blinkTimer;        // every machine (shown)
    private float flashTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        respawn = GetComponent<PlayerRespawn>();
        net = GetComponent<PlayerNetwork>();
        movement = GetComponent<PlayerMovement>();
        cores = GetComponent<CoreBridge>();
        cameraShake = Camera.main.GetComponent<CameraShake>(); // null → no shake
        bodyColor = body.color;
    }

    // Host only. direction = the way the hit travels (from the attacker toward the player).
    // knockbackScale scales the push (Hammer); source = the attacker's cores (null for enemies).
    // wound = where it landed (blood and the hit sound there on every machine), if known.
    public void TakeDamage(float amount, Vector2 direction, float knockbackScale = 1f, CoreBridge source = null,
                           Vector2? wound = null)
    {
        float before = Health;
        bool shielded = net.Shield > 0f; // the shield takes it: no blood, half the push
        bool alive = Hurt(amount, source);
        if (wound.HasValue && !shielded && (alive || Health < before)) // it landed (also the killing blow)
            net.PlayBleed(wound.Value, -direction.normalized);
        if (!alive)
            return;

        float scale = knockbackScale * (GameMode.IsLan ? lanKnockbackScale : 1f) * (shielded ? 0.5f : 1f);
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

    // Every machine, every physics step: a small tug (a black hole). Only this player's own machine moves it (smooth,
    // no messages); the host notes who pulled it (credit if it ends in a fall).
    public void Pull(Vector2 impulse, CoreBridge source)
    {
        if (IsDead)
            return;
        if (net.IsHostSide && source != null)
            LastAttacker = source;
        if (net.IsMine)
            rb.linearVelocity += impulse / rb.mass;
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
        if (GameMode.IsLan && match != null && !match.InMatch)
        {
            net.PlayHurt(0f);
            ConfirmHit(source, 0f, false);
            return true; // LAN lobby: shooting each other is just for fun: hits push and flash, no health is lost
        }

        // A shield takes the damage first (PlayerShield flashes and, once it is used up, shatters).
        float shield = net.Shield;
        if (shield > 0f)
        {
            float absorbed = Mathf.Min(shield, amount);
            net.SetShield(shield - absorbed);
            amount -= absorbed;
            if (source != null)
                LastAttacker = source;
            if (amount <= 0f)
            {
                ConfirmHit(source, 0f, false); // the shooter hears the hit, no number
                return true;
            }
        }

        float dealt = Mathf.Min(amount, Health);
        net.Health = Mathf.Max(0f, Health - amount);
        invulnerableTimer = GameMode.IsLan ? lanInvulnerableTime : invulnerableTime;
        LastDamageTime = Time.time;
        if (source != null)
            LastAttacker = source;
        net.PlayHurt(invulnerableTimer);
        ConfirmHit(source, dealt, Health <= 0f);
        if (source != null && source != cores)
            source.OnDamageDealt(dealt, transform.position); // Vampire

        if (Health <= 0f)
        {
            respawn.Die();
            return false;
        }
        return true;
    }

    // Host: the player whose bullet or core hit this one gets the hit feedback on its own machine (damage 0 = no
    // number, e.g. the lobby).
    private void ConfirmHit(CoreBridge source, float damage, bool knockout)
    {
        PlayerNetwork attacker = source != null ? source.GetComponent<PlayerNetwork>() : null;
        if (attacker != null && attacker != net)
            attacker.ConfirmHit((Vector2)transform.position + Vector2.up * 0.9f, damage, knockout);
    }

    // Every machine: sound, flash, blink (and a shake on the hurt player's own screen).
    public void PlayHurt(float blinkTime)
    {
        flashTimer = hitFlashTime;
        blinkTimer = Mathf.Max(blinkTimer, blinkTime);
        if (TryGetComponent(out CoreLooks looks))
            looks.OnHurt(); // Thick Skin
        if (cameraShake != null && net.IsMine)
            cameraShake.Shake(hitShakeStrength, hitShakeDuration);
        if (net.IsMine)
            GameAudio.Play(hurtSound, hurtVolume, 0.06f);
        else
            GameAudio.PlayAt(hurtSound, transform.position, hurtVolume * othersHurtVolume, 0.06f);
    }

    // Every machine: blood spurts out of the wound (normal = out of the body, back toward the shooter), with the hit
    // sound there and a squash of the body: the same hit as the dummies'.
    public void PlayBleed(Vector2 point, Vector2 normal)
    {
        if (bloodEffect != null)
        {
            float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
            ParticleSystem blood = Instantiate(bloodEffect, point + normal * 0.05f, Quaternion.Euler(0f, 0f, angle));
            Destroy(blood.gameObject, 5f); // the effect doesn't destroy itself
        }
        GameAudio.PlayAt(hitSound, point, hitVolume, 0.1f);
        HitSpark(point + normal * 0.1f, normal);
        if (TryGetComponent(out PlayerBodyAnimator animator))
            animator.PlayHit(); // squashed, like the dummies
    }

    // A fighting-game hit spark, drawn over the bullets' effects so a hit stands out among them: a white flash ring,
    // a 4-point star (white rays edged in the hurt player's color, the longest back toward the shooter) and specks.
    private void HitSpark(Vector2 at, Vector2 normal)
    {
        if (hitSparkMaterial == null)
            return;
        Color edge = new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0.85f);
        CoreEffects.Ring(hitSparkMaterial, Color.white, at, 0.85f, 0.16f, 0.12f);
        for (int k = 0; k < 4; k++)
        {
            Vector2 ray = Quaternion.Euler(0f, 0f, 45f + k * 90f) * normal;
            float length = Vector2.Dot(ray, normal) > 0f ? 0.75f : 0.45f;
            CoreEffects.Beam(hitSparkMaterial, edge, Color.white, at, at + ray * length, 0.2f, 0.1f);
        }
        CoreEffects.Burst(at, edge, 6, 7f, normal, 0.12f);
    }

    // Owner: pushed by a hit or a pull. set = replace the speed (hit), otherwise add to it.
    // A hit gives the air jumps back, so a player knocked away can still get back to the map.
    public void ApplyPush(Vector2 velocity, bool set)
    {
        rb.linearVelocity = set ? velocity : rb.linearVelocity + velocity;
        if (set)
            movement.RefreshAirJumps();
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
