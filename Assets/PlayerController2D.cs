using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Dash (Burst Velocity)")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.12f;
    public float dashCooldown = 0.35f;
    public bool dashKeepsVerticalVelocity = false;

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
        if (value.isPressed)
            jumpPressed = true;
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

        // If dashing, override everything
        if (isDashing)
        {
            dashTimeLeft -= Time.fixedDeltaTime;

            float yVel = dashKeepsVerticalVelocity ? rb.linearVelocity.y : 0f;
            rb.linearVelocity = new Vector2(lastFacingX * dashSpeed, yVel);

            if (dashTimeLeft <= 0f)
                EndDash();

            jumpPressed = false;
            dashPressed = false;
            return;
        }

        // WALL SLIDE / CLIMB state
        bool pressingIntoWall = (wallSide != 0) && (moveInput.x * wallSide > 0.1f);
        // Explanation: if wall is on left (wallSide=-1), pressing into it means moveInput.x < 0
        
        // Only slide/climb if:
        // - not grounded
        // - touching wall
        // - falling or trying to cling
        // - not in wall-jump lockout
        isWallSliding = !grounded && isWallTouching && wallJumpLockLeft <= 0f && pressingIntoWall && rb.linearVelocity.y <= 0.1f;

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

            // Wall Jump (from slide/cling)
            if (jumpPressed)
            {
                DoWallJump();
            }

            jumpPressed = false;
            dashPressed = false;
            return;
        }
        else
        {
            rb.gravityScale = defaultGravityScale;
        }

        // NORMAL MOVE
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);

        // NORMAL JUMP
        if (jumpPressed && grounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // DASH
        if (dashPressed && dashCooldownLeft <= 0f)
        {
            StartDash();
        }

        jumpPressed = false;
        dashPressed = false;
    }

    private void DoWallJump()
    {
        // Determine which side the wall is on:
        // If facing right and touching wall, wall is on right => jump left (negative)
        float jumpDirX = -wallSide; // jump away from the wall you are touching

        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = new Vector2(jumpDirX * wallJumpX, wallJumpY);

        // Flip facing direction to the jump direction for consistency
        lastFacingX = Mathf.Sign(jumpDirX);

        // Prevent instantly re-sticking to wall
        wallJumpLockLeft = wallJumpLockTime;

        // exit wall slide
        isWallSliding = false;
        jumpPressed = false;
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimeLeft = dashDuration;
        dashCooldownLeft = dashCooldown;

        rb.gravityScale = 0f;

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
        if (wallCheckLeft != null)
            Gizmos.DrawWireSphere(wallCheckLeft.position, wallCheckRadius);
        if (wallCheckRight != null)
            Gizmos.DrawWireSphere(wallCheckRight.position, wallCheckRadius);
    }
}
