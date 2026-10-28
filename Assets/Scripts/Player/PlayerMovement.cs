using UnityEngine;
using UnityEngine.InputSystem;

// Rigidbody2D platformer movement: A/D to run, Space to jump (plus a double jump in the air), Shift to dash in any of
// 8 directions (WASD; up and diagonals help get back to the map),
// S to drop through one-way platforms, and wall slide / wall jump when touching a wall in the air.
// Horizontal speed eases toward the target speed every physics step: it reacts right away,
// then settles gently at full speed or at a stop, so there are no hard corners in the motion.
// Pressing into a wall whose top is just above the feet pops the player up onto it (ledge assist), and being hit
// gives the air jumps back, so a player knocked off the map can get back.
// Falling is heavier than rising and has a top speed; holding S in the air falls faster. Rising into the corner of a
// block slides around it instead of bumping the head. Jump buffer and coyote time forgive early / late presses.
// Every jump (ground, wall and double jump) plays the jump sound; only hard landings play the landing sound.
// The body's squash, stretch and bounce are PlayerBodyAnimator's (it plays on every machine).
// Cores (CoreBridge on the player, optional) can add air jumps, unlock a Shift dash and react to hard landings.
// Only runs on the machine that controls this player (PlayerNetwork turns it off elsewhere).
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Run")]
    [Tooltip("Max horizontal speed (units/sec).")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("How quickly speed follows A/D (higher = snappier). Speed eases in: fast at first, gentle near full speed.")]
    [SerializeField] private float acceleration = 22f;

    [Tooltip("How quickly you stop after releasing A/D (higher = shorter slide). Also how quickly you brake onto a wall.")]
    [SerializeField] private float deceleration = 30f;

    [Tooltip("Share of the ground acceleration/deceleration kept in the air. 0 = no air control, 1 = same as on the ground.")]
    [Range(0f, 1f)]
    [SerializeField] private float airControl = 0.8f;

    [Tooltip("Share of the ground acceleration/deceleration kept on ice (a slippery LevelBlock). Low = long slides.")]
    [Range(0f, 1f)]
    [SerializeField] private float iceGrip = 0.12f;

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
    [SerializeField] private float fallGravityMultiplier = 1.6f;

    [Tooltip("Falling never gets faster than this (units/sec).")]
    [SerializeField] private float maxFallSpeed = 22f;

    [Tooltip("Holding S in the air falls faster: gravity multiplier and top speed while doing it.")]
    [Min(1f)]
    [SerializeField] private float fastFallGravityMultiplier = 2.4f;
    [SerializeField] private float maxFastFallSpeed = 30f;

    [Tooltip("A ground jump (or wall jump) still works this long after leaving the ground (or wall) (seconds).")]
    [SerializeField] private float coyoteTime = 0.12f;

    [Tooltip("Space pressed this long before landing still jumps on landing (seconds).")]
    [Min(0.01f)]
    [SerializeField] private float jumpBufferTime = 0.13f;

    [Tooltip("Rising into the corner of a block above: slide around it by up to this much sideways instead of " +
             "bumping the head (units).")]
    [SerializeField] private float cornerCorrection = 0.3f;

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

    [Tooltip("In the air, pressing into a wall whose top is at most this far above the feet pops the player up " +
             "onto it instead of sliding down (units; 0 = off).")]
    [SerializeField] private float ledgeAssistHeight = 0.6f;

    [Header("Dash (Shift, everybody; the Dash+ core makes it better)")]
    [Tooltip("Shift dashes this fast toward WASD (8 directions; no keys = the way you last moved). Not down " +
             "into the ground (units/sec).")]
    [SerializeField] private float dashSpeed = 22f;

    [Tooltip("Seconds a dash lasts (with the Dash+ core the player can't be hurt meanwhile).")]
    [SerializeField] private float dashTime = 0.15f;

    [Tooltip("Seconds from one dash to the next.")]
    [SerializeField] private float dashCooldown = 1.2f;

    [Tooltip("Seconds from one dash to the next with the Dash+ core.")]
    [SerializeField] private float upgradedDashCooldown = 0.6f;

    [Tooltip("Speed kept when a dash ends (it then eases back to running or falling), so it doesn't stop dead.")]
    [SerializeField] private float dashExitSpeed = 11f;

    [Header("Dust")]
    [Tooltip("Dust puff spawned when jumping off the ground or a wall, and when landing. It destroys itself.")]
    [SerializeField] private ParticleSystem dustPuff;

    [Tooltip("Size of the dust puff when jumping off the ground or a wall.")]
    [SerializeField] private float jumpDustScale = 0.8f;

    [Tooltip("Size of the dust puff after a hard landing. Softer landings make smaller puffs.")]
    [SerializeField] private float landDustScale = 1.3f;

    [Tooltip("Running near full speed on the ground leaves a small puff this often (seconds; 0 = never).")]
    [SerializeField] private float runDustInterval = 0.22f;
    [SerializeField] private float runDustScale = 0.35f;

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
    private CoreBridge cores;
    private PlayerHealth health;
    private PlayerNetwork net;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;
    private bool dashRequested;
    private float dashTimer;         // > 0 while dashing
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    private int facing = 1;          // the way the player last moved: 1 = right, -1 = left
    private float baseGravityScale;
    private bool frozen;             // stood still between rounds (gravity off)

    private float moveInput;        // -1 = left, 0 = none, 1 = right
    private float moveUpInput;      // -1 = down (S), 0 = none, 1 = up (W)
    private bool downHeld;          // S / down
    private bool jumpHeld;
    private float jumpBufferTimer;  // > 0: Space was pressed recently and is waiting to be used
    private float coyoteTimer;      // > 0: a ground jump is still allowed
    private int airJumpsLeft;
    private bool canCutJump;        // true while rising from a jump
    private float lastVelocityY;    // vertical speed in the previous physics step (how hard we land)
    private float slowTimer;        // > 0 while slowed down (e.g. a Chill core hit)
    private float slowFactor = 1f;

    private int wallPush;               // touching a wall in the air right now: +1 = a wall jump pushes right (wall on the left), -1 = left, 0 = none
    private int lastWallPush;           // the last wall touched, used by wall coyote time
    private Vector2 wallContactPoint;
    private float wallCoyoteTimer;      // > 0: a wall jump is still allowed (touching a wall, or just left one)
    private float wallJumpTimer;        // > 0 right after a wall jump: A/D control is coming back
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];

    private Collider2D droppingThrough; // one-way platform we are currently falling through
    private float dropTimer;
    private readonly Collider2D[] groundColliders = new Collider2D[4];
    private bool onIce;
    private Vector2 groundVelocity; // how the ground under the feet moves (moving platform, conveyor, crate)
    private float runDustTimer;
    private readonly RaycastHit2D[] ceilingHits = new RaycastHit2D[4];
    private readonly Collider2D[] ledgeHits = new Collider2D[4];
    private ContactFilter2D solidFilter; // everything but players and triggers (corner correction)

    // Grounded = touching any collider whose surface faces up (floor, top of a platform, ...).
    private ContactFilter2D groundFilter;

    private Vector2 FeetPosition => new Vector2(rb.position.x, bodyCollider.bounds.min.y);

    // Air jumps per landing, plus the ones cores give.
    private int TotalAirJumps => airJumps + (cores != null ? cores.ExtraAirJumps : 0);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        cores = GetComponent<CoreBridge>();        // this player's own cores (null → none)
        health = GetComponent<PlayerHealth>();     // null → a dash gives no protection
        net = GetComponent<PlayerNetwork>();       // null → no dash ghosts for the other machines
        baseGravityScale = rb.gravityScale;

        // Actions come from the project-wide input asset: Assets/InputSystem_Actions.inputactions
        moveAction = InputSystem.actions.FindAction("Player/Move");
        jumpAction = InputSystem.actions.FindAction("Player/Jump");
        dashAction = InputSystem.actions.FindAction("Player/Sprint"); // Left Shift

        groundFilter.SetNormalAngle(45f, 135f);
        solidFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer) & ~(1 << gameObject.layer));
        solidFilter.useTriggers = false;
    }

    private void Update()
    {
        // Read input every frame, apply it in FixedUpdate (physics). The ESC menu or the chat takes the keys while open.
        bool controls = !InputLock.Blocked && !InputLock.Frozen;
        Vector2 move = controls ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        moveInput = move.x;
        moveUpInput = Mathf.Abs(move.y) > 0.5f ? Mathf.Sign(move.y) : 0f;
        downHeld = move.y < -0.5f;
        jumpHeld = controls && jumpAction.IsPressed();

        if (controls && jumpAction.WasPressedThisFrame())
            jumpBufferTimer = jumpBufferTime;
        if (controls && dashAction != null && dashAction.WasPressedThisFrame())
            dashRequested = true;
        if (moveInput != 0f)
            facing = moveInput > 0f ? 1 : -1;
    }

    private void FixedUpdate()
    {
        // Between the rounds of a LAN match: frozen in place (no falling to death once the round is decided).
        if (InputLock.Frozen)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            frozen = true;
            dashTimer = 0f;
            dashRequested = false;
            jumpBufferTimer = 0f;
            return;
        }
        Unfreeze();

        UpdateGrounded();
        UpdateWall();

        // Dash: straight along its direction, no gravity, nothing else happens meanwhile.
        dashCooldownTimer -= Time.fixedDeltaTime;
        if (dashRequested)
        {
            dashRequested = false;
            if (dashCooldownTimer <= 0f && dashTimer <= 0f)
                StartDash();
        }
        if (dashTimer > 0f)
        {
            dashTimer -= Time.fixedDeltaTime;
            rb.gravityScale = 0f;
            // The last step of the dash keeps only some of its speed, then running eases it back.
            rb.linearVelocity = dashDirection * (dashTimer > 0f ? dashSpeed : dashExitSpeed);
            lastVelocityY = rb.linearVelocity.y;
            return;
        }

        wallJumpTimer -= Time.fixedDeltaTime;
        slowTimer -= Time.fixedDeltaTime;
        Run();
        LedgeAssist();

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

        // Never fall faster than the top speed (holding S allows a faster one).
        float topFallSpeed = FastFalling ? maxFastFallSpeed : maxFallSpeed;
        if (rb.linearVelocity.y < -topFallSpeed && !sliding)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -topFallSpeed);

        CorrectCorner();
        RunDust();
        lastVelocityY = rb.linearVelocity.y;
    }

    // Gravity back after the freeze. Also when this component is turned off meanwhile (a travel, a death), or the
    // player would float away with no gravity.
    private void Unfreeze()
    {
        if (!frozen)
            return;
        frozen = false;
        rb.gravityScale = baseGravityScale;
    }

    private void OnDisable() => Unfreeze();

    // Holding S in the air on the way down (not on a wall).
    private bool FastFalling => downHeld && !IsGrounded && wallPush == 0 && rb.linearVelocity.y < 0f;

    // Rising with the head about to hit the corner of a block: slide around it a little instead of bumping.
    // One-way platforms let the player through anyway, so only solid blocks count.
    private void CorrectCorner()
    {
        if (rb.linearVelocity.y <= 0f || cornerCorrection <= 0f)
            return;
        float distance = rb.linearVelocity.y * Time.fixedDeltaTime + 0.02f;
        if (!HeadBlocked(Vector2.zero, distance))
            return;
        for (float shift = 0.05f; shift <= cornerCorrection; shift += 0.05f)
            foreach (int side in new[] { -1, 1 })
            {
                var offset = new Vector2(side * shift, 0f);
                if (!HeadBlocked(offset, distance) && !HeadBlocked(offset, 0.01f))
                {
                    rb.position += offset;
                    return;
                }
            }
    }

    // In the air, pressing into a wall whose top is just above the feet: a little hop that clears it, instead of
    // sliding down past it. Running on toward the wall then carries the player onto the top.
    private void LedgeAssist()
    {
        if (IsGrounded || wallPush == 0 || moveInput * wallPush >= 0f || rb.linearVelocity.y > 2f || ledgeAssistHeight <= 0f)
            return;
        Bounds bounds = bodyCollider.bounds;
        Vector2 size = bounds.size * 0.95f;
        var inward = new Vector2(-wallPush * 0.15f, 0f); // a little into the wall: is there room on top of it?
        for (float lift = 0.05f; lift <= ledgeAssistHeight; lift += 0.05f)
        {
            if (Physics2D.OverlapBox((Vector2)bounds.center + inward + Vector2.up * lift, size, 0f, solidFilter, ledgeHits) > 0)
                continue;
            float gravity = Physics2D.gravity.magnitude * baseGravityScale * fallGravityMultiplier;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Sqrt(2f * gravity * (lift + 0.15f)));
            canCutJump = false;
            return;
        }
    }

    private bool HeadBlocked(Vector2 offset, float distance)
    {
        Bounds bounds = bodyCollider.bounds;
        var origin = (Vector2)bounds.center + offset;
        int count = Physics2D.BoxCast(origin, bounds.size * new Vector2(0.95f, 1f), 0f, Vector2.up, solidFilter, ceilingHits, distance);
        for (int i = 0; i < count; i++)
            if (ceilingHits[i].collider != bodyCollider && ceilingHits[i].normal.y < -0.5f &&
                ceilingHits[i].collider.GetComponent<PlatformEffector2D>() == null)
                return true;
        return false;
    }

    // A little dust under the feet while running fast on the ground.
    private void RunDust()
    {
        runDustTimer -= Time.fixedDeltaTime;
        bool running = IsGrounded && Mathf.Abs(rb.linearVelocity.x - groundVelocity.x) > moveSpeed * 0.85f;
        if (!running || runDustInterval <= 0f || runDustTimer > 0f)
            return;
        runDustTimer = runDustInterval;
        SpawnDust(FeetPosition, runDustScale);
    }

    // Gravity for this step: none while wall sliding, lighter near the top of a jump while Space is held
    // (a short float that rounds off the arc), heavier while falling, normal otherwise.
    private float GravityScale(bool sliding)
    {
        if (sliding)
            return 0f;

        float verticalSpeed = rb.linearVelocity.y;
        if (FastFalling)
            return baseGravityScale * fastFallGravityMultiplier;
        if (!IsGrounded && jumpHeld && Mathf.Abs(verticalSpeed) < apexHangSpeed)
            return baseGravityScale * apexGravityMultiplier;
        if (verticalSpeed < 0f)
            return baseGravityScale * fallGravityMultiplier;
        return baseGravityScale;
    }

    private void UpdateGrounded()
    {
        bool wasGrounded = IsGrounded;

        // What the feet stand on: ice, and how that ground moves (we move with it).
        onIce = false;
        groundVelocity = Vector2.zero;
        int count = rb.GetContacts(groundFilter, groundColliders);
        for (int i = 0; i < count; i++)
        {
            Collider2D ground = groundColliders[i];
            if (ground.attachedRigidbody != null && ground.attachedRigidbody != rb)
                groundVelocity = ground.attachedRigidbody.GetPointVelocity(FeetPosition);
            LevelBlock block = ground.GetComponent<LevelBlock>();
            if (block != null)
            {
                onIce |= block.Slippery;
                groundVelocity.x += block.ConveyorSpeed;
            }
        }

        // Not while moving up (faster than the ground does), so the step right after a jump never counts as
        // "still on the ground".
        IsGrounded = count > 0 && rb.linearVelocity.y - groundVelocity.y <= 0.01f;
        if (!IsGrounded)
            groundVelocity = Vector2.zero;

        if (IsGrounded)
        {

            coyoteTimer = coyoteTime;
            airJumpsLeft = TotalAirJumps;

            if (!wasGrounded)
            {
                // Landing: dust, bigger the faster we were falling (full at 20 units/sec). The squash is
                // PlayerBodyAnimator's.
                float fallSpeed = -lastVelocityY;
                float impact = Mathf.InverseLerp(2f, 20f, fallSpeed);
                if (impact > 0f)
                    SpawnDust(FeetPosition, landDustScale * Mathf.Lerp(0.5f, 1f, impact));

                // Sound only for hard landings (double jump, jumping down from above), louder the harder.
                if (fallSpeed >= hardLandingSpeed)
                {
                    PlaySound(landSound, landVolume * Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(hardLandingSpeed, hardLandingSpeed + 8f, fallSpeed)));
                    CRTScreen.Blur(FeetPosition, 0.3f);
                    if (cores != null)
                        cores.OnHardLanding(FeetPosition);
                }
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
            airJumpsLeft = TotalAirJumps;
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
    // Speeds are measured against the ground, so a moving platform or a conveyor carries the player along.
    private void Run()
    {
        float currentSpeed = rb.linearVelocity.x - groundVelocity.x;
        float targetSpeed = moveInput * moveSpeed * (slowTimer > 0f ? slowFactor : 1f);

        float sharpness;
        if (moveInput == 0f)
            sharpness = deceleration;                 // released → brake
        else if (moveInput * currentSpeed < 0f)
            sharpness = acceleration + deceleration;  // pressing against current motion → fast turn
        else
            sharpness = acceleration;                 // pressing the way we already move → speed up

        if (!IsGrounded)
            sharpness *= airControl;
        else if (onIce)
            sharpness *= iceGrip;

        // Right after a wall jump control is 0, then it comes back to full over wallJumpControlTime.
        if (wallJumpTimer > 0f)
            sharpness *= 1f - wallJumpTimer / wallJumpControlTime;

        float newSpeed = Mathf.Lerp(currentSpeed, targetSpeed, 1f - Mathf.Exp(-sharpness * Time.fixedDeltaTime));
        float verticalSpeed = rb.linearVelocity.y;
        if (groundVelocity.y < 0f && jumpBufferTimer <= 0f)
            verticalSpeed = groundVelocity.y; // stay on a platform going down instead of hopping behind it
        rb.linearVelocity = new Vector2(newSpeed + groundVelocity.x, verticalSpeed);
    }

    // Order: ground jump (incl. coyote time) → wall jump (incl. wall coyote time) → air jump.
    private bool TryJump()
    {
        if (coyoteTimer > 0f)       // on the ground, or just walked off an edge
        {
            if (IsGrounded)
                SpawnDust(FeetPosition, jumpDustScale); // no dust for a coyote jump: the feet are already in the air

            coyoteTimer = 0f;
            Jump(new Vector2(rb.linearVelocity.x, jumpForce / rb.mass + Mathf.Max(0f, groundVelocity.y))); // a rising platform adds its speed
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

    // The dash can be used again right away (e.g. Void Feast).
    public void ResetDash() => dashCooldownTimer = 0f;

    // Hit by someone: all the air jumps come back, to fight the push.
    public void RefreshAirJumps() => airJumpsLeft = Mathf.Max(airJumpsLeft, TotalAirJumps);

    // Runs slower for a while (factor 0.5 = half speed), e.g. hit by a Chill bullet.
    public void Slow(float factor, float duration)
    {
        slowFactor = factor;
        slowTimer = Mathf.Max(slowTimer, duration);
    }

    private void StartDash()
    {
        float x = Mathf.Abs(moveInput) > 0.3f ? Mathf.Sign(moveInput) : 0f;
        float y = IsGrounded ? Mathf.Max(0f, moveUpInput) : moveUpInput; // never down into the ground
        dashDirection = x == 0f && y == 0f ? new Vector2(facing, 0f) : new Vector2(x, y).normalized;
        dashTimer = dashTime;
        bool upgraded = cores != null && cores.DashUpgraded;
        dashCooldownTimer = upgraded ? upgradedDashCooldown : dashCooldown;
        canCutJump = false;
        SpawnDust(FeetPosition, jumpDustScale);
        CRTScreen.Blur(rb.position, 0.25f);
        PlaySound(jumpSound, jumpVolume);
        if (upgraded && health != null)
            health.MakeInvulnerable(dashTime + 0.05f); // Dash+: can't be hurt while dashing
        if (net != null)
            net.PlayDash(dashTime); // the ghosts, on every machine
    }

    // Sets the velocity directly (impulse / mass for the upward part) so every jump has the same height,
    // even one started while falling fast.
    private void Jump(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
        canCutJump = true;
        PlaySound(jumpSound, jumpVolume);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        GameAudio.Play(clip, volume, pitchVariation); // only the machine that controls this player plays its steps
    }

    private void SpawnDust(Vector2 position, float scale)
    {
        ParticleSystem dust = Instantiate(dustPuff, position, Quaternion.identity);
        dust.transform.localScale = Vector3.one * scale; // the prefab scales with its transform
    }
}
