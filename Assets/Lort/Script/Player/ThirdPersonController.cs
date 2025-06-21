using UnityEngine;
using UnityEngine.InputSystem; // Tambahkan namespace ini

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float crouchSpeed = 1.5f;
    public float rotationSpeed = 500f; // Kecepatan rotasi karakter

    [Header("Jump Settings")]
    public float jumpHeight = 2f;
    public float gravity = -9.81f; // Nilai gravitasi standar

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    // Untuk penanganan Crouch obstacle
    [Header("Crouch Obstacle Check")]
    public Transform ceilingCheck; // Titik untuk mengecek langit-langit di atas karakter
    public float ceilingCheckRadius = 0.2f; // Radius sphere check untuk langit-langit
    public LayerMask obstacleMask; // Layer objek yang bisa menghalangi saat berdiri

    private CharacterController controller;
    private Vector3 moveDirection;
    private float yVelocity = 0f; // Untuk gravitasi dan lompat
    private bool isGrounded;
    public bool isCrouching = false;

    private float originalControllerHeight;
    private Vector3 originalControllerCenter;

    private Animator animator;

    // --- VARIABEL BARU UNTUK INPUT ACTIONS ---
    private PlayerInputActions playerInputActions;
    private Vector2 moveInput;
    private bool isRunningInput;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("Animator component not found on character. Movement animations will not play.");
        }

        originalControllerHeight = controller.height;
        originalControllerCenter = controller.center;

        // --- INISIALISASI INPUT ACTIONS ---
        playerInputActions = new PlayerInputActions();

        // Bind aksi Move
        playerInputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        playerInputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Bind aksi Jump
        playerInputActions.Player.Jump.performed += OnJump;

        // Bind aksi Run
        playerInputActions.Player.Run.started += ctx => isRunningInput = true;
        playerInputActions.Player.Run.canceled += ctx => isRunningInput = false;

        // Bind aksi Crouch
        playerInputActions.Player.Crouch.performed += OnCrouch;
    }

    void OnEnable()
    {
        playerInputActions.Enable();
    }

    void OnDisable()
    {
        playerInputActions.Disable();
    }

    void Update()
    {
        // Ground Check
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && yVelocity < 0)
        {
            yVelocity = -2f;
        }

        // --- MENGGUNAKAN INPUT ACTIONS UNTUK MOVEMENT ---
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + Camera.main.transform.eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, rotationSpeed * Time.deltaTime);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            float currentSpeed = walkSpeed;
            if (isCrouching)
            {
                currentSpeed = crouchSpeed;
            }
            // --- MENGGUNAKAN INPUT ACTIONS UNTUK RUN ---
            else if (isRunningInput)
            {
                currentSpeed = runSpeed;
            }
            moveDirection = moveDir.normalized * currentSpeed;
        }
        else
        {
            moveDirection = Vector3.zero;
        }

        yVelocity += gravity * Time.deltaTime;
        moveDirection.y = yVelocity;

        controller.Move(moveDirection * Time.deltaTime);

        if (animator != null)
        {
            float currentMoveSpeed = new Vector3(controller.velocity.x, 0, controller.velocity.z).magnitude;
            animator.SetFloat("Speed", currentMoveSpeed / runSpeed);
            animator.SetBool("IsGrounded", isGrounded);
            // Animator IsCrouching diatur di OnCrouch
        }
    }

    // --- METHOD BARU UNTUK JUMP (DIPANGGIL DARI INPUT ACTIONS) ---
    private void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded && !isCrouching)
        {
            yVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }
    }

    // --- METHOD BARU UNTUK CROUCH (DIPANGGIL DARI INPUT ACTIONS) ---
    private void OnCrouch(InputAction.CallbackContext context)
    {
        if (isCrouching) // Jika sedang menunduk dan mencoba berdiri
        {
            bool ceilingBlocked = Physics.CheckSphere(ceilingCheck.position, ceilingCheckRadius, obstacleMask);

            if (!ceilingBlocked) // Jika tidak ada halangan di atas
            {
                isCrouching = false; // Boleh berdiri
                controller.height = originalControllerHeight;
                controller.center = originalControllerCenter;
                if (animator != null)
                {
                    animator.SetBool("IsCrouching", isCrouching);
                }
            }
        }
        else // Jika sedang berdiri dan mencoba menunduk
        {
            isCrouching = true; // Langsung menunduk
            controller.height = originalControllerHeight / 2f;
            controller.center = originalControllerCenter - new Vector3(0, originalControllerHeight / 4f, 0);
            if (animator != null)
            {
                animator.SetBool("IsCrouching", isCrouching);
            }
        }
    }

    // Fungsi untuk membantu visualisasi GroundCheck dan CeilingCheck di Scene View
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(groundCheck.position, groundDistance);
        }
        if (ceilingCheck != null)
        {
            Gizmos.color = Color.blue; // Warna biru untuk ceiling check
            Gizmos.DrawSphere(ceilingCheck.position, ceilingCheckRadius);
        }
    }
}