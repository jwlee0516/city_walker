using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Run Feel")]
    public float maxRunSpeed = 9f;
    public float groundAccel = 85f;
    public float groundDecel = 110f;
    public float airAccel = 55f;
    public float airDecel = 25f;

    [Header("Jump Feel")]
    public float jumpVelocity = 14f;
    public float fallGravityMultiplier = 2.6f;   // stronger gravity when falling
    public float lowJumpMultiplier = 2.2f;       // stronger gravity if jump released early
    public float maxFallSpeed = 28f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Dash (Burst Velocity)")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.12f;
    public float dashCooldown = 0.35f;
    public bool dashKeepsVerticalVelocity = true;

    [Tooltip("If 0, dash keeps normal gravity. If 0.3, dash has reduced gravity. If 0, recommended for less floaty feel.")]
    public float dashGravityMultiplier = 0f;

    [Header("Wall")]
    public Transform wallCheckLeft;
    public Transform wallCheckRight;
    public float wallCheckRadius = 0.18f;
    public float wallSlideSpeed = 2.5f;

    public bool enableWallClimb = true;
    public float wallClimbSpeed = 4.5f;

    [Header("Wall Jump")]
    public float wallJumpX = 10f;
    public float wallJumpY = 12f;
    public float wallJumpLockTime = 0.18f;

    private int wallSide = 0; // -1 = wall on left, +1 = wall on right, 0 = none

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private bool jumpPressed;
    private bool jumpHeld;     // NEW: for short hop / variable jump height
    private bool dashPressed;

    private bool isDashing;
    private float dashTimeLeft;
    private float dashCooldownLeft;

    private float defaultGravityScale;
    private float lastFacingX = 1f;

    private bool isWallTouching;
    private bool isWallSliding;
    private float wallJumpLockLeft;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravityScale = rb.gravityScale;
    }

    // Input System: "Move"
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();

        if (Mathf.Abs(moveInput.x) > 0.01f)
            lastFacingX = Mathf.Sign(moveInput.x);
    }

    // Input System: "Jump"
    public void OnJump(InputValue value)
    {
        // pressed edge
        if (value.isPressed)
            jumpPressed = true;

        // held state for short-hop
        jumpHeld = value.isPressed;
    }

    // Input System: "Dash"
    public void OnDash(InputValue value)
    {
        if (value.isPressed)
            dashPressed = true;
    }

    private void Update()
    {
        if (dashCooldownLeft > 0f) dashCooldownLeft -= Time.deltaTime;
        if (wallJumpLockLeft > 0f) wallJumpLockLeft -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        bool grounded = IsGrounded();
        isWallTouching = IsTouchingWall();

        // -------------------------
        // DASH state (burst velocity)
        // -------------------------
        if (isDashing)
        {
            dashTimeLeft -= Time.fixedDeltaTime;

            // Gravity during dash: 0 = normal, 0.3 = reduced, etc.
            rb.gravityScale = defaultGravityScale * dashGravityMultiplier;

            float yVel = dashKeepsVerticalVelocity ? rb.linearVelocity.y : 0f;
            rb.linearVelocity = new Vector2(lastFacingX * dashSpeed, yVel);

            if (dashTimeLeft <= 0f)
                EndDash();

            jumpPressed = false;
            dashPressed = false;
            return;
        }

        // -------------------------
        // WALL SLIDE / CLIMB
        // -------------------------
        bool pressingIntoWall = (wallSide != 0) && (moveInput.x * wallSide > 0.1f);

        isWallSliding =
            !grounded &&
            isWallTouching &&
            wallJumpLockLeft <= 0f &&
            pressingIntoWall &&
            rb.linearVelocity.y <= 0.1f;

        if (isWallSliding)
        {
            // If holding UP and climb enabled, climb; otherwise slide down slowly
            if (enableWallClimb && moveInput.y > 0.1f)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = new Vector2(0f, wallClimbSpeed);
            }
            else
            {
                rb.gravityScale = defaultGravityScale;
                float newY = Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed);
                rb.linearVelocity = new Vector2(0f, newY);
            }

            if (jumpPressed)
                DoWallJump();

            jumpPressed = false;
            dashPressed = false;
            return;
        }

        // -------------------------
        // NORMAL RUN (accel/decel)
        // -------------------------
        float targetSpeed = moveInput.x * maxRunSpeed;

        float accelRate;
        if (Mathf.Abs(targetSpeed) > 0.01f)
            accelRate = grounded ? groundAccel : airAccel;
        else
            accelRate = grounded ? groundDecel : airDecel;

        float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);

        // -------------------------
        // NORMAL JUMP
        // -------------------------
        if (jumpPressed && grounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
        }
        jumpPressed = false;

        // -------------------------
        // BETTER JUMP GRAVITY SHAPING (Mario/Ori-ish)
        // -------------------------
        float y = rb.linearVelocity.y;

        if (y < 0f)
        {
            // falling fast
            rb.gravityScale = defaultGravityScale * fallGravityMultiplier;
        }
        else if (y > 0f && !jumpHeld)
        {
            // short hop if you released jump
            rb.gravityScale = defaultGravityScale * lowJumpMultiplier;
        }
        else
        {
            rb.gravityScale = defaultGravityScale;
        }

        // clamp fall speed (prevents absurd terminal velocity)
        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);

        // -------------------------
        // DASH trigger
        // -------------------------
        if (dashPressed && dashCooldownLeft <= 0f)
            StartDash();

        dashPressed = false;
    }

    private void DoWallJump()
    {
        float jumpDirX = -wallSide; // jump away from wall

        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = new Vector2(jumpDirX * wallJumpX, wallJumpY);

        lastFacingX = Mathf.Sign(jumpDirX);
        wallJumpLockLeft = wallJumpLockTime;

        isWallSliding = false;
        jumpPressed = false;
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimeLeft = dashDuration;
        dashCooldownLeft = dashCooldown;

        if (Mathf.Abs(moveInput.x) > 0.01f)
            lastFacingX = Mathf.Sign(moveInput.x);
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = defaultGravityScale;
    }

    private bool IsGrounded()
    {
        if (groundCheck == null) return false;
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private bool IsTouchingWall()
    {
        wallSide = 0;

        if (wallCheckLeft != null &&
            Physics2D.OverlapCircle(wallCheckLeft.position, wallCheckRadius, groundLayer))
            wallSide = -1;

        if (wallCheckRight != null &&
            Physics2D.OverlapCircle(wallCheckRight.position, wallCheckRadius, groundLayer))
            wallSide = +1;

        return wallSide != 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

        if (wallCheckLeft != null)
            Gizmos.DrawWireSphere(wallCheckLeft.position, wallCheckRadius);

        if (wallCheckRight != null)
            Gizmos.DrawWireSphere(wallCheckRight.position, wallCheckRadius);
    }
}