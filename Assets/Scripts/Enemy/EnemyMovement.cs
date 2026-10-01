using UnityEngine;

// Legwork shared by the enemies: walks at a target speed (eased), jumps, and looks at the level around it
// (on the ground? wall ahead? edge ahead? safe to drop down?). MeleeEnemy / RangedEnemy decide where to go.
// Right after a hit it lets go of the controls for a moment, so bullet knockback still pushes the enemy.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Dummy))]
public class EnemyMovement : MonoBehaviour
{
    [Tooltip("How quickly the speed follows the target speed (higher = snappier).")]
    [SerializeField] private float acceleration = 15f;

    [Tooltip("Upward speed of a jump (units/sec). 16 = the player's jump height.")]
    [SerializeField] private float jumpSpeed = 16f;

    [Tooltip("Seconds without control after being hit, so the knockback can push the enemy.")]
    [SerializeField] private float staggerTime = 0.25f;

    [Tooltip("A drop deeper than this counts as the void (the enemy won't walk off there) (units).")]
    [SerializeField] private float safeDropDistance = 12f;

    [Tooltip("Layers that count as ground and walls (the level).")]
    [SerializeField] private LayerMask groundMask = 1; // Default

    public bool IsGrounded { get; private set; }

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Dummy dummy;
    private float targetSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        dummy = GetComponent<Dummy>();
    }

    // direction: -1 = left, 0 = stop, 1 = right.
    public void Move(float direction, float speed)
    {
        targetSpeed = direction * speed;
    }

    public void Jump()
    {
        if (IsGrounded)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpSpeed);
    }

    // Sets the sideways speed right now (e.g. a lunge).
    public void Push(float speedX)
    {
        rb.linearVelocity = new Vector2(speedX, rb.linearVelocity.y);
    }

    // A solid wall (floor block, pillar) right in front. One-way platforms don't count.
    public bool IsWallAhead(int direction)
    {
        Bounds bounds = bodyCollider.bounds;
        RaycastHit2D hit = Physics2D.Raycast(bounds.center, Vector2.right * direction, bounds.extents.x + 0.1f, groundMask);
        return hit.collider != null && hit.collider.GetComponent<PlatformEffector2D>() == null;
    }

    // Standing on the ground and there is no ground just in front of the feet.
    public bool IsEdgeAhead(int direction)
    {
        return IsGrounded && Physics2D.Raycast(FootAhead(direction), Vector2.down, 0.4f, groundMask).collider == null;
    }

    // Something to land on below the spot in front (not the void).
    public bool IsDropSafe(int direction)
    {
        return Physics2D.Raycast(FootAhead(direction), Vector2.down, safeDropDistance, groundMask).collider != null;
    }

    private Vector2 FootAhead(int direction)
    {
        Bounds bounds = bodyCollider.bounds;
        return new Vector2(bounds.center.x + direction * (bounds.extents.x + 0.15f), bounds.min.y + 0.05f);
    }

    private void FixedUpdate()
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 feet = new Vector2(bounds.center.x, bounds.min.y + 0.05f);
        IsGrounded = rb.linearVelocity.y <= 0.1f && Physics2D.Raycast(feet, Vector2.down, 0.15f, groundMask).collider != null;

        bool staggered = Time.time - dummy.LastHitTime < staggerTime;
        if (dummy.IsDying || staggered)
            return; // let knockback / the death play out

        float speedX = Mathf.Lerp(rb.linearVelocity.x, targetSpeed * dummy.SpeedFactor, 1f - Mathf.Exp(-acceleration * Time.fixedDeltaTime));
        rb.linearVelocity = new Vector2(speedX, rb.linearVelocity.y);
    }
}
