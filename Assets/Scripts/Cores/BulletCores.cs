using System.Collections.Generic;
using UnityEngine;

// What the bullet cores do, stage by stage in a bullet's life: SPAWN → FLY → HIT → END. Only CoreBridge calls this.
// It lives on the player (each player has its own cores). Targets are HitTargets: enemies in single player,
// the other players on LAN; a player's bullets and blasts never hurt that player.
// The rules that make any mix of bullet cores work together without special combos:
// 1. Every bullet (extra, back shot, nova, fork, shrapnel) carries all the bullet cores, but the small ones made
//    from another bullet (fork, shrapnel = "shards") never make anything more (no fork, shrapnel, split or crossfire).
// 2. FLY cores add up (homing keeps steering after a bounce, a mortar shell still speeds up, ...).
// 3. Every HIT core happens on every hit (a bullet that bounces or pierces explodes each time).
// 4. A bullet only disappears when no core keeps it alive (Bounce on walls, Pierce on targets).
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
        public bool forked;
        public HitTarget target;  // homing
        public float age;
        public float accelerateFactor = 1f;
        public bool returning;    // boomerang
        public PlayerWeapon.GunEffect gun; // fired by a special gun: rocket, grenade, rail, flame
        public bool lucky;        // a Lucky Shot critical
        public bool helix;        // one of a Helix pair...
        public float phase;       // ...and where it is in the twist (0 or π)
        public Vector2 lateral;   // Wave / Zigzag / Helix: the sideways part of the velocity added last step
        public bool split;        // Split: done
    }

    private class Reticle
    {
        public Transform mark;
        public float until;
    }

    private class Burn
    {
        public HitTarget target;
        public float timeLeft;
        public float tickTimer;
    }

    [Header("SPECIAL GUNS (weapon crates)")]
    [Tooltip("A rocket blows up where it hits: damage to everyone around (the one hit directly takes the rocket's own " +
             "damage instead), and a big push.")]
    [SerializeField] private float rocketBlastDamage = 30f;
    [SerializeField] private float rocketBlastRadius = 2.3f;
    [SerializeField] private float rocketKnockback = 3f;
    [SerializeField] private Color rocketColor = new Color(1f, 0.55f, 0.2f);

    [Tooltip("A grenade flies in an arc, bounces off walls and blows up after Grenade Fuse seconds (or on a target).")]
    [SerializeField] private float grenadeBlastDamage = 25f;
    [SerializeField] private float grenadeBlastRadius = 2f;
    [SerializeField] private float grenadeKnockback = 2.5f;
    [SerializeField] private float grenadeFuse = 1.1f;
    [SerializeField] private float grenadeGravity = 3f;
    [Range(0f, 1f)]
    [SerializeField] private float grenadeBounce = 0.6f;
    [SerializeField] private Color grenadeColor = new Color(0.45f, 0.9f, 0.35f);

    [Tooltip("A rail shot goes through every target (stopped by walls), drawn as a beam.")]
    [SerializeField] private Color railGlow = new Color(0.3f, 0.85f, 1f, 0.7f);
    [SerializeField] private Color railCore = Color.white;

    [Tooltip("Flames set on fire (like Ignite) and grow as they fly.")]
    [SerializeField] private Color flameColor = new Color(1f, 0.6f, 0.15f);

    [Tooltip("Push of each shotgun pellet and of a sniper round, times a normal bullet's.")]
    [SerializeField] private float shotgunKnockback = 2f;
    [SerializeField] private float sniperKnockback = 3.5f;
    [SerializeField] private Color sniperColor = new Color(1f, 0.95f, 0.6f);

    [SerializeField] private AudioClip gunBlastSound;
    [Range(0f, 1f)]
    [SerializeField] private float gunBlastVolume = 0.7f;

    [Header("SPAWN · Bullet Storm (ExtraBullet)")]
    [Tooltip("Angle between the three bullets of a shot (degrees).")]
    [SerializeField] private float extraBulletAngle = 12f;

    [Tooltip("Each extra bullet adds this share of a normal bullet's damage to the shot, and the total is split " +
             "between the bullets (1: every bullet deals full damage).")]
    [Range(0f, 1f)]
    [SerializeField] private float extraBulletDamage = 1f;

    [Header("SPAWN · Back Shot")]
    [Tooltip("Bullets fired backwards deal this share of the damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float backShotDamage = 0.25f;

    [Header("SPAWN · Lucky Shot")]
    [Range(0f, 1f)]
    [SerializeField] private float luckyChance = 0.15f;

    [Tooltip("Damage multiplier of a lucky (critical) bullet.")]
    [SerializeField] private float luckyDamage = 2.5f;

    [SerializeField] private float luckySize = 1.4f;
    [SerializeField] private Color luckyColor = new Color(1f, 0.42f, 0.21f);

    [Header("SPAWN · Nova")]
    [Tooltip("Every this many shots, a ring of bullets bursts out too, with a shock wave.")]
    [SerializeField] private int novaEvery = 4;

    [SerializeField] private int novaCount = 12;

    [Range(0f, 1f)]
    [SerializeField] private float novaDamage = 0.6f;
    [SerializeField] private Color novaColor = new Color(0.5f, 0.95f, 1f);

    [Header("SPAWN · Fan Shot")]
    [Tooltip("Two more bullets this far to each side (degrees, past a Bullet Storm's fan), at this share of the damage.")]
    [SerializeField] private float fanAngle = 22f;
    [Range(0f, 1f)]
    [SerializeField] private float fanDamage = 0.25f;
    [SerializeField] private Color fanColor = new Color(1f, 0.9f, 0.45f);

    [Header("SPAWN · Wings")]
    [Tooltip("Two small bullets straight out to the sides (at right angles to the aim), at this share of the damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float wingsDamage = 0.25f;
    [SerializeField] private float wingsSize = 0.75f;
    [SerializeField] private Color wingsColor = new Color(0.7f, 1f, 0.75f);

    [Header("SPAWN · Double Helix")]
    [Tooltip("Every bullet becomes two at this share of the damage each, twisting around its line.")]
    [Range(0f, 1f)]
    [SerializeField] private float helixDamage = 0.5f;
    [SerializeField] private float helixAmplitude = 0.4f; // units to each side
    [SerializeField] private float helixFrequency = 3f;   // twists per second
    [SerializeField] private Color[] helixColors = { new Color(1f, 0.5f, 0.85f), new Color(0.45f, 0.9f, 1f) };

    [Header("FLY · Wave")]
    [SerializeField] private float waveAmplitude = 0.6f; // units to each side
    [SerializeField] private float waveFrequency = 2.5f; // waves per second

    [Header("FLY · Zigzag")]
    [Tooltip("Seconds of each leg, and how steep it is (sideways speed for each unit of forward speed).")]
    [SerializeField] private float zigzagLeg = 0.09f;
    [SerializeField] private float zigzagSlope = 0.8f;

    [Header("FLY · Split")]
    [Tooltip("After this many seconds a bullet splits: two new ones this far to each side (degrees) at this share of its damage.")]
    [SerializeField] private float splitTime = 0.18f;
    [SerializeField] private float splitAngle = 20f;
    [Range(0f, 1f)]
    [SerializeField] private float splitDamage = 0.25f;
    [SerializeField] private float splitLifetime = 0.6f;
    [SerializeField] private Color splitColor = new Color(0.6f, 0.85f, 1f);

    [Header("HIT · Crossfire")]
    [Tooltip("Every hit bursts into 4 rays (up, down, left, right) at this share of the damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float crossDamage = 0.25f;
    [SerializeField] private float crossLifetime = 0.3f;
    [SerializeField] private Color crossColor = new Color(1f, 0.45f, 0.4f);

    [Header("Draw the Sword · sword wave")]
    [Tooltip("The crescent a slash sends flying (a bullet: it carries the bullet cores).")]
    [SerializeField] private Sprite swordWaveSprite;
    [SerializeField] private float swordWaveSize = 1.6f; // units across
    [SerializeField] private Color swordWaveColor = new Color(0.75f, 0.95f, 1f);

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
    [SerializeField] private float homingDamage = 1f;

    [Tooltip("Homing bullets glow this color; their target gets a red lock-on mark (Reticle Sprite) with a beep.")]
    [SerializeField] private Color homingColor = new Color(1f, 0.35f, 0.45f);
    [SerializeField] private Sprite reticleSprite;
    [SerializeField] private AudioClip lockSound;

    [Tooltip("Played with the \"CRIT!\" of a Lucky Shot.")]
    [SerializeField] private AudioClip critSound;

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

    [Header("FLY · Mortar")]
    [Tooltip("Shells leave this many degrees above the aim (toward the sky)...")]
    [SerializeField] private float mortarAngle = 30f;

    [Tooltip("...at this share of the bullet speed (a lob, so it lands inside the map)...")]
    [SerializeField] private float mortarSpeed = 0.65f;

    [Tooltip("...and fall with this gravity scale, in an arc.")]
    [SerializeField] private float mortarGravity = 4f;

    [Tooltip("Where a shell lands (wall, floor or target) it blows up: damage to everyone within the radius but " +
             "the one hit directly, and a push.")]
    [SerializeField] private float mortarBlastDamage = 14f;
    [SerializeField] private float mortarBlastRadius = 1.6f;
    [SerializeField] private float mortarKnockback = 1.5f;

    [Header("SPAWN · Echo")]
    [Tooltip("Every shot fires again from the same spot, the same way, this many seconds later.")]
    [SerializeField] private float echoDelay = 0.3f;

    [Range(0f, 1f)]
    [SerializeField] private float echoDamage = 0.6f;

    [Tooltip("Echo bullets are tinted like this, so they are told apart.")]
    [SerializeField] private Color echoColor = new Color(0.55f, 0.85f, 1f);

    [Header("FLY · Accelerate")]
    [Tooltip("Bullets leave at this share of their speed and reach Accelerate Max Speed after Accelerate Time.")]
    [SerializeField] private float accelerateStartSpeed = 0.45f;
    [SerializeField] private float accelerateMaxSpeed = 2.2f;
    [SerializeField] private float accelerateTime = 0.6f;

    [Tooltip("Damage multiplier when slowest (start) and fastest.")]
    [SerializeField] private Vector2 accelerateDamage = new Vector2(0.8f, 1.7f);

    [Header("FLY · Boomerang")]
    [Tooltip("Seconds of flight before the bullet turns back toward the player.")]
    [SerializeField] private float boomerangReturnTime = 0.55f;

    [Tooltip("How fast a returning bullet turns toward the player (degrees/sec).")]
    [SerializeField] private float boomerangTurnSpeed = 900f;

    [Tooltip("The player catches a returning bullet this close (units).")]
    [SerializeField] private float boomerangCatchRadius = 0.6f;

    [Header("HIT · Explosive")]
    [Tooltip("Targets within this distance of the blast take damage (units).")]
    [SerializeField] private float explosionRadius = 1.2f;

    [Tooltip("Blast damage to each target in range, except the one hit directly (it only takes the bullet's damage).")]
    [SerializeField] private float explosionDamage = 6f;

    [Tooltip("Knockback multiplier of the blast (it throws targets away).")]
    [SerializeField] private float explosionKnockback = 1.5f;

    [Tooltip("The fire rate is multiplied by this while Explosive is owned (explosions cost fire rate).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float explosiveFireRate = 0.85f;

    [Tooltip("Spawned at every blast (Explosive...). It should destroy itself (Stop Action = Destroy).")]
    [SerializeField] private ParticleSystem explosionEffect;

    [Header("HIT · Hammer")]
    [Tooltip("Knockback of bullet hits, blasts and pulls is multiplied by this.")]
    [SerializeField] private float hammerKnockback = 2.5f;

    [Header("HIT · Fork")]
    [Tooltip("On its first target the bullet splits into two that fly on at ± this angle (degrees).")]
    [SerializeField] private float forkAngle = 25f;

    [Tooltip("Each fork deals this share of the bullet's damage at that moment.")]
    [Range(0f, 1f)]
    [SerializeField] private float forkDamage = 0.25f;

    [SerializeField] private float forkLifetime = 0.5f;
    [SerializeField] private float forkSize = 0.7f;

    [Header("HIT · Chain")]
    [Tooltip("Targets the lightning jumps to, one after another.")]
    [SerializeField] private int chainJumps = 3;

    [Tooltip("Longest jump (units).")]
    [SerializeField] private float chainRange = 4.5f;

    [Tooltip("Each jump deals this share of the bullet's damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float chainDamage = 0.7f;

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

    [Header("HIT · Sticky Bomb")]
    [Tooltip("A hit sticks a bomb on the target (one at a time per target) that blows up after this many seconds.")]
    [SerializeField] private float stickyDelay = 1f;
    [SerializeField] private float stickyDamage = 15f;
    [SerializeField] private float stickyRadius = 1.6f;

    [Tooltip("Knockback multiplier of the bomb's blast.")]
    [SerializeField] private float stickyKnockback = 3f;
    [Tooltip("The bomb (a small round one).")]
    [UnityEngine.Serialization.FormerlySerializedAs("mineSprite")]
    [SerializeField] private Sprite bombSprite;
    [SerializeField] private Color stickyColor = new Color(1f, 0.35f, 0.2f);

    [Header("HIT · Thunder")]
    [Tooltip("Every hit calls lightning down on the target from the sky this many seconds later...")]
    [SerializeField] private float thunderDelay = 0.12f;

    [Tooltip("...dealing this share of the bullet's damage, and half of it to everyone else within Thunder Splash.")]
    [Range(0f, 2f)]
    [SerializeField] private float thunderDamage = 1f;
    [SerializeField] private float thunderSplash = 1.4f;

    [SerializeField] private Color thunderGlow = new Color(0.6f, 0.75f, 1f, 0.6f);
    [SerializeField] private Color thunderCore = new Color(1f, 1f, 0.85f);
    [SerializeField] private AudioClip thunderSound;
    [Range(0f, 1f)]
    [SerializeField] private float thunderVolume = 0.45f;

    [Header("END · Black Hole")]
    [Tooltip("A bullet stopping on a wall or the floor leaves a black hole, at most once every this many seconds.")]
    [SerializeField] private float blackHoleCooldown = 2f;

    [SerializeField] private int maxBlackHoles = 2;
    [SerializeField] private float blackHoleTime = 1.4f;
    [SerializeField] private float blackHoleRadius = 4.5f;

    [Tooltip("Pull toward the middle every 0.1 s (impulse; with mass 1 = speed).")]
    [SerializeField] private float blackHolePull = 4f;

    [Tooltip("Blast when it collapses, with a purple shock wave.")]
    [SerializeField] private float blackHoleDamage = 25f;
    [SerializeField] private float blackHoleBlastRadius = 2.8f;
    [SerializeField] private Color blackHoleColor = new Color(0.75f, 0.4f, 1f);
    [SerializeField] private Sprite blackHoleSprite;

    [Tooltip("The whirling purple spiral around the black hole (pixel art).")]
    [SerializeField] private Sprite blackHoleSwirlSprite;

    [Header("END · Shrapnel")]
    [SerializeField] private int shrapnelCount = 3;

    [Tooltip("The shards fan out over this angle around the way the bullet was going (degrees).")]
    [SerializeField] private float shrapnelSpread = 90f;

    [Tooltip("Each shard deals this share of the bullet's damage when it was fired.")]
    [Range(0f, 1f)]
    [SerializeField] private float shrapnelDamage = 0.25f;

    [Tooltip("Shard speed as a share of the bullet's speed.")]
    [SerializeField] private float shrapnelSpeed = 0.8f;

    [Tooltip("Seconds a shard flies.")]
    [SerializeField] private float shrapnelLifetime = 0.35f;

    [Tooltip("Shards are this much smaller than a bullet.")]
    [SerializeField] private float shrapnelScale = 0.6f;

    private CoreInventory inventory;
    private CoreBridge bridge;
    private HitTarget self; // this player (never hurt by its own bullets)
    private int shotCount;
    private readonly List<HitTarget> chainHits = new List<HitTarget>();
    private readonly List<Burn> burns = new List<Burn>();
    private readonly List<CoreBlackHole> blackHoles = new List<CoreBlackHole>();
    private readonly Dictionary<HitTarget, CoreStickyBomb> stickyBombs = new Dictionary<HitTarget, CoreStickyBomb>();
    private PlayerWeapon.GunEffect firingGun; // the special gun of the shot being spawned (FireShot)
    private float helixPhase = -1f;           // the Helix strand being spawned (-1 = none)
    private PlayerNetwork owner;
    private readonly RaycastHit2D[] railHits = new RaycastHit2D[8];
    private readonly Dictionary<HitTarget, Reticle> reticles = new Dictionary<HitTarget, Reticle>();
    private readonly List<HitTarget> reticleGone = new List<HitTarget>();
    private float nextBlackHole; // Black Hole: not again before this time

    public CoreBridge Bridge => bridge;

    private void Awake()
    {
        inventory = GetComponent<CoreInventory>();
        bridge = GetComponent<CoreBridge>();
        self = GetComponent<HitTarget>();
        owner = GetComponent<PlayerNetwork>();
    }

    // This machine's own player's bullets shake this screen (Hammer...).
    private void ShakeMine(float strength)
    {
        if (owner != null && !owner.IsMine)
            return;
        CameraShake shake = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
        if (shake != null)
            shake.Shake(strength, 0.15f);
    }

    // The shooter is off the ground (Air Gunner's bullets glow): nothing solid just under its feet.
    private bool InAir(Rigidbody2D shooter)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer) & ~(1 << gameObject.layer));
        return Physics2D.Raycast(shooter.position, Vector2.down, filter, railHits, 1.1f) == 0;
    }

    private bool Has(CoreType type) => inventory.Has(type);

    public float FireRateMultiplier() => Has(CoreType.Explosive) ? explosiveFireRate : 1f;

    public float Knockback() => Has(CoreType.Hammer) ? hammerKnockback : 1f;

    // The push of this bullet's hit: the cores' (Hammer) times its special gun's (shotgun pellets, sniper rounds).
    public float Knockback(Projectile bullet)
    {
        var state = bullet.CoreData as BulletState;
        float gun = state == null ? 1f
                  : state.gun == PlayerWeapon.GunEffect.Shotgun ? shotgunKnockback
                  : state.gun == PlayerWeapon.GunEffect.Sniper ? sniperKnockback
                  : 1f;
        return Knockback() * gun;
    }

    // ---------- SPAWN ----------

    // One shot of the gun: one bullet (two with +1 Bullet), mirrored backwards with Back Shot,
    // plus a ring every few shots with Nova.
    // seed = the shot's random numbers; lookAlike = only show the bullets (never hurt).
    // gun = a special gun's bullet effect (its bullets only, not the Nova ring).
    public void FireShot(Projectile prefab, Vector2 position, Quaternion aim, float speed, float damage, float lifetime,
                         Rigidbody2D shooter, int seed, bool lookAlike, PlayerWeapon.GunEffect gun = PlayerWeapon.GunEffect.None)
    {
        const float size = 1f; // the normal size (Giant, Lucky Shot... change it per bullet)
        var random = new System.Random(seed);
        firingGun = gun;
        FireBullets(prefab, position, aim, speed, damage, lifetime, shooter, size, random, lookAlike, null);
        firingGun = PlayerWeapon.GunEffect.None;

        // Echo: the same bullets again a moment later, from the same spot (not the Nova ring).
        if (Has(CoreType.Echo))
            StartCoroutine(Echo(prefab, position, aim, speed, damage * echoDamage, lifetime, shooter, size, random.Next(), lookAlike, gun));

        shotCount++;
        if (Has(CoreType.Nova) && shotCount % novaEvery == 0)
        {
            CoreEffects.Ring(zapMaterial, novaColor, position, 3f, 0.3f, 0.35f); // the burst's shock wave
            for (int i = 0; i < novaCount; i++)
            {
                Quaternion rotation = aim * Quaternion.Euler(0f, 0f, (i + 0.5f) * 360f / novaCount);
                Spawn(prefab, position, rotation * Vector3.right, speed, damage * novaDamage, lifetime, shooter, false, null, size, random.Next(), lookAlike);
            }
        }
    }

    // The shot's own bullets: one (two with +1 Bullet), each mirrored backwards with Back Shot.
    private void FireBullets(Projectile prefab, Vector2 position, Quaternion aim, float speed, float damage, float lifetime,
                             Rigidbody2D shooter, float size, System.Random random, bool lookAlike, Color? tint)
    {
        int count = Has(CoreType.ExtraBullet) ? 3 : 1; // Bullet Storm: a fan of three
        float damagePerBullet = damage * (1f + extraBulletDamage * (count - 1)) / count;
        for (int i = 0; i < count; i++)
        {
            Quaternion rotation = aim * Quaternion.Euler(0f, 0f, (i - (count - 1) * 0.5f) * extraBulletAngle);
            ShotBullet(prefab, position, rotation * Vector3.right, speed, damagePerBullet, lifetime, shooter, size, random, lookAlike, tint);
            if (Has(CoreType.BackShot))
                ShotBullet(prefab, position, rotation * Vector3.left, speed, damagePerBullet * backShotDamage, lifetime, shooter, size, random, lookAlike, tint);
        }

        // Fan Shot: one more on each side, past the fan; Wings: a small one straight out to each side.
        for (int side = -1; side <= 1; side += 2)
        {
            if (Has(CoreType.Fan))
            {
                Quaternion rotation = aim * Quaternion.Euler(0f, 0f, side * (fanAngle + (count - 1) * 0.5f * extraBulletAngle));
                ShotBullet(prefab, position, rotation * Vector3.right, speed, damage * fanDamage, lifetime, shooter, size, random, lookAlike, tint ?? fanColor);
            }
            if (Has(CoreType.Wings))
                ShotBullet(prefab, position, aim * Quaternion.Euler(0f, 0f, side * 90f) * Vector3.right, speed, damage * wingsDamage, lifetime,
                           shooter, size * wingsSize, random, lookAlike, tint ?? wingsColor);
        }
    }

    // One bullet of a shot, or with Double Helix two twisting around its line (pink and blue).
    private void ShotBullet(Projectile prefab, Vector2 position, Vector2 direction, float speed, float damage, float lifetime,
                            Rigidbody2D shooter, float size, System.Random random, bool lookAlike, Color? tint)
    {
        if (!Has(CoreType.Helix))
        {
            Spawn(prefab, position, direction, speed, damage, lifetime, shooter, false, null, size, random.Next(), lookAlike, tint);
            return;
        }
        for (int strand = 0; strand < 2; strand++)
        {
            helixPhase = strand * Mathf.PI;
            Spawn(prefab, position, direction, speed, damage * helixDamage, lifetime, shooter, false, null, size, random.Next(), lookAlike,
                  tint ?? helixColors[strand % helixColors.Length]);
        }
        helixPhase = -1f;
    }

    private System.Collections.IEnumerator Echo(Projectile prefab, Vector2 position, Quaternion aim, float speed, float damage,
                                                float lifetime, Rigidbody2D shooter, float size, int seed, bool lookAlike,
                                                PlayerWeapon.GunEffect gun)
    {
        yield return new WaitForSeconds(echoDelay);
        if (shooter == null)
            yield break;
        firingGun = gun;
        FireBullets(prefab, position, aim, speed, damage, lifetime, shooter, size, new System.Random(seed), lookAlike, echoColor);
        firingGun = PlayerWeapon.GunEffect.None;
    }

    private void Spawn(Projectile prefab, Vector2 position, Vector2 direction, float speed, float damage, float lifetime,
                       Rigidbody2D shooter, bool isShard, Collider2D ignore, float size, int seed, bool lookAlike, Color? tint = null)
    {
        var random = new System.Random(seed);
        float baseDamage = damage;
        if (Has(CoreType.Giant))
        {
            size *= giantSize;
            damage *= giantDamage;
            speed *= giantSpeed;
        }
        bool lucky = false;
        if (Has(CoreType.LuckyShot) && !isShard && random.NextDouble() < luckyChance)
        {
            size *= luckySize;
            damage *= luckyDamage;
            tint = luckyColor;
            lucky = true;
        }
        if (Has(CoreType.Homing))
        {
            damage *= homingDamage;
            tint ??= homingColor;
        }
        if (Has(CoreType.Pierce))
            tint ??= new Color(1f, 1f, 0.75f); // a hard bright core
        if (Has(CoreType.AirGunner) && shooter != null && InAir(shooter))
            tint ??= new Color(0.45f, 0.95f, 1f); // fired in the air: glowing cyan
        if (Has(CoreType.Mortar)) // tilted toward the sky, whichever way it was aimed, and lobbed softly
        {
            direction = Quaternion.Euler(0f, 0f, direction.x >= 0f ? mortarAngle : -mortarAngle) * direction;
            if (!isShard) // shards already start from the shell's slower speed
                speed *= mortarSpeed;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
        float launchSpeed = Has(CoreType.Accelerate) ? speed * accelerateStartSpeed : speed;
        float launchDamage = Has(CoreType.Accelerate) ? damage * accelerateDamage.x : damage;
        bullet.Launch(direction, launchSpeed, launchDamage, lifetime, shooter);
        if (lookAlike)
            bullet.MakeLookAlike();
        if (size != 1f)
            bullet.SetSize(size);
        if (tint.HasValue)
            bullet.SetTint(tint.Value);
        bullet.IgnoreCollider(ignore);
        if (Has(CoreType.Mortar))
            bullet.Body.gravityScale = mortarGravity;

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
            accelerateFactor = Has(CoreType.Accelerate) ? accelerateDamage.x : 1f,
            gun = isShard ? PlayerWeapon.GunEffect.None : firingGun,
            lucky = lucky,
            helix = !isShard && helixPhase >= 0f,
            phase = Mathf.Max(0f, helixPhase),
        };
        bullet.AttachCores(bridge, state);
        if (state.gun != PlayerWeapon.GunEffect.None)
            LookLikeGun(bullet, state.gun, position, direction);
    }

    // A special gun's bullet: a rocket is big and orange, a grenade green and falls in an arc, a rail shot draws its
    // beam up to the first wall at once, a flame is orange and grows.
    private void LookLikeGun(Projectile bullet, PlayerWeapon.GunEffect gun, Vector2 position, Vector2 direction)
    {
        switch (gun)
        {
            case PlayerWeapon.GunEffect.Rocket:
                bullet.SetSize(2f);
                bullet.SetTint(rocketColor);
                break;
            case PlayerWeapon.GunEffect.Grenade:
                bullet.SetSize(1.6f);
                bullet.SetTint(grenadeColor);
                bullet.Body.gravityScale = grenadeGravity;
                break;
            case PlayerWeapon.GunEffect.Rail:
                bullet.SetTint(railCore);
                CoreEffects.Beam(zapMaterial, railGlow, railCore, position, RailEnd(position, direction), 0.45f, 0.18f);
                break;
            case PlayerWeapon.GunEffect.Flame:
                bullet.SetSize(1.4f);
                bullet.SetTint(flameColor);
                break;
            case PlayerWeapon.GunEffect.Sniper: // a bright round with a thin tracer up to the first wall
                bullet.SetSize(1.3f);
                bullet.SetTint(sniperColor);
                CoreEffects.Beam(zapMaterial, new Color(sniperColor.r, sniperColor.g, sniperColor.b, 0.35f), Color.white,
                                 position, RailEnd(position, direction), 0.12f, 0.12f);
                break;
            case PlayerWeapon.GunEffect.Shotgun:
                bullet.SetTint(new Color(1f, 0.85f, 0.55f));
                break;
            case PlayerWeapon.GunEffect.SwordWave: // a glowing crescent; the hit area grows with it
                bullet.SetSize(4f);
                bullet.SetTint(swordWaveColor);
                bullet.GetComponent<SpriteRenderer>().enabled = false;
                var crescent = new GameObject("SwordWave").AddComponent<SpriteRenderer>();
                crescent.transform.SetParent(bullet.transform, false);
                crescent.transform.localScale = Vector3.one * swordWaveSize / (swordWaveSprite != null ? swordWaveSprite.bounds.size.x : 1f) /
                                                bullet.transform.localScale.x;
                crescent.sprite = swordWaveSprite;
                crescent.color = swordWaveColor;
                crescent.sortingOrder = 8;
                break;
        }
    }

    // Where a rail shot's beam ends: the first wall (not a target, not a trigger) along its way.
    private Vector2 RailEnd(Vector2 position, Vector2 direction)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer) & ~(1 << gameObject.layer));
        int count = Physics2D.Raycast(position, direction, filter, railHits, 60f);
        float nearest = 60f;
        for (int i = 0; i < count; i++)
            if (railHits[i].collider.GetComponentInParent<HitTarget>() == null && railHits[i].distance < nearest)
                nearest = railHits[i].distance;
        return position + direction * nearest;
    }

    // A rocket or grenade going off (everybody sees it; only the host's real one hurts).
    private void GunBlast(Vector2 point, BulletState state, HitTarget direct)
    {
        bool rocket = state.gun == PlayerWeapon.GunEffect.Rocket;
        float radius = rocket ? rocketBlastRadius : grenadeBlastRadius;
        CoreEffects.Blast(explosionEffect, radius * 1.2f, point, radius, rocket ? rocketBlastDamage : grenadeBlastDamage,
                          Knockback() * (rocket ? rocketKnockback : grenadeKnockback), bridge, !state.lookAlike, direct, self);
        CoreEffects.Ring(zapMaterial, rocket ? rocketColor : grenadeColor, point, radius * 1.3f, 0.3f, 0.35f);
        GameAudio.PlayAt(gunBlastSound, point, gunBlastVolume, 0.08f);
        CRTScreen.Blur(point, rocket ? 0.9f : 0.7f);
        CameraShake shake = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
        if (shake != null)
            shake.Shake(rocket ? 0.35f : 0.25f, 0.25f);
    }

    // ---------- FLY ----------

    public void OnFly(Projectile bullet)
    {
        var state = (BulletState)bullet.CoreData;
        Rigidbody2D rb = bullet.Body;
        float dt = Time.fixedDeltaTime;
        state.age += dt;

        // Wave / Zigzag / Helix sway sideways on top of the flight: last step's sway comes off first, so the other
        // cores steer the real flight, and the new sway goes on at the end (Sway).
        bool sways = !state.isShard && state.gun != PlayerWeapon.GunEffect.Rail &&
                     (Has(CoreType.Wave) || Has(CoreType.Zigzag) || state.helix);
        if (sways && state.lateral != Vector2.zero)
        {
            bullet.SetVelocity(rb.linearVelocity - state.lateral);
            state.lateral = Vector2.zero;
        }

        // Special guns: a grenade's fuse; a rocket turns to face where it flies.
        if (state.gun == PlayerWeapon.GunEffect.Grenade)
        {
            bullet.SetVelocity(rb.linearVelocity);
            if (state.age >= grenadeFuse)
            {
                GunBlast(rb.position, state, null);
                bullet.Shatter();
                return;
            }
        }
        if (state.gun == PlayerWeapon.GunEffect.Flame)
            bullet.SizeTo(state.size * (1.4f + state.age * 4f)); // the flame swells as it flies

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

        // Mortar: gravity bends the path; keep the shell facing where it flies.
        if (Has(CoreType.Mortar))
            bullet.SetVelocity(rb.linearVelocity);

        // Accelerate: from slow to very fast over Accelerate Time, damage growing with it.
        if (Has(CoreType.Accelerate) && !state.returning)
        {
            float progress = Mathf.Clamp01(state.age / accelerateTime);
            bullet.SetVelocity(rb.linearVelocity.normalized * state.speed * Mathf.Lerp(accelerateStartSpeed, accelerateMaxSpeed, progress));
            float factor = Mathf.Lerp(accelerateDamage.x, accelerateDamage.y, progress);
            bullet.ScaleDamage(factor / state.accelerateFactor);
            state.accelerateFactor = factor;
        }

        // Split: after a moment, two more fly off to the sides from here.
        if (Has(CoreType.Split) && !state.isShard && !state.split && state.age >= splitTime && state.gun != PlayerWeapon.GunEffect.Rail)
        {
            state.split = true;
            Vector2 forward = rb.linearVelocity.normalized;
            for (int side = -1; side <= 1; side += 2)
                Spawn(state.prefab, rb.position, Quaternion.Euler(0f, 0f, side * splitAngle) * forward, state.speed, bullet.Damage * splitDamage,
                      splitLifetime, state.shooter, true, null, state.size * 0.85f, state.random.Next(), state.lookAlike, splitColor);
            CoreEffects.Ring(zapMaterial, splitColor, rb.position, 0.9f, 0.15f, 0.15f);
        }

        if (sways)
            Sway(bullet, state);
    }

    // The sideways sway: Wave a smooth wave, Helix a tighter one (its two bullets opposite), Zigzag straight legs
    // left and right. Each starts on the line and swings to both sides of it.
    private void Sway(Projectile bullet, BulletState state)
    {
        Vector2 forward = bullet.Body.linearVelocity;
        float speed = forward.magnitude;
        if (speed < 0.01f)
            return;
        float t = state.age;
        float sideways = 0f; // speed to the left of the flight
        if (Has(CoreType.Wave))
        {
            float w = waveFrequency * 2f * Mathf.PI;
            sideways += waveAmplitude * w * Mathf.Cos(w * t + state.phase);
        }
        if (state.helix)
        {
            float w = helixFrequency * 2f * Mathf.PI;
            sideways += helixAmplitude * w * Mathf.Cos(w * t + state.phase);
        }
        if (Has(CoreType.Zigzag))
        {
            int leg = Mathf.FloorToInt(t / zigzagLeg + 0.5f); // half a leg first: centered on the line
            sideways += speed * zigzagSlope * (leg % 2 == 0 ? 1f : -1f) * Mathf.Cos(state.phase);
        }
        state.lateral = new Vector2(-forward.y, forward.x) / speed * sideways;
        bullet.SetVelocity(forward + state.lateral);
    }

    // Homing: turns the flight direction toward the target a bit every physics step, keeping the speed.
    private void Steer(Projectile bullet, BulletState state)
    {
        Rigidbody2D rb = bullet.Body;
        if (state.target == null || !state.target.isActiveAndEnabled || state.target.IsDying)
            state.target = FindTarget(rb);
        if (state.target == null)
            return;
        MarkTarget(state.target);

        // A fast bullet can't turn tight enough to hit a close target and would circle around it,
        // so it turns faster the closer the target is.
        Vector2 toTarget = (Vector2)state.target.transform.position - rb.position;
        float closeness = 1f - Mathf.Clamp01(toTarget.magnitude / homingRange); // 0 = at the edge of the range, 1 = right there
        float maxTurn = homingTurnSpeed * (1f + homingCloseBoost * closeness) * Mathf.Deg2Rad * Time.fixedDeltaTime;
        Vector2 direction = Vector3.RotateTowards(rb.linearVelocity.normalized, toTarget.normalized, maxTurn, 0f);
        bullet.SetVelocity(direction * rb.linearVelocity.magnitude);
    }

    // Homing: a red lock-on mark on the target while bullets chase it (gone a moment after the last one).
    private void MarkTarget(HitTarget target)
    {
        if (!reticles.TryGetValue(target, out Reticle reticle) || reticle.mark == null)
        {
            var mark = new GameObject("LockOn").AddComponent<SpriteRenderer>();
            mark.sprite = reticleSprite;
            mark.color = homingColor;
            mark.sortingOrder = 30;
            reticle = new Reticle { mark = mark.transform };
            reticles[target] = reticle;
            GameAudio.PlayAt(lockSound, target.transform.position, 0.35f, 0.05f);
        }
        reticle.until = Time.time + 0.2f;
    }

    // The lock-on marks follow their targets, turning in steps and pulsing; old ones go.
    private void UpdateReticles()
    {
        if (reticles.Count == 0)
            return;
        reticleGone.Clear();
        foreach (var pair in reticles)
        {
            if (pair.Key == null || pair.Key.IsDying || Time.time > pair.Value.until || pair.Value.mark == null)
            {
                reticleGone.Add(pair.Key);
                continue;
            }
            Transform mark = pair.Value.mark;
            mark.position = pair.Key.transform.position + Vector3.up * 0.3f;
            mark.rotation = Quaternion.Euler(0f, 0f, Mathf.Floor(Time.time * 8f) * 15f);
            mark.localScale = Vector3.one * (Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? 1.6f : 1.4f);
        }
        foreach (HitTarget gone in reticleGone)
        {
            if (reticles[gone].mark != null)
                Destroy(reticles[gone].mark.gameObject);
            reticles.Remove(gone);
        }
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

    // What a hit looks like with the cores (every machine, look-alikes too): a critical, a finishing blow, a hammer
    // blow, frost, flames, a little vortex.
    private void ShowHit(BulletState state, HitTarget target, Vector2 point, Vector2 normal)
    {
        Vector2 above = (Vector2)target.transform.position + Vector2.up * 1.3f;
        if (state.lucky)
        {
            CoreEffects.Pop(Lang.T("CRIT!", "CHÍ MẠNG!"), above, new Color(1f, 0.85f, 0.25f), 1.1f);
            CoreEffects.Ring(zapMaterial, new Color(1f, 0.85f, 0.25f), point, 1.3f, 0.22f, 0.25f);
            CoreEffects.Burst(point, new Color(1f, 0.9f, 0.4f), 8, 8f);
            GameAudio.PlayAt(critSound, point, 0.5f, 0.05f);
        }
        if (Has(CoreType.Reaper) && !target.IsDying && target.HealthFraction <= reaperThreshold)
        {
            CoreEffects.Pop(Lang.T("FINISHED!", "KẾT LIỄU!"), above, new Color(1f, 0.25f, 0.3f), 1.15f);
            CoreEffects.Ring(zapMaterial, new Color(1f, 0.2f, 0.25f), target.transform.position, 2f, 0.35f, 0.35f);
            CoreEffects.Burst(target.transform.position, new Color(0.8f, 0.05f, 0.1f), 14, 9f);
            CRTScreen.Blur(point, 0.6f);
        }
        if (Has(CoreType.Hammer))
        {
            CoreEffects.Ring(zapMaterial, new Color(1f, 0.85f, 0.6f), point, 1.7f, 0.35f, 0.22f);
            CoreEffects.Burst(point, Color.white, 6, 9f, -normal);
            ShakeMine(0.22f);
        }
        if (Has(CoreType.Chill))
        {
            StatusMark.Put(target.gameObject, StatusMark.Kind.Frost, chillTime, CoreEffects.Speck);
            CoreEffects.Burst(point, new Color(0.7f, 0.95f, 1f), 7, 5f);
        }
        if (Has(CoreType.Ignite))
            StatusMark.Put(target.gameObject, StatusMark.Kind.Flame, igniteTime, CoreEffects.Speck);
        if (Has(CoreType.Vortex))
            FxSpeck.Spawn(blackHoleSwirlSprite, new Color(0.5f, 0.95f, 1f, 0.85f), point, Vector2.zero, 0f, 0.45f, 0.9f, -900f, null, -1.6f);
    }

    // The bullet has hit a target (a real bullet has already damaged it). True = it keeps flying (Pierce).
    public bool OnHitTarget(Projectile bullet, HitTarget target, Vector2 point, Vector2 normal)
    {
        var state = (BulletState)bullet.CoreData;
        bool real = !state.lookAlike;

        // Special guns: a rocket or grenade blows up on the target; flames set it on fire; a rail shot goes on through.
        if (state.gun == PlayerWeapon.GunEffect.Rocket || state.gun == PlayerWeapon.GunEffect.Grenade)
        {
            GunBlast(point, state, target);
            return false;
        }
        if (state.gun == PlayerWeapon.GunEffect.Flame)
        {
            if (real)
                SetOnFire(target);
            StatusMark.Put(target.gameObject, StatusMark.Kind.Flame, igniteTime, CoreEffects.Speck);
        }
        bool railThrough = state.gun == PlayerWeapon.GunEffect.Rail;

        ShowHit(state, target, point, normal);
        if (real && Has(CoreType.Reaper) && !target.IsDying && target.HealthFraction <= reaperThreshold)
            target.TakeTickDamage(target.HealthFraction * 10000f, bridge); // finished off
        if (Has(CoreType.Explosive))
            CoreEffects.Blast(explosionEffect, explosionRadius, point + normal * 0.1f, explosionRadius, explosionDamage, Knockback() * explosionKnockback, bridge, real, target, self);
        if (Has(CoreType.Mortar))
            CoreEffects.Blast(explosionEffect, mortarBlastRadius, point, mortarBlastRadius, mortarBlastDamage, Knockback() * mortarKnockback, bridge, real, target, self);
        if (Has(CoreType.StickyBomb))
            StickBomb(target, point, state.lookAlike);
        if (Has(CoreType.Thunder))
            StartCoroutine(Thunder(target, bullet.Damage * thunderDamage, real));
        if (real && Has(CoreType.Chill))
            target.Slow(chillSpeed, chillTime);
        if (real && Has(CoreType.Ignite))
            SetOnFire(target);
        if (Has(CoreType.Chain))
            ChainLightning(target, bullet.Damage * chainDamage, real);
        if (real && Has(CoreType.Vortex))
            Pull(point, target);
        CrossBurst(state, point, Vector2.zero, bullet.LastHit);

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
            CoreEffects.Ring(zapMaterial, new Color(1f, 1f, 0.75f), point, 0.9f, 0.2f, 0.18f); // punches through
            CoreEffects.Burst(point, new Color(1f, 1f, 0.75f), 5, 8f, bullet.Body.linearVelocity);
            return true;
        }
        return railThrough;
    }

    // Hit a wall or the floor. True = it keeps flying (Bounce).
    public bool OnHitWall(Projectile bullet, Vector2 point, Vector2 normal)
    {
        var state = (BulletState)bullet.CoreData;
        bool real = !state.lookAlike;

        // Special guns: a rocket blows up on the wall, a grenade bounces off it (losing speed) until its fuse is out.
        if (state.gun == PlayerWeapon.GunEffect.Rocket)
        {
            GunBlast(point + normal * 0.1f, state, null);
            return false;
        }
        if (state.gun == PlayerWeapon.GunEffect.Grenade)
        {
            bullet.Body.position = point + normal * 0.15f;
            bullet.SetVelocity(Vector2.Reflect(bullet.Body.linearVelocity, normal) * grenadeBounce);
            return true;
        }

        if (Has(CoreType.Explosive))
            CoreEffects.Blast(explosionEffect, explosionRadius, point + normal * 0.1f, explosionRadius, explosionDamage, Knockback() * explosionKnockback, bridge, real, self);
        if (Has(CoreType.Mortar))
            CoreEffects.Blast(explosionEffect, mortarBlastRadius, point + normal * 0.1f, mortarBlastRadius, mortarBlastDamage, Knockback() * mortarKnockback, bridge, real, self);
        CrossBurst(state, point, normal, null);

        // Pierce goes through crates and barrels too (they are not walls).
        if (state.piercesLeft > 0 && IsProp(bullet.LastHit))
        {
            state.piercesLeft--;
            bullet.IgnoreCollider(bullet.LastHit);
            return true;
        }
        if (state.bouncesLeft > 0)
        {
            state.bouncesLeft--;
            bullet.ScaleDamage(bounceDamage);
            bullet.Body.position = point + normal * 0.1f; // back out of the wall
            bullet.SetVelocity(Vector2.Reflect(bullet.Body.linearVelocity, normal));
            return true;
        }
        return false;
    }

    // Crossfire: 4 short rays in a + (up, down, left, right) burst from the hit; on a wall, not into it.
    // normal = the wall's (zero on a target); ignore = what was hit, so the rays don't hit it again.
    private void CrossBurst(BulletState state, Vector2 point, Vector2 normal, Collider2D ignore)
    {
        if (!Has(CoreType.Cross) || state.isShard)
            return;
        Vector2 from = point + normal * 0.15f;
        for (int k = 0; k < 4; k++)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, k * 90f) * Vector2.right;
            if (Vector2.Dot(direction, normal) < -0.5f)
                continue;
            Spawn(state.prefab, from, direction, state.speed, state.damage * crossDamage, crossLifetime, state.shooter, true, ignore,
                  state.size * 0.8f, state.random.Next(), state.lookAlike, crossColor);
        }
        CoreEffects.Burst(from, crossColor, 6, 6f);
        CoreEffects.Ring(zapMaterial, crossColor, from, 0.9f, 0.15f, 0.15f);
    }

    private static bool IsProp(Collider2D hit) =>
        hit != null && (hit.GetComponent<Crate>() != null || hit.GetComponent<ExplosiveBarrel>() != null || hit.GetComponent<WeaponCrate>() != null);

    // Sticky Bomb: a blinking bomb rides on the target (one at a time) and blows up after a moment.
    private void StickBomb(HitTarget target, Vector2 point, bool lookAlike)
    {
        if (stickyBombs.TryGetValue(target, out CoreStickyBomb old) && old != null)
            return;
        var bomb = new GameObject("StickyBomb");
        bomb.transform.SetParent(target.transform, false);
        bomb.transform.position = point;
        bomb.transform.localScale = Vector3.one * 1.3f;
        var sprite = bomb.AddComponent<SpriteRenderer>();
        sprite.sprite = bombSprite;
        sprite.color = stickyColor;
        sprite.sortingOrder = 6;
        CoreStickyBomb sticky = bomb.AddComponent<CoreStickyBomb>();
        sticky.Setup(this, stickyDelay, lookAlike);
        stickyBombs[target] = sticky;
    }

    public void StickyBlast(Vector2 position, bool lookAlike)
    {
        CoreEffects.Blast(explosionEffect, stickyRadius, position, stickyRadius, stickyDamage, Knockback() * stickyKnockback, bridge, !lookAlike, self);
        CRTScreen.Blur(position, 0.35f);
    }

    // Thunder: a moment after the hit, lightning comes down from the sky onto the target.
    private System.Collections.IEnumerator Thunder(HitTarget target, float damage, bool real)
    {
        yield return new WaitForSeconds(thunderDelay);
        if (target == null || target.IsDying)
            yield break;
        Vector2 point = target.transform.position;
        CoreEffects.Bolt(zapMaterial, thunderGlow, thunderCore, point, 14f, 0.16f);
        CoreEffects.Bolt(zapMaterial, thunderGlow, thunderCore, point, 14f, 0.1f); // a second fork of the same strike
        CoreEffects.Ring(zapMaterial, thunderCore, point, thunderSplash * 1.3f, 0.2f, 0.25f);
        GameAudio.PlayAt(thunderSound, point, thunderVolume, 0.1f);
        CRTScreen.Blur(point, 0.5f);
        if (real)
            target.TakeDamage(damage, point, Vector2.up, 0.3f, bridge);
        // The shock spreads to everyone close by (half), an explosion of sparks shows it.
        CoreEffects.Blast(explosionEffect, 0.9f, point, thunderSplash, damage * 0.5f, 0.4f, bridge, real, target, self);
    }

    // Lightning jumps from the hit target to the nearest others (never this player), and to the barrels and crates
    // near it (barrels go off, crates are knocked). real = also hurt them.
    private void ChainLightning(HitTarget first, float damage, bool real)
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(first.transform.position, chainRange))
        {
            if (!IsProp(hit))
                continue;
            CoreEffects.Zap(zapMaterial, zapColor, first.transform.position, hit.transform.position, 0.08f, 0.1f);
            if (!real)
                continue;
            if (hit.TryGetComponent(out ExplosiveBarrel barrel))
                barrel.Hit(1000f);
            else if (hit.attachedRigidbody != null && hit.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic)
                hit.attachedRigidbody.AddForce(((Vector2)hit.transform.position - (Vector2)first.transform.position).normalized * 8f, ForceMode2D.Impulse);
        }

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
            CoreEffects.Zap(zapMaterial, new Color(zapColor.r, zapColor.g, zapColor.b, 0.45f), a, b, 0.35f, 0.16f); // a thick glow...
            CoreEffects.Zap(zapMaterial, Color.white, a, b, 0.1f, 0.12f);                                            // ...and a white core
            CoreEffects.Ring(zapMaterial, zapColor, b, 1f, 0.15f, 0.2f);
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
        UpdateReticles();
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

        // Black Hole: where the bullet stopped on a wall or the floor, now and then.
        bool onWall = bullet.StoppedBy != null && bullet.StoppedBy.GetComponentInParent<HitTarget>() == null;
        if (Has(CoreType.BlackHole) && onWall && Time.time >= nextBlackHole)
        {
            blackHoles.RemoveAll(hole => hole == null);
            if (blackHoles.Count < maxBlackHoles)
            {
                nextBlackHole = Time.time + blackHoleCooldown;
                blackHoles.Add(PlaceBlackHole(position, state.lookAlike));
            }
        }
    }

    // A dark core in a whirling swirl (CoreBlackHole draws it from these sprites).
    private CoreBlackHole PlaceBlackHole(Vector2 position, bool lookAlike)
    {
        var go = new GameObject("BlackHole");
        go.transform.position = position;
        var hole = go.AddComponent<CoreBlackHole>();
        hole.Setup(this, self, blackHoleSwirlSprite, blackHoleSprite, blackHoleTime, blackHoleRadius, blackHolePull, lookAlike);
        return hole;
    }

    public void BlackHoleCollapse(Vector2 position, bool lookAlike)
    {
        CoreEffects.Blast(explosionEffect, blackHoleBlastRadius, position, blackHoleBlastRadius, blackHoleDamage, Knockback() * 1.5f, bridge, !lookAlike, self);
        CoreEffects.Ring(zapMaterial, blackHoleColor, position, blackHoleBlastRadius * 1.8f, 0.35f, 0.4f);
        GameAudio.PlayAt(gunBlastSound, position, gunBlastVolume * 0.8f, 0.1f);
        CRTScreen.Blur(position, 0.7f);
    }

    // The map changed: black holes left on the old one go away.
    public void ClearBlackHoles()
    {
        foreach (CoreBlackHole hole in blackHoles)
            if (hole != null)
                Destroy(hole.gameObject);
        blackHoles.Clear();
    }

}
