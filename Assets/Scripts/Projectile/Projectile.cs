using UnityEngine;

// Flies straight. When it hits something solid it spawns an impact effect on the surface,
// damages it if it's a Dummy/enemy or the player, and disappears. It also disappears quietly when its lifetime runs out.
// Speed, damage and lifetime come from whoever fires it (PlayerWeapon, RangedEnemy).
// Enemy bullets fly through other enemies. All bullets fly through level blocks set to Bullets Pass Through (LevelBlock).
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [Tooltip("Child trail. It is left behind to fade out when the projectile disappears.")]
    [SerializeField] private TrailRenderer trail;

    [Tooltip("Spawned where the projectile hits something, with its +X axis pointing out of the surface. " +
             "It should destroy itself (Stop Action = Destroy).")]
    [SerializeField] private ParticleSystem impactEffect;

    public float Damage { get; private set; }

    private Rigidbody2D rb;
    private Rigidbody2D shooter;
    private bool firedByEnemy;
    private float lifeTimer;

    public void Launch(Vector2 direction, float speed, float damage, float lifetime, Rigidbody2D shooter)
    {
        rb = GetComponent<Rigidbody2D>();
        Damage = damage;
        this.shooter = shooter;
        firedByEnemy = shooter != null && shooter.GetComponent<Dummy>() != null; // remembered: the enemy may die first
        lifeTimer = lifetime;
        rb.linearVelocity = direction.normalized * speed;
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
            Disappear();
    }

    // The projectile's collider is a trigger, so this runs when it overlaps a block or a dummy.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore whoever fired it and other triggers (e.g. other bullets).
        if (other.isTrigger || other.attachedRigidbody == shooter)
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
        }

        Disappear();
    }

    private void Disappear()
    {
        // Leave the trail behind so it fades out instead of vanishing with the bullet.
        trail.transform.SetParent(null);
        trail.autodestruct = true;
        Destroy(gameObject);
    }
}
