using UnityEngine;

// Flies straight. When it hits something solid it spawns an impact effect on the surface,
// damages it if it's a Dummy/enemy or the player, and disappears. It also disappears quietly when its lifetime runs out.
// Speed, damage and lifetime come from whoever fires it (PlayerWeapon, RangedEnemy).
// Enemy bullets fly through other enemies. All bullets fly through level blocks set to Bullets Pass Through (LevelBlock).
//
// The player's bullets get the cores (augments) attached (AttachCores): at each moment of its life
// (flying, hitting an enemy, hitting a wall, disappearing) the bullet tells the CoreBridge, which may steer it,
// change its damage, keep it flying, etc. Bullets without cores (the enemies') never call it.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [Tooltip("Child trail. It is left behind to fade out when the projectile disappears.")]
    [SerializeField] private TrailRenderer trail;

    [Tooltip("Spawned where the projectile hits something, with its +X axis pointing out of the surface. " +
             "It should destroy itself (Stop Action = Destroy).")]
    [SerializeField] private ParticleSystem impactEffect;

    public float Damage { get; private set; }
    public Rigidbody2D Body => rb;

    // The last collider it hit (e.g. shards from it ignore that enemy).
    public Collider2D LastHit { get; private set; }

    // What it stopped in when it disappeared: an enemy's or a wall's collider, or null (time ran out / caught).
    public Collider2D StoppedBy { get; private set; }

    // The core system's own data about this bullet. Only the core system reads it.
    public object CoreData { get; private set; }

    private Rigidbody2D rb;
    private Collider2D bulletCollider;
    private SpriteRenderer sprite;
    private Rigidbody2D shooter;
    private CoreBridge cores; // null = plain bullet
    private bool firedByEnemy;
    private float lifeTimer;
    private bool finished; // gone: Destroy only happens at the end of the frame, so ignore any further overlaps

    public void Launch(Vector2 direction, float speed, float damage, float lifetime, Rigidbody2D shooter)
    {
        rb = GetComponent<Rigidbody2D>();
        bulletCollider = GetComponent<Collider2D>();
        sprite = GetComponent<SpriteRenderer>();
        Damage = damage;
        this.shooter = shooter;
        firedByEnemy = shooter != null && shooter.GetComponent<Dummy>() != null; // remembered: the enemy may die first
        lifeTimer = lifetime;
        SetVelocity(direction.normalized * speed);
    }

    public void AttachCores(CoreBridge cores, object coreData)
    {
        this.cores = cores;
        CoreData = coreData;
    }

    public void ScaleDamage(float factor)
    {
        Damage *= factor;
    }

    // Changes the flight (speed and direction); the bullet turns to face it.
    public void SetVelocity(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
        rb.rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
    }

    // Bigger or smaller bullet (sprite, hit area and trail).
    public void SetSize(float scale)
    {
        transform.localScale *= scale;
        trail.widthMultiplier *= scale;
    }

    public void SetTint(Color color)
    {
        if (sprite != null)
            sprite.color = color;
        trail.startColor = color;
    }

    // This bullet will fly through that collider from now on.
    public void IgnoreCollider(Collider2D other)
    {
        if (other != null)
            Physics2D.IgnoreCollision(bulletCollider, other);
    }

    // Ends the bullet right now (e.g. a boomerang caught by the player).
    public void Finish()
    {
        Disappear(rb.position, rb.linearVelocity.normalized, null);
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
            Disappear(rb.position, rb.linearVelocity.normalized, null);
    }

    private void FixedUpdate()
    {
        if (cores != null && !finished)
            cores.OnBulletFly(this);
    }

    // The projectile's collider is a trigger, so this runs when it overlaps a block or a dummy.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore whoever fired it and other triggers (e.g. other bullets, mines).
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

        LastHit = other;
        Vector2 flightDirection = rb.linearVelocity.normalized;
        bool keepFlying = false;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (dummy != null)
        {
            // The dummy shows its own blood + damage number.
            dummy.TakeDamage(Damage, hitPoint, normal, cores != null ? cores.BulletKnockback : 1f);
            if (cores != null)
                keepFlying = cores.OnBulletHitEnemy(this, dummy, hitPoint, normal);
            if (keepFlying)
                IgnoreCollider(other); // flies on through: don't hit this enemy again
        }
        else if (player != null)
        {
            player.TakeDamage(Damage, flightDirection); // pushed the way the bullet flies
        }
        else
        {
            float normalAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
            Instantiate(impactEffect, hitPoint, Quaternion.Euler(0f, 0f, normalAngle)); // sparks off walls/floor
            if (cores != null)
                keepFlying = cores.OnBulletHitWall(this, hitPoint, normal);
        }

        // Stopped in an enemy: things fly on the way the bullet went. Stopped in a wall: out of the wall.
        if (!keepFlying)
            Disappear(dummy != null ? hitPoint : hitPoint + normal * 0.1f, dummy != null ? flightDirection : normal, other);
    }

    private void Disappear(Vector2 position, Vector2 direction, Collider2D stoppedBy)
    {
        if (finished)
            return;
        finished = true;
        StoppedBy = stoppedBy;

        if (cores != null)
            cores.OnBulletEnd(this, position, direction);

        // Leave the trail behind so it fades out instead of vanishing with the bullet.
        trail.transform.SetParent(null);
        trail.autodestruct = true;
        Destroy(gameObject);
    }
}
