using UnityEngine;

// Close-range enemy: chases the player (jumps up when the player is above or a wall is in the way,
// drops down when the player is below, never walks into the void). In reach it stops for a moment
// (wind-up, so the player can see it coming), then lunges and hits.
[RequireComponent(typeof(EnemyMovement))]
public class MeleeEnemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("Attack")]
    [Tooltip("Attacks when the player is closer than this sideways (units, center to center).")]
    [SerializeField] private float attackRange = 1.1f;

    [Tooltip("...and not more than this above or below (units).")]
    [SerializeField] private float attackHeight = 1f;

    [Tooltip("Seconds standing still before the hit lands.")]
    [SerializeField] private float windupTime = 0.3f;

    [Tooltip("Seconds between the end of one attack and the next wind-up.")]
    [SerializeField] private float attackCooldown = 0.8f;

    [SerializeField] private float damage = 15f;

    [Tooltip("Sideways speed of the lunge when the hit lands (units/sec).")]
    [SerializeField] private float lungeSpeed = 6f;

    [Header("Jumping")]
    [Tooltip("Jumps when the player is at least this much higher and close sideways (units).")]
    [SerializeField] private float jumpWhenPlayerAbove = 1.5f;

    [SerializeField] private float jumpCooldown = 1f;

    private EnemyMovement movement;
    private Dummy dummy;
    private PlayerHealth player;
    private float windupTimer;
    private float cooldownTimer;
    private float jumpTimer;

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        dummy = GetComponent<Dummy>();
        player = GameObject.FindWithTag("Player").GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;
        jumpTimer -= Time.deltaTime;
        if (dummy.IsDying)
            return;

        Vector2 toPlayer = player.transform.position - transform.position;
        int direction = toPlayer.x > 0f ? 1 : -1;

        // Winding up: stand still, then strike.
        if (windupTimer > 0f)
        {
            movement.Move(0f, 0f);
            windupTimer -= Time.deltaTime;
            if (windupTimer <= 0f)
                Strike(toPlayer, direction);
            return;
        }

        if (player.IsDead)
        {
            movement.Move(0f, 0f);
            return;
        }

        // In reach → wind up an attack.
        if (Mathf.Abs(toPlayer.x) <= attackRange && Mathf.Abs(toPlayer.y) <= attackHeight)
        {
            movement.Move(0f, 0f);
            if (cooldownTimer <= 0f)
                windupTimer = windupTime;
            return;
        }

        // Chase. Stop at an edge, unless the player is below and there is something to land on.
        bool playerBelow = toPlayer.y < -1f;
        bool blockedByEdge = movement.IsEdgeAhead(direction) && !(playerBelow && movement.IsDropSafe(direction));
        movement.Move(blockedByEdge || Mathf.Abs(toPlayer.x) < 0.2f ? 0f : direction, moveSpeed);

        // Jump over a wall, or up toward a player standing above.
        bool playerAboveAndClose = toPlayer.y > jumpWhenPlayerAbove && Mathf.Abs(toPlayer.x) < 3f;
        if (movement.IsGrounded && jumpTimer <= 0f && (movement.IsWallAhead(direction) || playerAboveAndClose))
        {
            movement.Jump();
            jumpTimer = jumpCooldown;
        }
    }

    private void Strike(Vector2 toPlayer, int direction)
    {
        movement.Push(direction * lungeSpeed);
        cooldownTimer = attackCooldown;

        // The player may have stepped back during the wind-up: a little extra reach for the lunge.
        if (Mathf.Abs(toPlayer.x) <= attackRange * 1.4f && Mathf.Abs(toPlayer.y) <= attackHeight)
            player.TakeDamage(damage, new Vector2(direction, 0f));
    }
}
