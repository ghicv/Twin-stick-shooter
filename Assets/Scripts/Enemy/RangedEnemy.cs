using UnityEngine;

// Long-range enemy: doesn't chase. It patrols back and forth on its platform (turns at walls and edges)
// with its gun always pointed at the player. Every few seconds, if it can see the player and is in range,
// it stops, aims for a moment (wind-up) and fires a slow bullet the player can dodge.
[RequireComponent(typeof(EnemyMovement))]
public class RangedEnemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.8f;

    [Header("Gun")]
    [Tooltip("Rotates toward the player. The gun sprite and the fire point are children of it.")]
    [SerializeField] private Transform gunPivot;

    [Tooltip("Bullets spawn here and fly toward the player.")]
    [SerializeField] private Transform firePoint;

    [SerializeField] private Projectile projectilePrefab;

    [Header("Attack")]
    [Tooltip("Only shoots when the player is closer than this (units).")]
    [SerializeField] private float attackRange = 14f;

    [Tooltip("Average seconds between shots.")]
    [SerializeField] private float attackCooldown = 2.2f;

    [Tooltip("Random +/- change of each cooldown, so several enemies don't fire in sync (seconds).")]
    [SerializeField] private float cooldownRandomness = 0.6f;

    [Tooltip("Seconds standing still and aiming before the shot.")]
    [SerializeField] private float windupTime = 0.45f;

    [Tooltip("Units/sec. Slower than the player's bullets so they can be dodged.")]
    [SerializeField] private float projectileSpeed = 11f;

    [SerializeField] private float projectileDamage = 10f;
    [SerializeField] private float projectileLifetime = 3f;

    [Tooltip("Layers that block the line of sight (the level).")]
    [SerializeField] private LayerMask sightBlockers = 1; // Default

    [Header("Sound")]
    [SerializeField] private AudioClip shootSound;

    [Range(0f, 1f)]
    [SerializeField] private float shootVolume = 0.5f;

    [Tooltip("Lower than 1 = deeper than the player's gun.")]
    [SerializeField] private float shootPitch = 0.75f;

    private EnemyMovement movement;
    private Dummy dummy;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private PlayerHealth player;
    private int patrolDirection = 1;
    private float cooldownTimer;
    private float windupTimer;

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        dummy = GetComponent<Dummy>();
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        player = GameObject.FindWithTag("Player").GetComponent<PlayerHealth>();
        patrolDirection = Random.value < 0.5f ? -1 : 1;
        cooldownTimer = Random.Range(0.5f, attackCooldown); // first shot comes at a random moment
    }

    private void Update()
    {
        if (dummy.IsDying)
            return;
        float dt = Time.deltaTime * dummy.SpeedFactor; // slowed down → aims and shoots slower too

        // Gun always points at the player.
        Vector2 toPlayer = player.transform.position - gunPivot.position;
        gunPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg);

        // Aiming: stand still, then fire.
        if (windupTimer > 0f)
        {
            movement.Move(0f, 0f);
            windupTimer -= dt;
            if (windupTimer <= 0f)
                Shoot();
            return;
        }

        // Patrol: turn around at walls and edges.
        if (movement.IsWallAhead(patrolDirection) || movement.IsEdgeAhead(patrolDirection))
            patrolDirection = -patrolDirection;
        movement.Move(patrolDirection, moveSpeed);

        cooldownTimer -= dt;
        if (cooldownTimer <= 0f && !player.IsDead && CanSeePlayer())
            windupTimer = windupTime;
    }

    // In range, and nothing in between that stops bullets. Level blocks set to Bullets Pass Through
    // (LevelBlock) don't block the view, since bullets fly through them.
    private bool CanSeePlayer()
    {
        Vector2 from = firePoint.position;
        Vector2 to = player.transform.position;
        if (Vector2.Distance(from, to) > attackRange)
            return false;

        foreach (RaycastHit2D hit in Physics2D.LinecastAll(from, to, sightBlockers))
        {
            LevelBlock block = hit.collider.GetComponent<LevelBlock>();
            if (block == null || !block.BulletsPassThrough)
                return false;
        }
        return true;
    }

    private void Shoot()
    {
        cooldownTimer = attackCooldown + Random.Range(-cooldownRandomness, cooldownRandomness);
        if (player.IsDead)
            return;

        Vector2 direction = ((Vector2)player.transform.position - (Vector2)firePoint.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.Euler(0f, 0f, angle));
        projectile.Launch(direction, projectileSpeed, projectileDamage, projectileLifetime, rb);

        if (shootSound != null)
        {
            audioSource.pitch = shootPitch;
            audioSource.PlayOneShot(shootSound, shootVolume);
        }
    }
}
