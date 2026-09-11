using UnityEngine;

// Flies straight. When it hits something solid it spawns an impact effect on the surface,
// damages it if it's a Dummy/enemy or the player, and disappears. It also disappears quietly when its lifetime runs out.
// Speed, damage and lifetime come from whoever fires it (PlayerWeapon, RangedEnemy).
// Enemy bullets fly through other enemies. All bullets fly through level blocks set to Bullets Pass Through (LevelBlock).
//
// Item upgrades (only on the player's bullets, see ApplyUpgrades):
// - Homing: steers toward the nearest enemy in range and in front of it; deals less damage.
// - Bounce: bounces off walls this many times before it stops; loses some damage per bounce.
// - Explosive: explodes where it finally hits a wall or an enemy, hurting the other enemies in the blast.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [Tooltip("Child trail. It is left behind to fade out when the projectile disappears.")]
    [SerializeField] private TrailRenderer trail;

    [Tooltip("Spawned where the projectile hits something, with its +X axis pointing out of the surface. " +
             "It should destroy itself (Stop Action = Destroy).")]
    [SerializeField] private ParticleSystem impactEffect;

    [Header("Upgrades")]
    [Tooltip("Homing: only enemies closer than this are chased (units).")]
    [SerializeField] private float homingRange = 5f;

    [Tooltip("Homing: only enemies within this angle of the bullet's flight direction can become its target (degrees, each side).")]
    [SerializeField] private float homingConeAngle = 60f;

    [Tooltip("Homing: how fast the bullet turns toward a target at the edge of Homing Range (degrees/sec).")]
    [SerializeField] private float homingTurnSpeed = 360f;

    [Tooltip("Homing: the turn gets this many times faster as the target gets close (0 = no boost, 3 = 4x right next to it), " +
             "so a bullet doesn't keep circling around a close target.")]
    [SerializeField] private float homingCloseBoost = 3f;

    [Tooltip("Homing: a homing bullet deals this share of its normal damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float homingDamage = 0.8f;

    [Tooltip("Explosive: enemies within this distance of the blast take damage (units).")]
    [SerializeField] private float explosionRadius = 1f;

    [Tooltip("Explosive: blast damage to each enemy in range, except the one hit directly (it only takes the bullet's damage).")]
    [SerializeField] private float explosionDamage = 5f;

    [Tooltip("Bounce: the bullet keeps this share of its damage after each bounce.")]
    [Range(0f, 1f)]
    [SerializeField] private float bounceDamage = 0.7f;

    [Tooltip("Explosive: spawned at the blast. It should destroy itself (Stop Action = Destroy).")]
    [SerializeField] private ParticleSystem explosionEffect;

    public float Damage { get; private set; }

    private Rigidbody2D rb;
    private Rigidbody2D shooter;
    private bool firedByEnemy;
    private float lifeTimer;
    private bool homing;
    private bool explosive;
    private int bouncesLeft;
    private Dummy target;
    private bool finished; // hit something: Destroy only happens at the end of the frame, so ignore any further overlaps

    public void Launch(Vector2 direction, float speed, float damage, float lifetime, Rigidbody2D shooter)
    {
        rb = GetComponent<Rigidbody2D>();
        Damage = damage;
        this.shooter = shooter;
        firedByEnemy = shooter != null && shooter.GetComponent<Dummy>() != null; // remembered: the enemy may die first
        lifeTimer = lifetime;
        rb.linearVelocity = direction.normalized * speed;
    }

    // Called by PlayerWeapon right after Launch, with the player's item upgrades.
    public void ApplyUpgrades(bool homing, bool explosive, int bounces)
    {
        this.homing = homing;
        this.explosive = explosive;
        bouncesLeft = bounces;
        if (homing)
            Damage *= homingDamage;
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
            Disappear();
    }

    // Homing: turns the flight direction toward the target a bit every physics step, keeping the speed.
    private void FixedUpdate()
    {
        if (!homing)
            return;

        if (target == null || !target.isActiveAndEnabled || target.IsDying)
            target = FindTarget();
        if (target == null)
            return;

        // A fast bullet can't turn tight enough to hit a close target and would circle around it,
        // so it turns faster the closer the target is.
        Vector2 toTarget = (Vector2)target.transform.position - rb.position;
        float closeness = 1f - Mathf.Clamp01(toTarget.magnitude / homingRange); // 0 = at the edge of the range, 1 = right there
        float maxTurn = homingTurnSpeed * (1f + homingCloseBoost * closeness) * Mathf.Deg2Rad * Time.fixedDeltaTime;
        Vector2 direction = Vector3.RotateTowards(rb.linearVelocity.normalized, toTarget.normalized, maxTurn, 0f);
        rb.linearVelocity = direction * rb.linearVelocity.magnitude;
        FaceVelocity();
    }

    private Dummy FindTarget()
    {
        Dummy nearest = null;
        float nearestDistance = homingRange;
        foreach (Dummy dummy in Dummy.Active)
        {
            if (dummy.IsDying)
                continue;

            Vector2 toDummy = (Vector2)dummy.transform.position - rb.position;
            if (Vector2.Angle(rb.linearVelocity, toDummy) > homingConeAngle)
                continue; // behind or off to the side: not a target

            float distance = toDummy.magnitude;
            if (distance < nearestDistance)
            {
                nearest = dummy;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    // The projectile's collider is a trigger, so this runs when it overlaps a block or a dummy.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore whoever fired it and other triggers (e.g. other bullets, items).
        if (finished || other.isTrigger || other.attachedRigidbody == shooter)
            return;

        LevelBlock block = other.GetComponent<LevelBlock>();
        if (block != null && block.BulletsPassThrough)
            return; // thin platform/wall: bullets fly through

        Dummy dummy = other.GetComponentInParent<Dummy>();
        if (firedByEnemy && dummy != null)
            return; // enemies don't shoot each other

        // Hit point = the point of the surface closest to where the bullet was one physics step ago.
        // The direction from that point back to the bullet is the surface normal.
        Vector2 previousPosition = rb.position - rb.linearVelocity * Time.fixedDeltaTime;
        Vector2 hitPoint = other.ClosestPoint(previousPosition);
        Vector2 normal = (previousPosition - hitPoint).normalized;
        if (normal == Vector2.zero)
            normal = -rb.linearVelocity.normalized; // bullet started inside the collider: face back the way it came

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (dummy != null)
        {
            dummy.TakeDamage(Damage, hitPoint, normal); // the dummy shows its own blood + damage number
        }
        else if (player != null)
        {
            player.TakeDamage(Damage, rb.linearVelocity.normalized); // pushed the way the bullet flies
        }
        else
        {
            float normalAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
            Instantiate(impactEffect, hitPoint, Quaternion.Euler(0f, 0f, normalAngle)); // sparks off walls/floor

            // Bounce: fly on, mirrored off the surface.
            if (bouncesLeft > 0)
            {
                bouncesLeft--;
                Damage *= bounceDamage;
                rb.linearVelocity = Vector2.Reflect(rb.linearVelocity, normal);
                rb.position = hitPoint + normal * 0.1f; // back out of the wall
                FaceVelocity();
                return;
            }
        }

        if (explosive)
            Explode(hitPoint + normal * 0.1f, dummy);
        Disappear();
    }

    // Hurts every enemy within the blast radius (an enemy has one collider, so it's hit once) except the one
    // the bullet hit directly, pushing them away from the blast.
    private void Explode(Vector2 position, Dummy hitDirectly)
    {
        if (explosionEffect != null)
            Instantiate(explosionEffect, position, Quaternion.identity);

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, explosionRadius))
        {
            Dummy dummy = hit.GetComponentInParent<Dummy>();
            if (dummy == null || dummy == hitDirectly)
                continue;

            Vector2 closestPoint = hit.ClosestPoint(position);
            Vector2 towardBlast = (position - closestPoint).normalized; // TakeDamage pushes the other way
            if (towardBlast == Vector2.zero)
                towardBlast = Vector2.up;
            dummy.TakeDamage(explosionDamage, closestPoint, towardBlast);
        }
    }

    private void FaceVelocity()
    {
        Vector2 velocity = rb.linearVelocity;
        rb.rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
    }

    private void Disappear()
    {
        if (finished)
            return;
        finished = true;

        // Leave the trail behind so it fades out instead of vanishing with the bullet.
        trail.transform.SetParent(null);
        trail.autodestruct = true;
        Destroy(gameObject);
    }
}
