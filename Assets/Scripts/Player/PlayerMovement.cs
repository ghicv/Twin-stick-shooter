using UnityEngine;
using UnityEngine.InputSystem;

// Rigidbody2D platformer movement: A/D to run, Space to jump (plus a double jump in the air),
// S to drop through one-way platforms, and wall slide / wall jump when touching a wall in the air.
// Horizontal speed eases toward the target speed every physics step: it reacts right away,
// then settles gently at full speed or at a stop, so there are no hard corners in the motion.
// Every jump (ground, wall and double jump) plays the jump sound; only hard landings play the landing sound.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Run")]
    [Tooltip("Max horizontal speed (units/sec).")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("How quickly speed follows A/D (higher = snappier). Speed eases in: fast at first, gentle near full speed.")]
    [SerializeField] private float acceleration = 18f;

    [Tooltip("How quickly you stop after releasing A/D (higher = shorter slide). Also how quickly you brake onto a wall.")]
    [SerializeField] private float deceleration = 25f;

    [Tooltip("Share of the ground acceleration/deceleration kept in the air. 0 = no air control, 1 = same as on the ground.")]
    [Range(0f, 1f)]
    [SerializeField] private float airControl = 0.8f;

    [Header("Jump")]
    [Tooltip("Upward impulse of the ground jump. With Rigidbody2D mass = 1 this is the jump speed (units/sec).")]
    [SerializeField] private float jumpForce = 16f;

    [Tooltip("Upward impulse of each jump made in the air.")]
    [SerializeField] private float doubleJumpForce = 14f;

    [Tooltip("Jumps allowed in the air before landing again (or touching a wall). 1 = double jump.")]
    [Min(0)]
    [SerializeField] private int airJumps = 1;

    [Tooltip("Releasing Space while rising multiplies the upward speed by this: tap = short hop, hold = full jump.")]
    [Range(0f, 1f)]
    [SerializeField] private float jumpCutMultiplier = 0.5f;

    [Tooltip("Near the top of a jump (vertical speed below this, units/sec) gravity is lighter while Space is held: a short float.")]
    [SerializeField] private float apexHangSpeed = 2.5f;

    [Tooltip("Gravity multiplier during that float at the top of a jump.")]
    [Range(0f, 1f)]
    [SerializeField] private float apexGravityMultiplier = 0.5f;

    [Tooltip("Gravity multiplier while falling, for a snappier descent.")]
    [Min(1f)]
    [SerializeField] private float fallGravityMultiplier = 1.3f;

    [Tooltip("A ground jump (or wall jump) still works this long after leaving the ground (or wall) (seconds).")]
    [SerializeField] private float coyoteTime = 0.1f;

    [Tooltip("Space pressed this long before landing still jumps on landing (seconds).")]
    [Min(0.01f)]
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Wall")]
    [Tooltip("Falling while holding toward a wall you touch slows you down to this speed: you slide down the wall " +
             "(units/sec). Not while moving up, so a jump along a wall keeps its height.")]
    [Min(0.1f)]
    [SerializeField] private float wallSlideSpeed = 2.5f;

    [Tooltip("Upward impulse of a wall jump.")]
    [SerializeField] private float wallJumpForce = 15f;

    [Tooltip("Sideways speed a wall jump gives, away from the wall (units/sec).")]
    [SerializeField] private float wallJumpPush = 7f;

    [Tooltip("After a wall jump, A/D control comes back gradually over this time (none → full), " +
             "so the jump first carries you away from the wall and then blends into normal air control (seconds).")]
    [SerializeField] private float wallJumpControlTime = 0.25f;

    [Header("Drop Through")]
    [Tooltip("Holding S on a one-way platform (PlatformEffector2D) drops through it. " +
             "Collision with that platform stays off at least this long, and until the player is out of it (seconds).")]
    [SerializeField] private float dropThroughTime = 0.25f;

    [Header("Squash & Stretch")]
    [Tooltip("Body sprite that gets squashed and stretched (visual only, the collider doesn't change).")]
    [SerializeField] private Transform body;

    [Tooltip("Body scale right after a jump (x thinner, y taller).")]
    [SerializeField] private Vector2 jumpStretch = new Vector2(0.9f, 1.12f);

    [Tooltip("Body scale after a hard landing (x wider, y shorter). Soft landings squash less.")]
    [SerializeField] private Vector2 landSquash = new Vector2(1.15f, 0.85f);

    [Tooltip("How quickly the body gets back to its normal shape (higher = snappier).")]
    [SerializeField] private float squashRecovery = 12f;

    [Header("Dust")]
    [Tooltip("Dust puff spawned when jumping off the ground or a wall, and when landing. It destroys itself.")]
    [SerializeField] private ParticleSystem dustPuff;

    [Tooltip("Size of the dust puff when jumping off the ground or a wall.")]
    [SerializeField] private float jumpDustScale = 0.8f;

    [Tooltip("Size of the dust puff after a hard landing. Softer landings make smaller puffs.")]
    [SerializeField] private float landDustScale = 1.3f;

    [Header("Sound")]
    [Tooltip("Played on every jump: ground, wall and double jump.")]
    [SerializeField] private AudioClip jumpSound;

    [Range(0f, 1f)]
    [SerializeField] private float jumpVolume = 0.3f;

    [Tooltip("Played on hard landings only (see Hard Landing Speed). Harder landings are louder.")]
    [SerializeField] private AudioClip landSound;

    [Tooltip("Volume of the hardest landing.")]
    [Range(0f, 1f)]
    [SerializeField] private float landVolume = 0.35f;

    [Tooltip("The landing sound only plays when landing at least this fast (units/sec). A full jump on flat ground " +
             "lands at about 18, a double jump or a jump down from a platform above at about 24 or more.")]
    [SerializeField] private float hardLandingSpeed = 21f;

    [Tooltip("Random pitch change per jump / landing, so they don't sound identical.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float pitchVariation = 0.08f;

    public bool IsGrounded { get; private set; }

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private AudioSource audioSource;
    private InputAction moveAction;
    private InputAction jumpAction;
    private float baseGravityScale;

    private float moveInput;        // -1 = left, 0 = none, 1 = right
    private bool downHeld;          // S / down
    private bool jumpHeld;
    private float jumpBufferTimer;  // > 0: Space was pressed recently and is waiting to be used
    private float coyoteTimer;      // > 0: a ground jump is still allowed
    private int airJumpsLeft;
    private bool canCutJump;        // true while rising from a jump
    private float lastVelocityY;    // vertical speed in the previous physics step (how hard we land)

    private int wallPush;               // touching a wall in the air right now: +1 = a wall jump pushes right (wall on the left), -1 = left, 0 = none
    private int lastWallPush;           // the last wall touched, used by wall coyote time
    private Vector2 wallContactPoint;
    private float wallCoyoteTimer;      // > 0: a wall jump is still allowed (touching a wall, or just left one)
    private float wallJumpTimer;        // > 0 right after a wall jump: A/D control is coming back
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];

    private Collider2D droppingThrough; // one-way platform we are currently falling through
    private float dropTimer;
    private readonly Collider2D[] groundColliders = new Collider2D[4];

    private Vector3 bodyBaseScale;
    private Vector3 bodyBasePosition;
    private float bodyHalfHeight;
    private Vector2 bodyShape = Vector2.one; // current squash/stretch factor, (1, 1) = normal

    // Grounded = touching any collider whose surface faces up (floor, top of a platform, ...).
    private ContactFilter2D groundFilter;

    private Vector2 FeetPosition => new Vector2(rb.position.x, bodyCollider.bounds.min.y);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        baseGravityScale = rb.gravityScale;

        // Actions come from the project-wide input asset: Assets/InputSystem_Actions.inputactions
        moveAction = InputSystem.actions.FindAction("Player/Move");
        jumpAction = InputSystem.actions.FindAction("Player/Jump");

        groundFilter.SetNormalAngle(45f, 135f);

        bodyBaseScale = body.localScale;
        bodyBasePosition = body.localPosition;
        bodyHalfHeight = body.GetComponent<SpriteRenderer>().sprite.bounds.extents.y * bodyBaseScale.y;
    }

    private void Update()
    {
        // Read input every frame, apply it in FixedUpdate (physics).
        Vector2 move = moveAction.ReadValue<Vector2>();
        moveInput = move.x;
        downHeld = move.y < -0.5f;
        jumpHeld = jumpAction.IsPressed();

        if (jumpAction.WasPressedThisFrame())
            jumpBufferTimer = jumpBufferTime;

        UpdateSquashStretch();
    }

    private void FixedUpdate()
    {
        UpdateGrounded();
        UpdateWall();

        wallJumpTimer -= Time.fixedDeltaTime;
        Run();

        // Wall slide: falling while holding toward a wall you touch → you brake down to Wall Slide Speed
        // and slide down the wall. Never while moving up, so a jump along a wall keeps its full height.
        // Let go to fall normally, press Space to wall jump.
        bool sliding = wallJumpTimer <= 0f && wallPush != 0 && moveInput * wallPush < 0f && rb.linearVelocity.y <= 0f;
        rb.gravityScale = GravityScale(sliding);
        if (sliding)
        {
            float fallSpeed = Mathf.Lerp(rb.linearVelocity.y, -wallSlideSpeed, 1f - Mathf.Exp(-deceleration * Time.fixedDeltaTime));
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, fallSpeed);
        }

        // S on a one-way platform → fall through it (a pending jump wins).
        if (downHeld && IsGrounded && jumpBufferTimer <= 0f)
            TryDropThrough();
        UpdateDropThrough();

        if (jumpBufferTimer > 0f && TryJump())
            jumpBufferTimer = 0f;
        jumpBufferTimer -= Time.fixedDeltaTime;

        // Variable jump height: letting go of Space while still rising cuts the jump short.
        if (canCutJump && !jumpHeld && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            canCutJump = false;
        }
        if (rb.linearVelocity.y <= 0f)
            canCutJump = false;

        lastVelocityY = rb.linearVelocity.y;
    }

    // Gravity for this step: none while wall sliding, lighter near the top of a jump while Space is held
    // (a short float that rounds off the arc), heavier while falling, normal otherwise.
    private float GravityScale(bool sliding)
    {
        if (sliding)
            return 0f;

        float verticalSpeed = rb.linearVelocity.y;
        if (!IsGrounded && jumpHeld && Mathf.Abs(verticalSpeed) < apexHangSpeed)
            return baseGravityScale * apexGravityMultiplier;
        if (verticalSpeed < 0f)
            return baseGravityScale * fallGravityMultiplier;
        return baseGravityScale;
    }

    private void UpdateGrounded()
    {
        bool wasGrounded = IsGrounded;

        // Not while moving up, so the step right after a jump never counts as "still on the ground".
        IsGrounded = rb.IsTouching(groundFilter) && rb.linearVelocity.y <= 0.01f;

        if (IsGrounded)
        {
            coyoteTimer = coyoteTime;
            airJumpsLeft = airJumps;

            if (!wasGrounded)
            {
                // Landing: squash and dust, stronger the faster we were falling (full at 20 units/sec).
                float fallSpeed = -lastVelocityY;
                float impact = Mathf.InverseLerp(2f, 20f, fallSpeed);
                bodyShape = Vector2.Lerp(Vector2.one, landSquash, impact);
                if (impact > 0f)
                    SpawnDust(FeetPosition, landDustScale * Mathf.Lerp(0.5f, 1f, impact));

                // Sound only for hard landings (double jump, jumping down from above), louder the harder.
                if (fallSpeed >= hardLandingSpeed)
                    PlaySound(landSound, landVolume * Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(hardLandingSpeed, hardLandingSpeed + 8f, fallSpeed)));
            }
        }
        else
        {
            coyoteTimer -= Time.fixedDeltaTime;
        }
    }

    // In the air and touching a wall (a surface facing sideways)? Then wall sliding and a wall jump are possible
    // and the air jumps come back. Disabled contacts (passing through a one-way platform) don't count.
    private void UpdateWall()
    {
        wallPush = 0;
        wallCoyoteTimer -= Time.fixedDeltaTime;
        if (IsGrounded)
        {
            wallCoyoteTimer = 0f;
            return;
        }

        int count = rb.GetContacts(contacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = contacts[i];
            if (!contact.enabled || Mathf.Abs(contact.normal.x) < 0.7f)
                continue;

            wallPush = contact.normal.x > 0f ? 1 : -1; // the wall's surface faces us, so push along its normal
            lastWallPush = wallPush;
            wallContactPoint = contact.point;
            wallCoyoteTimer = coyoteTime;
            airJumpsLeft = airJumps;
            return;
        }
    }

    // Turns off collision between this player and the one-way platform under its feet.
    // Only this player is affected; the platform stays solid for everything else.
    private void TryDropThrough()
    {
        int count = rb.GetContacts(groundFilter, groundColliders);
        for (int i = 0; i < count; i++)
        {
            Collider2D platform = groundColliders[i];
            if (platform.GetComponent<PlatformEffector2D>() == null)
                continue; // floor, pillars... are solid

            Physics2D.IgnoreCollision(bodyCollider, platform, true);
            droppingThrough = platform;
            dropTimer = dropThroughTime;
            coyoteTimer = 0f; // dropping is not walking off an edge
            return;
        }
    }

    // Turns the collision back on once the time is up and the player is out of the platform.
    private void UpdateDropThrough()
    {
        if (droppingThrough == null)
            return;

        dropTimer -= Time.fixedDeltaTime;
        if (dropTimer <= 0f && !bodyCollider.Distance(droppingThrough).isOverlapped)
        {
            Physics2D.IgnoreCollision(bodyCollider, droppingThrough, false);
            droppingThrough = null;
        }
    }

    // Speed eases toward the target: fast at first, then slowing down as it gets close, so starting,
    // stopping and turning have no hard corners.
    private void Run()
    {
        float currentSpeed = rb.linearVelocity.x;
        float targetSpeed = moveInput * moveSpeed;

        float sharpness;
        if (moveInput == 0f)
            sharpness = deceleration;                 // released → brake
        else if (moveInput * currentSpeed < 0f)
            sharpness = acceleration + deceleration;  // pressing against current motion → fast turn
        else
            sharpness = acceleration;                 // pressing the way we already move → speed up

        if (!IsGrounded)
            sharpness *= airControl;

        // Right after a wall jump control is 0, then it comes back to full over wallJumpControlTime.
        if (wallJumpTimer > 0f)
            sharpness *= 1f - wallJumpTimer / wallJumpControlTime;

        float newSpeed = Mathf.Lerp(currentSpeed, targetSpeed, 1f - Mathf.Exp(-sharpness * Time.fixedDeltaTime));
        rb.linearVelocity = new Vector2(newSpeed, rb.linearVelocity.y);
    }

    // Order: ground jump (incl. coyote time) → wall jump (incl. wall coyote time) → air jump.
    private bool TryJump()
    {
        if (coyoteTimer > 0f)       // on the ground, or just walked off an edge
        {
            if (IsGrounded)
                SpawnDust(FeetPosition, jumpDustScale); // no dust for a coyote jump: the feet are already in the air

            coyoteTimer = 0f;
            Jump(new Vector2(rb.linearVelocity.x, jumpForce / rb.mass));
            return true;
        }

        if (wallCoyoteTimer > 0f)   // wall jump: up and away from the wall (touching it, or just left it)
        {
            SpawnDust(wallContactPoint, jumpDustScale);
            Jump(new Vector2(lastWallPush * wallJumpPush, wallJumpForce / rb.mass));
            wallJumpTimer = wallJumpControlTime;
            wallCoyoteTimer = 0f;
            rb.gravityScale = baseGravityScale; // stop wall sliding right away
            return true;
        }

        if (airJumpsLeft > 0)       // double jump
        {
            airJumpsLeft--;
            Jump(new Vector2(rb.linearVelocity.x, doubleJumpForce / rb.mass));
            return true;
        }

        return false;
    }

    // Sets the velocity directly (impulse / mass for the upward part) so every jump has the same height,
    // even one started while falling fast.
    private void Jump(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
        canCutJump = true;
        bodyShape = jumpStretch;
        PlaySound(jumpSound, jumpVolume);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip, volume);
    }

    private void SpawnDust(Vector2 position, float scale)
    {
        ParticleSystem dust = Instantiate(dustPuff, position, Quaternion.identity);
        dust.transform.localScale = Vector3.one * scale; // the prefab scales with its transform
    }

    // Eases the body back to its normal shape and keeps its feet in place while squashed/stretched.
    private void UpdateSquashStretch()
    {
        float t = 1f - Mathf.Exp(-squashRecovery * Time.deltaTime); // same speed at any FPS
        bodyShape = Vector2.Lerp(bodyShape, Vector2.one, t);

        body.localScale = new Vector3(bodyBaseScale.x * bodyShape.x, bodyBaseScale.y * bodyShape.y, bodyBaseScale.z);
        body.localPosition = bodyBasePosition + Vector3.down * (bodyHalfHeight * (1f - bodyShape.y));
    }
}
