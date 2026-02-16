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
    
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool jumpPressed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Called by PlayerInput (Send Messages) for action named "Move"
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
        Debug.Log("MOVE: " + moveInput);
    }

    // Called by PlayerInput (Send Messages) for action named "Jump"
    public void OnJump(InputValue value)
    {
        // value.isPressed is true on press, false on release
        if(value.isPressed) jumpPressed = true;
    }

    private void FixedUpdate()
    {
        // Horizontal movement
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
        // Jump
        if (jumpPressed && IsGrounded())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        jumpPressed = false;
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
