using System.Collections.Generic;
using UnityEngine;

// What the bullet cores do, stage by stage in a bullet's life: SPAWN → FLY → HIT → END. Only CoreBridge calls this.
// It lives on the player (each player has its own cores). Targets are HitTargets: enemies in single player,
// the other players on LAN; a player's bullets and blasts never hurt that player.
// The rules that make any mix of bullet cores work together without special combos:
// 1. Every bullet (extra, back shot, nova, fork, shrapnel) carries all the bullet cores, but the small ones made
//    from another bullet (fork, shrapnel = "shards") never make anything more (no fork, shrapnel or mines).
// 2. FLY cores add up (homing keeps steering after a bounce, a lob still waves, ...).
// 3. Every HIT core happens on every hit (a bullet that bounces or pierces explodes each time).
// 4. A bullet only disappears when no core keeps it alive (Bounce/Drill on walls, Pierce on targets).
// 5. Each core has its own cost (less damage, slower fire rate, ...).
// Networking: every random choice comes from the shot's seed, so the host's real bullets and the other machines'
// look-alikes (LAN) behave the same. Look-alikes only show things (blasts, sparks, shards); they never hurt.
[RequireComponent(typeof(CoreInventory))]
public class BulletCores : MonoBehaviour
{
    // What the core system remembers about one bullet (kept on it as Projectile.CoreData).
    private class BulletState
    {
        public Projectile prefab;
        public Rigidbody2D shooter;
        public float speed;
        public float damage;      // when it was fired, before any cost (shrapnel damage is based on it)
        public float size;
        public bool isShard;
        public bool lookAlike;
        public System.Random random; // this bullet's own random numbers (same on every machine)
        public int bouncesLeft;
        public int piercesLeft;
        public int drillsLeft;
        public bool forked;
        public HitTarget target;  // homing
        public float age;
        public float distance;    // long shot
        public float longShotFactor = 1f;
        public float waveOffset;
        public bool returning;    // boomerang
    }

    private class Burn
    {
        public HitTarget target;
        public float timeLeft;
        public float tickTimer;
    }

    [Header("SPAWN · +1 Bullet")]
    [Tooltip("Angle between the two bullets of a shot (degrees).")]
    [SerializeField] private float extraBulletAngle = 14f;

    [Tooltip("The extra bullet adds this share of a normal bullet's damage to the shot, and the total is split " +
             "between the two bullets (0.4: they deal 140% together, 70% each).")]
    [Range(0f, 1f)]
    [SerializeField] private float extraBulletDamage = 0.4f;

    [Header("SPAWN · Back Shot")]
    [Tooltip("Bullets fired backwards deal this share of the damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float backShotDamage = 0.6f;

    [Header("SPAWN · Lucky Shot")]
    [Range(0f, 1f)]
    [SerializeField] private float luckyChance = 0.15f;

    [Tooltip("Damage multiplier of a lucky (critical) bullet.")]
    [SerializeField] private float luckyDamage = 2.5f;

    [SerializeField] private float luckySize = 1.4f;
    [SerializeField] private Color luckyColor = new Color(1f, 0.42f, 0.21f);

    [Header("SPAWN · Nova")]
    [Tooltip("Every this many shots, a ring of bullets goes out too.")]
    [SerializeField] private int novaEvery = 6;

    [SerializeField] private int novaCount = 8;

    [Range(0f, 1f)]
    [SerializeField] private float novaDamage = 0.5f;

    [Header("SPAWN · Giant")]
    [SerializeField] private float giantSize = 1.8f;
    [SerializeField] private float giantDamage = 1.3f;

    [Tooltip("Speed multiplier of giant bullets.")]
    [SerializeField] private float giantSpeed = 0.8f;

    [Header("FLY · Homing")]
    [Tooltip("Only targets closer than this are chased (units).")]
    [SerializeField] private float homingRange = 5f;

    [Tooltip("Only targets within this angle of the flight direction can be chased (degrees, each side).")]
    [SerializeField] private float homingConeAngle = 60f;

    [Tooltip("How fast the bullet turns toward a target at the edge of Homing Range (degrees/sec).")]
    [SerializeField] private float homingTurnSpeed = 360f;

    [Tooltip("The turn gets this many times faster as the target gets close (3 = 4x right next to it), " +
             "so a fast bullet doesn't keep circling around a close target.")]
    [SerializeField] private float homingCloseBoost = 3f;

    [Tooltip("A homing bullet deals this share of its damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float homingDamage = 0.8f;

    [Header("FLY · Bounce")]
    [Tooltip("Wall bounces per bullet.")]
    [Min(1)]
    [SerializeField] private int bounceCount = 2;

    [Tooltip("The bullet keeps this share of its damage after each bounce.")]
    [Range(0f, 1f)]
    [SerializeField] private float bounceDamage = 0.75f;

    [Header("FLY · Pierce")]
    [Tooltip("Targets a bullet flies through before it stops (it stops in the next one).")]
    [Min(1)]
    [SerializeField] private int pierceCount = 2;

    [Tooltip("The bullet keeps this share of its damage after each target it flies through.")]
    [Range(0f, 1f)]
    [SerializeField] private float pierceDamage = 0.7f;

    [Header("FLY · Lob")]
    [Tooltip("Gravity scale of lobbed bullets (they fall in an arc).")]
    [SerializeField] private float lobGravity = 2.5f;

    [SerializeField] private float lobDamage = 1.25f;

    [Header("FLY · Wave")]
    [Tooltip("How far the bullet wiggles to each side of its path (units).")]
    [SerializeField] private float waveAmplitude = 0.5f;

    [Tooltip("Wiggles per second.")]
    [SerializeField] private float waveFrequency = 2.5f;

    [Header("FLY · Boomerang")]
    [Tooltip("Seconds of flight before the bullet turns back toward the player.")]
    [SerializeField] private float boomerangReturnTime = 0.3f;

    [Tooltip("How fast a returning bullet turns toward the player (degrees/sec).")]
    [SerializeField] private float boomerangTurnSpeed = 900f;

    [Tooltip("The player catches a returning bullet this close (units).")]
    [SerializeField] private float boomerangCatchRadius = 0.6f;

    [Header("FLY · Long Shot")]
    [Tooltip("Damage added per unit flown (0.06 = +6%).")]
    [SerializeField] private float longShotPerUnit = 0.06f;

    [Tooltip("Damage never grows beyond this multiplier.")]
    [SerializeField] private float longShotMax = 2f;

    [Header("FLY · Drill")]
    [Tooltip("Thick walls a bullet goes through (after its bounces).")]
    [Min(1)]
    [SerializeField] private int drillCount = 1;

    [Range(0f, 1f)]
    [SerializeField] private float drillDamage = 0.8f;

    [Header("HIT · Explosive")]
    [Tooltip("Targets within this distance of the blast take damage (units).")]
    [SerializeField] private float explosionRadius = 1.2f;

    [Tooltip("Blast damage to each target in range, except the one hit directly (it only takes the bullet's damage).")]
    [SerializeField] private float explosionDamage = 6f;

    [Tooltip("The fire rate is multiplied by this while Explosive is owned (explosions cost fire rate).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float explosiveFireRate = 0.75f;

    [Tooltip("Spawned at every blast (Explosive, Mine). It should destroy itself (Stop Action = Destroy).")]
    [SerializeField] private ParticleSystem explosionEffect;

    [Header("HIT · Hammer")]
    [Tooltip("Knockback of bullet hits, blasts and pulls is multiplied by this.")]
    [SerializeField] private float hammerKnockback = 2.5f;

    [Header("HIT · Fork")]
    [Tooltip("On its first target the bullet splits into two that fly on at ± this angle (degrees).")]
    [SerializeField] private float forkAngle = 25f;

    [Tooltip("Each fork deals this share of the bullet's damage at that moment.")]
    [Range(0f, 1f)]
    [SerializeField] private float forkDamage = 0.35f;

    [SerializeField] private float forkLifetime = 0.5f;
    [SerializeField] private float forkSize = 0.7f;

    [Header("HIT · Chain")]
    [Tooltip("Targets the lightning jumps to, one after another.")]
    [SerializeField] private int chainJumps = 2;

    [Tooltip("Longest jump (units).")]
    [SerializeField] private float chainRange = 3.5f;

    [Tooltip("Each jump deals this share of the bullet's damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float chainDamage = 0.4f;

    [Tooltip("Material of the lightning line (e.g. the bullet's sprite material).")]
    [SerializeField] private Material zapMaterial;

    [SerializeField] private Color zapColor = new Color(0.55f, 0.95f, 1f);

    [Header("HIT · Chill")]
    [Tooltip("Hit targets move (and enemies attack) at this share of their speed.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float chillSpeed = 0.5f;

    [SerializeField] private float chillTime = 1.5f;

    [Header("HIT · Ignite")]
    [Tooltip("Burn damage per tick.")]
    [SerializeField] private float igniteDamage = 5f;

    [Tooltip("Seconds between burn ticks.")]
    [SerializeField] private float igniteTick = 0.5f;

    [Tooltip("Seconds a burn lasts. Hitting a burning target starts it over (burns don't stack).")]
    [SerializeField] private float igniteTime = 2f;

    [Header("HIT · Vortex")]
    [Tooltip("Other targets within this distance of the hit are pulled toward it (units).")]
    [SerializeField] private float vortexRadius = 2.5f;

    [Tooltip("Pull impulse (with mass 1 = speed).")]
    [SerializeField] private float vortexForce = 4f;

    [Header("HIT · Reaper")]
    [Tooltip("A hit on a target at or below this share of its health kills it.")]
    [Range(0f, 1f)]
    [SerializeField] private float reaperThreshold = 0.15f;

    [Header("END · Shrapnel")]
    [SerializeField] private int shrapnelCount = 3;

    [Tooltip("The shards fan out over this angle around the way the bullet was going (degrees).")]
    [SerializeField] private float shrapnelSpread = 90f;

    [Tooltip("Each shard deals this share of the bullet's damage when it was fired.")]
    [Range(0f, 1f)]
    [SerializeField] private float shrapnelDamage = 0.3f;

    [Tooltip("Shard speed as a share of the bullet's speed.")]
    [SerializeField] private float shrapnelSpeed = 0.8f;

    [Tooltip("Seconds a shard flies.")]
    [SerializeField] private float shrapnelLifetime = 0.35f;

    [Tooltip("Shards are this much smaller than a bullet.")]
    [SerializeField] private float shrapnelScale = 0.6f;

    [Header("END · Mine")]
    [Tooltip("Chance that a bullet stopping on a wall or the floor leaves a mine there.")]
    [Range(0f, 1f)]
    [SerializeField] private float mineChance = 0.25f;

    [SerializeField] private int maxMines = 4;
    [SerializeField] private float mineDamage = 15f;
    [SerializeField] private float mineRadius = 1.5f;

    [Tooltip("An armed mine goes off when a target is this close (units).")]
    [SerializeField] private float mineTriggerRadius = 0.8f;

    [SerializeField] private float mineArmTime = 0.3f;
    [SerializeField] private float mineLifetime = 6f;
    [SerializeField] private Sprite mineSprite;
    [SerializeField] private Color mineColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float mineScale = 1.6f;

    private CoreInventory inventory;
    private CoreBridge bridge;
    private HitTarget self; // this player (never hurt by its own bullets)
    private int shotCount;
    private readonly List<HitTarget> chainHits = new List<HitTarget>();
    private readonly List<Burn> burns = new List<Burn>();
    private readonly List<CoreMine> mines = new List<CoreMine>();

    private void Awake()
    {
        inventory = GetComponent<CoreInventory>();
        bridge = GetComponent<CoreBridge>();
        self = GetComponent<HitTarget>();
    }

    private bool Has(CoreType type) => inventory.Has(type);

    public float FireRateMultiplier() => Has(CoreType.Explosive) ? explosiveFireRate : 1f;

    public float Knockback() => Has(CoreType.Hammer) ? hammerKnockback : 1f;

    // ---------- SPAWN ----------

    // One shot of the gun: one bullet (two with +1 Bullet), mirrored backwards with Back Shot,
    // plus a ring every few shots with Nova. size = bullet size multiplier from other cores (e.g. Opener).
    // seed = the shot's random numbers; lookAlike = only show the bullets (never hurt).
    public void FireShot(Projectile prefab, Vector2 position, Quaternion aim, float speed, float damage, float lifetime,
                         Rigidbody2D shooter, float size, int seed, bool lookAlike)
    {
        var random = new System.Random(seed);
        int count = Has(CoreType.ExtraBullet) ? 2 : 1;
        float damagePerBullet = damage * (1f + extraBulletDamage * (count - 1)) / count;
        for (int i = 0; i < count; i++)
        {
            Quaternion rotation = aim * Quaternion.Euler(0f, 0f, (i - (count - 1) * 0.5f) * extraBulletAngle);
            Spawn(prefab, position, rotation * Vector3.right, speed, damagePerBullet, lifetime, shooter, false, null, size, random.Next(), lookAlike);
            if (Has(CoreType.BackShot))
                Spawn(prefab, position, rotation * Vector3.left, speed, damagePerBullet * backShotDamage, lifetime, shooter, false, null, size, random.Next(), lookAlike);
        }

        shotCount++;
        if (Has(CoreType.Nova) && shotCount % novaEvery == 0)
        {
            for (int i = 0; i < novaCount; i++)
            {
                Quaternion rotation = aim * Quaternion.Euler(0f, 0f, (i + 0.5f) * 360f / novaCount);
                Spawn(prefab, position, rotation * Vector3.right, speed, damage * novaDamage, lifetime, shooter, false, null, size, random.Next(), lookAlike);
            }
        }
    }

    private void Spawn(Projectile prefab, Vector2 position, Vector2 direction, float speed, float damage, float lifetime,
                       Rigidbody2D shooter, bool isShard, Collider2D ignore, float size, int seed, bool lookAlike)
    {
        var random = new System.Random(seed);
        float baseDamage = damage;
        Color? tint = null;
        if (Has(CoreType.Giant))
        {
            size *= giantSize;
            damage *= giantDamage;
            speed *= giantSpeed;
        }
        if (Has(CoreType.LuckyShot) && !isShard && random.NextDouble() < luckyChance)
        {
            size *= luckySize;
            damage *= luckyDamage;
            tint = luckyColor;
        }
        if (Has(CoreType.Lob))
            damage *= lobDamage;
        if (Has(CoreType.Homing))
            damage *= homingDamage;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, speed, damage, lifetime, shooter);
        if (lookAlike)
            bullet.MakeLookAlike();
        if (size != 1f)
            bullet.SetSize(size);
        if (tint.HasValue)
            bullet.SetTint(tint.Value);
        bullet.IgnoreCollider(ignore);
        if (Has(CoreType.Lob))
            bullet.Body.gravityScale = lobGravity;

        var state = new BulletState
        {
            prefab = prefab,
            shooter = shooter,
            speed = speed,
            damage = baseDamage,
            size = size,
            isShard = isShard,
            lookAlike = lookAlike,
            random = random,
            bouncesLeft = Has(CoreType.Bounce) ? bounceCount : 0,
            piercesLeft = Has(CoreType.Pierce) ? pierceCount : 0,
            drillsLeft = Has(CoreType.Drill) ? drillCount : 0,
        };
        bullet.AttachCores(bridge, state);
    }

    // ---------- FLY ----------

    public void OnFly(Projectile bullet)
    {
        var state = (BulletState)bullet.CoreData;
        Rigidbody2D rb = bullet.Body;
        float dt = Time.fixedDeltaTime;
        state.age += dt;

        // Boomerang: after a moment it turns back toward the player, and is caught there.
        if (Has(CoreType.Boomerang) && state.shooter != null && state.age >= boomerangReturnTime)
        {
            state.returning = true;
            Vector2 toPlayer = state.shooter.position - rb.position;
            if (toPlayer.magnitude < boomerangCatchRadius)
            {
                bullet.Finish();
                return;
            }
            Vector2 back = Vector3.RotateTowards(rb.linearVelocity.normalized, toPlayer.normalized, boomerangTurnSpeed * Mathf.Deg2Rad * dt, 0f);
            bullet.SetVelocity(back * Mathf.Max(rb.linearVelocity.magnitude, state.speed));
        }

        if (Has(CoreType.Homing) && !state.returning)
            Steer(bullet, state);

        // Lob: gravity bends the path; keep the bullet facing where it flies.
        if (Has(CoreType.Lob))
            bullet.SetVelocity(rb.linearVelocity);

        // Wave: slides the bullet sideways along a wiggle, without touching its velocity (so it mixes with the rest).
        if (Has(CoreType.Wave))
        {
            float offset = waveAmplitude * Mathf.Sin(state.age * waveFrequency * 2f * Mathf.PI);
            rb.position += Vector2.Perpendicular(rb.linearVelocity.normalized) * (offset - state.waveOffset);
            state.waveOffset = offset;
        }

        // Long Shot: damage grows with the distance flown.
        if (Has(CoreType.LongShot))
        {
            state.distance += rb.linearVelocity.magnitude * dt;
            float factor = Mathf.Min(1f + longShotPerUnit * state.distance, longShotMax);
            bullet.ScaleDamage(factor / state.longShotFactor);
            state.longShotFactor = factor;
        }
    }

    // Homing: turns the flight direction toward the target a bit every physics step, keeping the speed.
    private void Steer(Projectile bullet, BulletState state)
    {
        Rigidbody2D rb = bullet.Body;
        if (state.target == null || !state.target.isActiveAndEnabled || state.target.IsDying)
            state.target = FindTarget(rb);
        if (state.target == null)
            return;

        // A fast bullet can't turn tight enough to hit a close target and would circle around it,
        // so it turns faster the closer the target is.
        Vector2 toTarget = (Vector2)state.target.transform.position - rb.position;
        float closeness = 1f - Mathf.Clamp01(toTarget.magnitude / homingRange); // 0 = at the edge of the range, 1 = right there
        float maxTurn = homingTurnSpeed * (1f + homingCloseBoost * closeness) * Mathf.Deg2Rad * Time.fixedDeltaTime;
        Vector2 direction = Vector3.RotateTowards(rb.linearVelocity.normalized, toTarget.normalized, maxTurn, 0f);
        bullet.SetVelocity(direction * rb.linearVelocity.magnitude);
    }

    // Nearest target (not this player) in range and in front of the bullet.
    private HitTarget FindTarget(Rigidbody2D rb)
    {
        HitTarget nearest = null;
        float nearestDistance = homingRange;
        foreach (HitTarget target in HitTarget.Active)
        {
            if (target == self || target.IsDying)
                continue;

            Vector2 toTarget = (Vector2)target.transform.position - rb.position;
            if (Vector2.Angle(rb.linearVelocity, toTarget) > homingConeAngle)
                continue; // behind or off to the side: not a target

            float distance = toTarget.magnitude;
            if (distance < nearestDistance)
            {
                nearest = target;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    // ---------- HIT ----------

    // The bullet has hit a target (a real bullet has already damaged it). True = it keeps flying (Pierce).
    public bool OnHitTarget(Projectile bullet, HitTarget target, Vector2 point, Vector2 normal)
    {
        var state = (BulletState)bullet.CoreData;
        bool real = !state.lookAlike;

        if (real && Has(CoreType.Reaper) && !target.IsDying && target.HealthFraction <= reaperThreshold)
            target.TakeTickDamage(target.HealthFraction * 10000f, bridge); // finished off
        if (Has(CoreType.Explosive))
            CoreEffects.Blast(explosionEffect, explosionRadius, point + normal * 0.1f, explosionRadius, explosionDamage, Knockback(), bridge, real, target, self);
        if (real && Has(CoreType.Chill))
            target.Slow(chillSpeed, chillTime);
        if (real && Has(CoreType.Ignite))
            SetOnFire(target);
        if (Has(CoreType.Chain))
            ChainLightning(target, bullet.Damage * chainDamage, real);
        if (real && Has(CoreType.Vortex))
            Pull(point, target);

        // Fork: on its first target a (full-size) bullet splits in two that fly on past it.
        if (Has(CoreType.Fork) && !state.isShard && !state.forked)
        {
            state.forked = true;
            Vector2 direction = bullet.Body.linearVelocity.normalized;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 forkDirection = Quaternion.Euler(0f, 0f, side * forkAngle) * direction;
                Spawn(state.prefab, point, forkDirection, state.speed, bullet.Damage * forkDamage, forkLifetime,
                      state.shooter, true, bullet.LastHit, state.size * forkSize, state.random.Next(), state.lookAlike);
            }
        }

        if (state.piercesLeft > 0)
        {
            state.piercesLeft--;
            bullet.ScaleDamage(pierceDamage);
            state.target = null; // homing: look for the next one
            return true;
        }
        return false;
    }

    // Hit a wall or the floor. True = it keeps flying (Bounce first, then Drill).
    public bool OnHitWall(Projectile bullet, Vector2 point, Vector2 normal)
    {
        var state = (BulletState)bullet.CoreData;
        if (Has(CoreType.Explosive))
            CoreEffects.Blast(explosionEffect, explosionRadius, point + normal * 0.1f, explosionRadius, explosionDamage, Knockback(), bridge, !state.lookAlike, self);

        if (state.bouncesLeft > 0)
        {
            state.bouncesLeft--;
            bullet.ScaleDamage(bounceDamage);
            bullet.Body.position = point + normal * 0.1f; // back out of the wall
            bullet.SetVelocity(Vector2.Reflect(bullet.Body.linearVelocity, normal));
            state.waveOffset = 0f;
            return true;
        }
        if (state.drillsLeft > 0)
        {
            state.drillsLeft--;
            bullet.ScaleDamage(drillDamage);
            bullet.IgnoreCollider(bullet.LastHit); // flies on through this wall
            return true;
        }
        return false;
    }

    // Lightning jumps from the hit target to the nearest others (never this player). real = also hurt them.
    private void ChainLightning(HitTarget first, float damage, bool real)
    {
        chainHits.Clear();
        chainHits.Add(first);
        HitTarget from = first;
        for (int i = 0; i < chainJumps; i++)
        {
            HitTarget next = null;
            float nearest = chainRange;
            foreach (HitTarget target in HitTarget.Active)
            {
                if (target == self || target.IsDying || chainHits.Contains(target))
                    continue;
                float distance = Vector2.Distance(target.transform.position, from.transform.position);
                if (distance < nearest)
                {
                    next = target;
                    nearest = distance;
                }
            }
            if (next == null)
                return;

            Vector2 a = from.transform.position;
            Vector2 b = next.transform.position;
            CoreEffects.Zap(zapMaterial, zapColor, a, b, 0.08f, 0.1f);
            if (real)
                next.TakeDamage(damage, b, (a - b).normalized, 0.5f, bridge);
            chainHits.Add(next);
            from = next;
        }
    }

    private void Pull(Vector2 point, HitTarget hit)
    {
        foreach (HitTarget target in HitTarget.Active)
        {
            if (target == hit || target == self || target.IsDying)
                continue;
            Vector2 toPoint = point - (Vector2)target.transform.position;
            if (toPoint.magnitude < vortexRadius)
                target.Knock(toPoint.normalized * (vortexForce * Knockback()), bridge);
        }
    }

    private void SetOnFire(HitTarget target)
    {
        foreach (Burn burn in burns)
        {
            if (burn.target == target)
            {
                burn.timeLeft = igniteTime; // burns don't stack, they start over
                return;
            }
        }
        burns.Add(new Burn { target = target, timeLeft = igniteTime, tickTimer = igniteTick });
    }

    private void Update()
    {
        for (int i = burns.Count - 1; i >= 0; i--)
        {
            Burn burn = burns[i];
            burn.timeLeft -= Time.deltaTime;
            burn.tickTimer -= Time.deltaTime;
            bool gone = burn.target == null || !burn.target.isActiveAndEnabled || burn.target.IsDying;
            if (!gone && burn.tickTimer <= 0f)
            {
                burn.tickTimer += igniteTick;
                burn.target.TakeTickDamage(igniteDamage, bridge);
            }
            if (gone || burn.timeLeft <= 0f)
                burns.RemoveAt(i);
        }
    }

    // ---------- END ----------

    // The bullet is gone (it stopped in something, its time ran out, or it was caught). direction = where shards go.
    public void OnEnd(Projectile bullet, Vector2 position, Vector2 direction)
    {
        var state = (BulletState)bullet.CoreData;
        if (state.isShard)
            return; // shards never make anything more

        if (Has(CoreType.Shrapnel))
        {
            for (int i = 0; i < shrapnelCount; i++)
            {
                float t = shrapnelCount > 1 ? i / (shrapnelCount - 1f) : 0.5f;
                Vector2 shardDirection = Quaternion.Euler(0f, 0f, Mathf.Lerp(-0.5f, 0.5f, t) * shrapnelSpread) * direction;
                // Shards ignore whatever the bullet stopped in, so they don't all hit the same target again.
                Spawn(state.prefab, position, shardDirection, state.speed * shrapnelSpeed, state.damage * shrapnelDamage,
                      shrapnelLifetime, state.shooter, true, bullet.LastHit, state.size * shrapnelScale, state.random.Next(), state.lookAlike);
            }
        }

        // Mine: only where the bullet stopped on a wall or the floor.
        bool onWall = bullet.StoppedBy != null && bullet.StoppedBy.GetComponentInParent<HitTarget>() == null;
        if (Has(CoreType.Mine) && onWall && state.random.NextDouble() < mineChance)
        {
            mines.RemoveAll(mine => mine == null);
            if (mines.Count < maxMines)
                mines.Add(PlaceMine(position, state.lookAlike));
        }
    }

    private CoreMine PlaceMine(Vector2 position, bool lookAlike)
    {
        var go = new GameObject("Mine");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * mineScale;
        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = mineSprite;
        sprite.color = mineColor;
        sprite.sortingOrder = 5;
        var mine = go.AddComponent<CoreMine>();
        mine.Setup(this, self, mineArmTime, mineLifetime, mineTriggerRadius, lookAlike);
        return mine;
    }

    public void ClearMines()
    {
        foreach (CoreMine mine in mines)
            if (mine != null)
                Destroy(mine.gameObject);
        mines.Clear();
    }

    public void MineBlast(Vector2 position, bool lookAlike)
    {
        CoreEffects.Blast(explosionEffect, mineRadius, position, mineRadius, mineDamage, Knockback(), bridge, !lookAlike, self);
    }
}
