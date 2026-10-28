using UnityEngine;

// Brings the body to life on every machine (the player is one body sprite, so its shape does the acting): it breathes
// when standing, bounces step by step while running, stretches with its speed in the air, squashes on landing,
// stretches on jumping and dashing, and leaves fading ghosts of itself behind while dashing (PlayerAfterimage).
// Everything is read from how the player really moves, so the other machines'
// players move just as alive as this one's. The shape snaps to whole pixels of the sprite (retro), and the feet stay
// on the ground while the body squashes.
public class PlayerBodyAnimator : MonoBehaviour
{
    [Tooltip("The body sprite (the hat and the outfit are its children).")]
    [SerializeField] private Transform body;

    [Header("Standing")]
    [SerializeField] private float breatheAmount = 0.045f;
    [SerializeField] private float breatheSpeed = 1.1f;

    [Header("Running")]
    [Tooltip("How much the body squashes and stretches with each step.")]
    [SerializeField] private float runBounce = 0.09f;

    [Tooltip("Steps per unit run (faster running = faster steps).")]
    [SerializeField] private float stepsPerUnit = 0.55f;

    [Header("Air")]
    [Tooltip("Stretch per unit/sec of vertical speed (tall and thin when fast up or down).")]
    [SerializeField] private float airStretch = 0.006f;
    [SerializeField] private float maxAirStretch = 0.14f;

    [Header("Hits")]
    [SerializeField] private Vector2 landSquash = new Vector2(1.25f, 0.75f);
    [SerializeField] private Vector2 jumpStretch = new Vector2(0.82f, 1.2f);
    [SerializeField] private Vector2 dashStretch = new Vector2(1.3f, 0.78f);

    [Tooltip("Squashed by a hit, like the dummies.")]
    [SerializeField] private Vector2 hitSquash = new Vector2(1.15f, 0.88f);

    [Tooltip("Landing at least this fast squashes fully (units/sec); slower landings squash less.")]
    [SerializeField] private float hardLanding = 20f;

    [Tooltip("How quickly a squash or stretch springs back (higher = snappier).")]
    [SerializeField] private float recovery = 14f;

    [Header("Dash")]
    [Tooltip("While dashing, a ghost of the body is left behind this often (seconds)...")]
    [SerializeField] private float afterimageInterval = 0.03f;

    [Tooltip("...and each fades out over this long.")]
    [SerializeField] private float afterimageLife = 0.22f;

    [Range(0f, 1f)]
    [SerializeField] private float afterimageAlpha = 0.55f;

    private Vector3 baseScale;
    private Vector3 basePosition;
    private float halfHeight;
    private Vector2 pixels;      // the sprite's size in pixels (shapes snap to them)
    private Vector2 punch = Vector2.one;
    private Vector2 lastPosition;
    private Vector2 velocity;
    private float stepPhase;
    private bool grounded;
    private float airVelocityY;
    private Collider2D bodyCollider;
    private PlayerNetwork player;
    private float afterimageTimer;
    private float dashWindow;    // > 0: a dash is going on (or about to show up here)
    private SpriteRenderer[] bodySprites; // the body, outfit and hat (copied by the dash ghosts)
    private readonly RaycastHit2D[] hits = new RaycastHit2D[4];
    private ContactFilter2D groundFilter;

    private void Awake()
    {
        baseScale = body.localScale;
        basePosition = body.localPosition;
        Sprite sprite = body.GetComponent<SpriteRenderer>().sprite;
        halfHeight = sprite.bounds.extents.y * baseScale.y;
        pixels = sprite.rect.size;
        bodyCollider = GetComponent<Collider2D>();
        player = GetComponent<PlayerNetwork>();
        bodySprites = body.GetComponentsInChildren<SpriteRenderer>(true);
        groundFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer) & ~(1 << gameObject.layer));
        groundFilter.useTriggers = false;
        lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        float step = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector2 position = transform.position;
        Vector2 moved = (position - lastPosition) / step;
        lastPosition = position;
        if (moved.sqrMagnitude > 2500f)
            moved = Vector2.zero; // a teleport (respawn, travel), not a move
        velocity = Vector2.Lerp(velocity, moved, 1f - Mathf.Exp(-25f * step));

        bool wasGrounded = grounded;
        grounded = OnGround();
        if (!wasGrounded && grounded && airVelocityY < -4f)
            punch = Vector2.Lerp(Vector2.one, landSquash, Mathf.InverseLerp(4f, hardLanding, -airVelocityY));
        else if (wasGrounded && !grounded && velocity.y > 6f)
            punch = jumpStretch;
        // Dashing: told by the owner (PlayDash), shown while the body really moves fast (other machines see the
        // move a little later than they hear about it).
        dashWindow -= step;
        bool dashing = dashWindow > 0f && velocity.magnitude > 12f;
        if (dashing)
        {
            bool sideways = Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y);
            punch = sideways ? dashStretch : new Vector2(dashStretch.y, dashStretch.x);
            afterimageTimer -= step;
            if (afterimageTimer <= 0f)
            {
                afterimageTimer = afterimageInterval;
                Color color = player != null ? player.Color : Color.white;
                color.a = afterimageAlpha;
                PlayerAfterimage.Leave(bodySprites, color, afterimageLife);
            }
        }
        else
        {
            afterimageTimer = 0f;
        }
        if (!grounded)
            airVelocityY = velocity.y;
        punch = Vector2.Lerp(punch, Vector2.one, 1f - Mathf.Exp(-recovery * step));

        // The idle / run / air shape, on top of the punch.
        Vector2 shape;
        float speed = Mathf.Abs(velocity.x);
        if (!grounded)
        {
            float stretch = Mathf.Min(maxAirStretch, Mathf.Abs(velocity.y) * airStretch);
            shape = new Vector2(1f - stretch * 0.6f, 1f + stretch);
        }
        else if (speed > 1f)
        {
            stepPhase += speed * stepsPerUnit * step;
            float bounce = Mathf.Abs(Mathf.Sin(stepPhase * Mathf.PI)) * runBounce * Mathf.InverseLerp(1f, 6f, speed);
            shape = new Vector2(1f + bounce * 0.5f, 1f - bounce);
        }
        else
        {
            float breathe = (Mathf.Sin(Time.time * breatheSpeed * Mathf.PI * 2f) * 0.5f + 0.5f) * breatheAmount;
            shape = new Vector2(1f, 1f + breathe);
            stepPhase = 0f;
        }
        shape = Vector2.Scale(shape, punch);

        // Snap to whole pixels of the sprite, then keep the feet where they are.
        shape.x = Mathf.Round(shape.x * pixels.x) / pixels.x;
        shape.y = Mathf.Round(shape.y * pixels.y) / pixels.y;
        body.localScale = new Vector3(baseScale.x * shape.x, baseScale.y * shape.y, baseScale.z);
        body.localPosition = basePosition + Vector3.down * (halfHeight * (1f - shape.y));
    }

    // This player dashes for that long (any direction). The window is a little longer, for the other machines'
    // delay in seeing the move.
    public void PlayDash(float duration) => dashWindow = duration + 0.15f;

    // Hit: a quick squash (every machine, from PlayerHealth.PlayBleed).
    public void PlayHit() => punch = hitSquash;

    // Something solid right under the feet (works for every machine's players, moved by physics or not).
    private bool OnGround()
    {
        if (bodyCollider == null || !bodyCollider.enabled)
            return true;
        // A half-height box from the middle down to just under the feet.
        Bounds bounds = bodyCollider.bounds;
        var size = new Vector2(bounds.size.x * 0.8f, bounds.size.y * 0.5f);
        int count = Physics2D.BoxCast(bounds.center, size, 0f, Vector2.down, groundFilter, hits, bounds.size.y * 0.25f + 0.08f);
        for (int i = 0; i < count; i++)
            if (hits[i].collider != bodyCollider && hits[i].normal.y > 0.5f && hits[i].distance > 0f)
                return true;
        return false;
    }
}
