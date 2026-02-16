using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent (typeof (Rigidbody2D))]
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
    public bool dashKeepsVerticalVelocity = false; // if true, preserve y velocity
    
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool jumpPressed;

    private bool dashPressed;
    private bool isDashing;
    private float dashTimeLeft;
    private float dashCooldownLeft;

    private float defaultGravityScale;
    private float lastFacingX = 1f; // tracks facing direction for dash
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravityScale = rb.gravityScale;
    }

    // Called by PlayerInput (Send Messages) for action named "Move"
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
        // Update facing direction when player gives horizontal input
        if (Mathf.Abs(moveInput.x) > 0.01f)
            lastFacingX = Mathf.Sign(moveInput.x);
    }

    // Called by PlayerInput (Send Messages) for action named "Jump"
    public void OnJump(InputValue value)
    {
        // value.isPressed is true on press, false on release
        if(value.isPressed) jumpPressed = true;
    }
    
    // Input System: action "Dash"
    public void OnDash(InputValue value)
    {
        if (value.isPressed)
            dashPressed = true;
    }
    
    private void Update()
    {
        // cooldown timer in Update so it feels responsive even if physics timestep is lower
        if (dashCooldownLeft > 0f)
            dashCooldownLeft -= Time.deltaTime;
    }
    private void FixedUpdate()
    {
        // If currently dashing, override movement
        if (isDashing)
        {
            dashTimeLeft -= Time.fixedDeltaTime;

            float yVel = dashKeepsVerticalVelocity ? rb.linearVelocity.y : 0f;
            rb.linearVelocity = new Vector2(lastFacingX * dashSpeed, yVel);

            if (dashTimeLeft <= 0f)
            {
                EndDash();
            }

            // Consume inputs for this frame
            jumpPressed = false;
            dashPressed = false;
            return;
        }
        // Horizontal movement
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
        
        // Jump (only when grounded)
        if (jumpPressed && IsGrounded())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        jumpPressed = false;
        
        // Dash start
        if (dashPressed && dashCooldownLeft <= 0f)
        {
            StartDash();
        }

        // consume one-frame buttons
        jumpPressed = false;
        dashPressed = false;
    }
    
    private void StartDash()
    {
        isDashing = true;
        dashTimeLeft = dashDuration;
        dashCooldownLeft = dashCooldown;

        // Optional: turn off gravity during dash so it feels “snappy”
        rb.gravityScale = 0f;

        // If you want dash to always go in input direction when held:
        if (Mathf.Abs(moveInput.x) > 0.01f)
            lastFacingX = Mathf.Sign(moveInput.x);
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = defaultGravityScale;

        // When dash ends, keep some horizontal momentum or immediately return to move speed:
        // This version just keeps current x velocity; your normal movement will take over next frame.
    }

    private bool IsGrounded()
    {
        if (groundCheck ==  null) return false;
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void OnDrawGizmosSelected()
    {
       if (groundCheck == null) return;
       Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
