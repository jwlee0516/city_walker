using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class OriController : MonoBehaviour
{
    [Header("References")]
    public Transform groundCheck;                 // child transform at feet
    public Vector2 groundCheckSize = new Vector2(0.6f, 0.15f);
    public LayerMask groundMask;

    [Header("Movement (Ori-ish)")]
    public float maxRunSpeed = 9.5f;

    // Acceleration-based horizontal motion (the secret sauce)
    public float groundAccel = 85f;
    public float groundDecel = 95f;
    public float airAccel = 70f;
    public float airDecel = 55f;

    // Helps avoid micro-sliding / adds “weight”
    public float groundStickForce = 18f;          // small downward velocity when grounded

    [Header("Jump (forgiving + floaty)")]
    public float jumpHeight = 3.6f;               // meters-ish (depends on gravity scale)
    public float timeToApex = 0.42f;              // longer = floatier
    public float coyoteTime = 0.10f;              // forgiving after leaving ground
    public float jumpBuffer = 0.12f;              // forgiving before landing
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.45f; // release early → reduce upward speed

    [Header("Gravity Shaping (Ori feel)")]
    public float fallGravityMultiplier = 1.9f;    // faster falling
    public float apexGravityMultiplier = 0.55f;   // floaty near apex
    public float apexThreshold = 1.2f;            // smaller = “only very near top”
    public float maxFallSpeed = 22f;

    [Header("Optional: Double Jump")]
    public bool enableDoubleJump = true;
    public float doubleJumpHeight = 3.2f;

    // Runtime
    Rigidbody2D rb;

    float moveInput;
    bool jumpPressed;
    bool jumpReleased;

    bool isGrounded;
    bool wasGrounded;

    float coyoteTimer;
    float jumpBufferTimer;

    bool usedDoubleJump;

    // Derived physics values (computed from jumpHeight + timeToApex)
    float gravity;       // positive magnitude
    float jumpVelocity;  // initial upward velocity
    float doubleJumpVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        RecomputeJumpPhysics();
    }

    void OnValidate()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!rb) return;

        if (timeToApex < 0.05f) timeToApex = 0.05f;
        if (jumpHeight < 0.1f) jumpHeight = 0.1f;
        RecomputeJumpPhysics();
    }

    void RecomputeJumpPhysics()
    {
        // If called from editor before Awake
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!rb) return;
        
        // Using kinematics:
        // gravity = 2H / T^2, jumpVelocity = gravity * T
        gravity = (2f * jumpHeight) / (timeToApex * timeToApex);
        jumpVelocity = gravity * timeToApex;
        doubleJumpVelocity = Mathf.Sqrt(2f * gravity * doubleJumpHeight);

        // Match Rigidbody2D to our gravity (Physics2D.gravity.y is negative)
        // We set rb.gravityScale so that actual gravity magnitude equals `gravity`.
        float worldGravity = Mathf.Abs(Physics2D.gravity.y);
        if (worldGravity < 0.0001f) worldGravity = 9.81f; // safety
        
        rb.gravityScale = gravity / worldGravity;
    }

    void Update()
    {
        // Input (old system). If you use the new Input System, swap this out.
        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump")) jumpPressed = true;
        if (Input.GetButtonUp("Jump")) jumpReleased = true;

        // Jump buffer timer
        if (jumpPressed) jumpBufferTimer = jumpBuffer;
        else jumpBufferTimer -= Time.deltaTime;

        // Ground + coyote timers updated in FixedUpdate (after physics), but we decrement here too
        coyoteTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        wasGrounded = isGrounded;
        isGrounded = CheckGrounded();

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            usedDoubleJump = false;

            // “Stick” to ground so you don’t feel floaty near slopes/edges
            if (rb.linearVelocity.y <= 0f)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -groundStickForce);
        }
        else if (wasGrounded && !isGrounded)
        {
            // just left ground; coyoteTimer already set, will count down
        }

        HandleHorizontal();
        HandleJump();
        ShapeGravity();

        // reset one-frame inputs
        jumpPressed = false;
        jumpReleased = false;
    }

    void HandleHorizontal()
    {
        float targetSpeed = moveInput * maxRunSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;

        bool accelerating = Mathf.Abs(targetSpeed) > 0.01f;

        float accelRate;
        if (isGrounded)
            accelRate = accelerating ? groundAccel : groundDecel;
        else
            accelRate = accelerating ? airAccel : airDecel;

        // Force-based acceleration toward target speed
        float movement = speedDiff * accelRate;

        rb.AddForce(new Vector2(movement, 0f), ForceMode2D.Force);

        // Optional clamp to keep it stable with different masses
        rb.linearVelocity = new Vector2(Mathf.Clamp(rb.linearVelocity.x, -maxRunSpeed, maxRunSpeed),
                                        rb.linearVelocity.y);
    }

    void HandleJump()
    {
        bool canCoyoteJump = coyoteTimer > 0f;

        // If buffered jump exists, consume it when we can
        if (jumpBufferTimer > 0f)
        {
            if (canCoyoteJump)
            {
                DoJump(jumpVelocity);
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
            }
            else if (enableDoubleJump && !usedDoubleJump && !isGrounded)
            {
                DoJump(doubleJumpVelocity);
                usedDoubleJump = true;
                jumpBufferTimer = 0f;
            }
        }

        // Variable jump height: releasing jump cuts upward velocity
        if (jumpReleased && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }
    }

    void DoJump(float vel)
    {
        // Reset vertical velocity so jump is consistent (Ori does this a lot)
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * vel, ForceMode2D.Impulse);
    }

    void ShapeGravity()
    {
        float vy = rb.linearVelocity.y;

        // Apex hang: near top of jump, gravity is reduced -> floaty control
        if (vy > 0f && Mathf.Abs(vy) < apexThreshold)
        {
            rb.gravityScale = (gravity / Mathf.Abs(Physics2D.gravity.y)) * apexGravityMultiplier;
        }
        // Falling: stronger gravity -> snappy landing (Ori signature)
        else if (vy < 0f)
        {
            rb.gravityScale = (gravity / Mathf.Abs(Physics2D.gravity.y)) * fallGravityMultiplier;
        }
        else
        {
            rb.gravityScale = gravity / Mathf.Abs(Physics2D.gravity.y);
        }

        // Clamp fall speed
        if (vy < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    bool CheckGrounded()
    {
        // Box overlap at feet (more stable than a ray)
        if (groundCheck == null) return false;

        Collider2D hit = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundMask);
        return hit != null;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
}