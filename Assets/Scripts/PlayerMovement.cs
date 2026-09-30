using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;

// [MovedFrom] remapea las referencias del script viejo "CharacterController" a este,
// para que prefabs/escenas no queden en "Missing (Mono Script)".
[MovedFrom(true, null, null, "CharacterController")]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Input (bindings editables aquí mismo)")]
    [SerializeField] private InputAction moveAction = new InputAction("Move", InputActionType.Value);
    [SerializeField] private InputAction jumpAction = new InputAction("Jump", InputActionType.Button);
    [SerializeField] private InputAction dashAction = new InputAction("Dash", InputActionType.Button);

    [Header("Movimiento")]
    [SerializeField, Min(0f)] private float walkSpeed = 8f;
    [SerializeField, Min(0f)] private float acceleration = 60f;
    [SerializeField, Min(0f)] private float deceleration = 70f;
    [SerializeField, Min(0f)] private float airAcceleration = 40f;
    [SerializeField, Min(0f)] private float airDeceleration = 30f;
    [Tooltip("Multiplicador de velocidad horizontal en el pico del salto")]
    [SerializeField, Min(1f)] private float apexBonus = 1.2f;

    [Header("Salto")]
    [SerializeField, Min(0f)] private float jumpForce = 14f;
    [Tooltip("Saltos extra en el aire (0 = sin doble salto)")]
    [SerializeField, Min(0)] private int maxJumps = 1;
    [SerializeField, Min(0f)] private float coyoteTime = 0.1f;
    [SerializeField, Min(0f)] private float jumpBuffer = 0.1f;
    [Tooltip("Qué tanto se corta la velocidad al soltar el botón de salto")]
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f;

    [Header("Dash")]
    [SerializeField, Min(0f)] private float dashForce = 20f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.2f;
    [SerializeField, Min(0f)] private float dashCooldown = 0.5f;

    [Header("Ground Check")]
    [Tooltip("Si se deja vacío se usa la posición del jugador")]
    [SerializeField] private Transform groundCheck;
    [SerializeField, Min(0f)] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    // Estado público (por si otros scripts lo leen)
    [HideInInspector] public bool canMove = true;
    [HideInInspector] public bool facingRight = true;
    [HideInInspector] public Vector2 moveInput;

    private Rigidbody2D rb;
    private Vector2 velocity = Vector2.zero;

    private bool isGrounded;
    private bool wasGrounded;
    private int jumpCount = 0;
    private bool consumedGroundJump = false;
    private float timeLeftGrounded = -1f;
    private float lastJumpPressedTime = -1f;

    private bool isDashing;
    private float dashTime;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    private float dashSpeed;
    private float originalGravityScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravityScale = rb.gravityScale;

        SetupDefaultBindings();
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
    }

    // Si en el Inspector no hay bindings, se ponen unos por defecto (teclado + gamepad)
    private void SetupDefaultBindings()
    {
        if (moveAction.bindings.Count == 0)
        {
            moveAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            moveAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick/x");
        }

        if (jumpAction.bindings.Count == 0)
        {
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
        }

        if (dashAction.bindings.Count == 0)
        {
            dashAction.AddBinding("<Keyboard>/leftShift");
            dashAction.AddBinding("<Gamepad>/rightTrigger");
        }
    }

    void Update()
    {
        if (!canMove) return;

        // Input
        moveInput = new Vector2(moveAction.ReadValue<float>(), 0f);
        if (jumpAction.WasPressedThisFrame()) lastJumpPressedTime = Time.time;
        if (jumpAction.WasReleasedThisFrame()) CutJump();
        if (dashAction.WasPressedThisFrame()) TryDash();

        // Ground check
        Vector2 checkPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
        isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundLayer);

        // Flip
        if (moveInput.x > 0 && !facingRight) Flip();
        else if (moveInput.x < 0 && facingRight) Flip();

        if (wasGrounded && !isGrounded)
            timeLeftGrounded = Time.time;

        // Reset de saltos solo al aterrizar
        if (isGrounded && !wasGrounded)
        {
            jumpCount = maxJumps;
            consumedGroundJump = false;
        }

        wasGrounded = isGrounded;

        bool wantsToJump = lastJumpPressedTime > 0 && Time.time < lastJumpPressedTime + jumpBuffer;

        if (wantsToJump && !isDashing)
        {
            // Salto desde suelo o coyote time (no gasta jumpCount)
            if (!consumedGroundJump && (isGrounded || (coyoteTime > 0f && Time.time < timeLeftGrounded + coyoteTime)))
            {
                Jump();
                consumedGroundJump = true;
                lastJumpPressedTime = -1f;
            }
            // Saltos en el aire
            else if (!isGrounded && jumpCount > 0)
            {
                Jump();
                jumpCount--;
                lastJumpPressedTime = -1f;
            }
        }

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (!canMove) return;

        if (isDashing)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = dashDirection * dashSpeed;

            // Frenado progresivo del dash
            dashSpeed = Mathf.MoveTowards(dashSpeed, 0f, dashForce / dashDuration * Time.fixedDeltaTime);

            dashTime -= Time.fixedDeltaTime;
            if (dashTime <= 0f || dashSpeed <= 0.1f)
                EndDash();

            return;
        }

        // Apex modifier: más velocidad horizontal en el pico del salto
        float apexModifier = 1f;
        if (!isGrounded && jumpForce > 0f)
        {
            float yVelocity = rb.linearVelocity.y;
            apexModifier = Mathf.Lerp(1f, apexBonus, 1f - Mathf.Abs(yVelocity) / jumpForce);
        }

        float targetSpeed = moveInput.x * walkSpeed * apexModifier;
        float accel = isGrounded ? acceleration : airAcceleration;
        float decel = isGrounded ? deceleration : airDeceleration;

        if (moveInput.x == 0)
            velocity.x = Mathf.MoveTowards(rb.linearVelocity.x, 0f, decel * Time.fixedDeltaTime);
        else
            velocity.x = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accel * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector2(velocity.x, rb.linearVelocity.y);
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private void CutJump()
    {
        if (rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
    }

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    private void TryDash()
    {
        if (isDashing || dashCooldownTimer > 0f) return;

        isDashing = true;
        dashTime = dashDuration;
        dashCooldownTimer = dashCooldown;

        if (moveInput.x != 0)
            dashDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
        else
            dashDirection = new Vector2(facingRight ? 1f : -1f, 0f);

        dashSpeed = dashForce;
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = originalGravityScale;
    }

    public bool IsGrounded() => isGrounded;

    // Dibuja el círculo del ground check en la escena
    void OnDrawGizmosSelected()
    {
        Vector3 pos = groundCheck != null ? groundCheck.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos, groundCheckRadius);
    }
}