using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem; // Tambahkan namespace ini

public class DragonController : MonoBehaviour
{
    // --- Status Terbang ---
    public bool isFlightActivated { get; private set; } = false;

    // --- Pengaturan Kecepatan ---
    [Header("Flight Settings")]
    [SerializeField] public float forwardSpeed = 15f;
    [SerializeField] private float brakeSpeed = 5f;
    [SerializeField] private float reverseSpeed = -3f;
    [SerializeField] private float acceleration = 2f;
    [SerializeField] private float deceleration = 1f;
    [SerializeField] private float brakeAcceleration = 5f;
    [SerializeField] private float speedLossOnClimb = 1.0f;
    [SerializeField] private float speedGainOnDive = 1.5f;
    [SerializeField] private float maxDiveSpeedMultiplier = 1.5f;

    [field: SerializeField] public float CurrentSpeed { get; private set; }

    [Header("Vertical Movement Settings")]
    [SerializeField] private float minVerticalSpeed = 3f;
    [SerializeField] private float maxVerticalSpeedFactor = 0.5f;
    [SerializeField] private float verticalRampUpTime = 1.5f;
    [SerializeField] private float verticalRampDownSpeed = 5f;
    [SerializeField] private float verticalInertia = 3f;
    private float verticalInputHoldTime = 0f;
    private float calculatedVerticalSpeed;
    private float currentVerticalSpeed = 0f;

    [Header("Rotation Settings")]
    [SerializeField] private float yawSpeed = 90f;

    // Pengaturan untuk Rotasi VISUAL (Pitch & Roll)
    [Header("Visual Rotation Settings")]
    [SerializeField] private float visualPitchAngle = 30f;
    [SerializeField] private float visualRollAngle = 45f;
    [SerializeField] private float visualRotationSmoothness = 10f;
    [SerializeField] private float visualRotationRampUpTime = 1.0f;
    [SerializeField] private float minVisualRotationFactor = 0.2f;
    [SerializeField] private float visualRotationReturnSpeed = 8f;
    [SerializeField] private float visualRotationInertia = 4f;

    // Pengaturan Kamera (Hanya FOV di sini)
    [Header("Camera Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float defaultCameraFOV = 60f;
    [SerializeField] private float maxCameraFOV = 75f;
    [SerializeField] private float cameraFOVTransitionSpeed = 5f;

    // Event yang dipanggil saat naga dinonaktifkan (misal, mendarat)
    public UnityEvent onDragonDeactivated;

    // Referensi ke GameObject visual naga
    private Transform dragonVisualsTransform;

    // Variabel untuk melacak target pitch dan roll saat ini
    private Quaternion currentVisualLocalRotation;
    private float currentTargetPitch = 0f;
    private float currentTargetRoll = 0f;

    // --- VARIABEL BARU UNTUK INPUT ACTIONS ---
    private PlayerInputActions playerInputActions;
    private Vector2 moveInput;
    private float verticalMovementRawInput; // Untuk Space/LControl

    void Awake()
    {
        dragonVisualsTransform = transform.Find("DragonVisuals");
        if (dragonVisualsTransform == null)
        {
            Debug.LogError("DragonController: Tidak menemukan GameObject 'DragonVisuals' sebagai child. Pastikan Anda sudah membuatnya!");
            dragonVisualsTransform = this.transform;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("DragonController: Main Camera tidak ditemukan! Pastikan ada Camera di scene dengan tag 'MainCamera'.");
            }
        }

        // --- INISIALISASI INPUT ACTIONS ---
        playerInputActions = new PlayerInputActions();

        // Bind aksi Move (untuk horizontal dan vertikal pergerakan naga)
        playerInputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        playerInputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Bind aksi Climb (Space)
        playerInputActions.Player.Jump.started += ctx => verticalMovementRawInput = 1f;
        playerInputActions.Player.Jump.canceled += ctx => verticalMovementRawInput = 0f;

        // Bind aksi Dive (LeftControl)
        playerInputActions.Player.Crouch.started += ctx => verticalMovementRawInput = -1f;
        playerInputActions.Player.Crouch.canceled += ctx => verticalMovementRawInput = 0f;
    }

    void OnEnable()
    {
        playerInputActions.Enable();
    }

    void OnDisable()
    {
        playerInputActions.Disable();
    }

    void Start()
    {
        isFlightActivated = false;
        CurrentSpeed = 0f;
        verticalInputHoldTime = 0f;
        currentVerticalSpeed = 0f;
        if (dragonVisualsTransform != null)
        {
            dragonVisualsTransform.localRotation = Quaternion.identity;
        }
        currentVisualLocalRotation = Quaternion.identity;

        if (mainCamera != null)
        {
            mainCamera.fieldOfView = defaultCameraFOV;
        }
    }

    public void ActivateFlight()
    {
        if (!isFlightActivated)
        {
            isFlightActivated = true;
            CurrentSpeed = 0f;
            verticalInputHoldTime = 0f;
            currentVerticalSpeed = 0f;
            if (dragonVisualsTransform != null)
            {
                dragonVisualsTransform.localRotation = Quaternion.identity;
            }
            currentVisualLocalRotation = Quaternion.identity;
            Debug.Log(gameObject.name + ": Penerbangan diaktifkan!");
        }
    }

    public void DeactivateFlight()
    {
        if (isFlightActivated)
        {
            isFlightActivated = false;
            CurrentSpeed = 0f;
            verticalInputHoldTime = 0f;
            currentVerticalSpeed = 0f;
            Debug.Log(gameObject.name + ": Penerbangan dinonaktifkan.");
            onDragonDeactivated.Invoke();
            if (dragonVisualsTransform != null)
            {
                dragonVisualsTransform.localRotation = Quaternion.identity;
            }
            currentVisualLocalRotation = Quaternion.identity;

            if (mainCamera != null)
            {
                mainCamera.fieldOfView = defaultCameraFOV;
            }
        }
    }

    void Update()
    {
        if (isFlightActivated)
        {
            HandleFlightInput();
            UpdateCameraFOV();

            transform.parent.position = transform.position;
            transform.parent.rotation = transform.rotation;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }

    void HandleFlightInput()
    {
        // --- MENGGUNAKAN INPUT ACTIONS UNTUK MOVEMENT VERTIKAL (FORWARD/BACKWARD) ---
        float verticalInput = moveInput.y;

        float targetBaseSpeed;
        if (verticalInput > 0)
        {
            targetBaseSpeed = forwardSpeed;
        }
        else if (verticalInput < 0)
        {
            targetBaseSpeed = brakeSpeed;
        }
        else
        {
            targetBaseSpeed = 0f;
        }

        if (verticalInput > 0)
        {
            CurrentSpeed = Mathf.Min(CurrentSpeed + acceleration * Time.deltaTime, forwardSpeed);
        }
        else if (verticalInput < 0)
        {
            if (CurrentSpeed > 0)
            {
                CurrentSpeed = Mathf.Max(CurrentSpeed - brakeAcceleration * Time.deltaTime, 0f);
            }
            else
            {
                CurrentSpeed = Mathf.Max(CurrentSpeed - acceleration * Time.deltaTime, reverseSpeed);
            }
        }
        else
        {
            if (CurrentSpeed > 0)
            {
                CurrentSpeed = Mathf.Max(CurrentSpeed - deceleration * Time.deltaTime, 0f);
            }
            else if (CurrentSpeed < 0)
            {
                CurrentSpeed = Mathf.Min(CurrentSpeed + deceleration * Time.deltaTime, 0f);
            }
        }

        float normalizedVerticalMovement = currentVerticalSpeed / (forwardSpeed * maxVerticalSpeedFactor * 3f);
        normalizedVerticalMovement = Mathf.Clamp(normalizedVerticalMovement, -1f, 1f);

        if (normalizedVerticalMovement > 0.1f)
        {
            CurrentSpeed -= speedLossOnClimb * normalizedVerticalMovement * Time.deltaTime;
        }
        else if (normalizedVerticalMovement < -0.1f)
        {
            CurrentSpeed += speedGainOnDive * Mathf.Abs(normalizedVerticalMovement) * Time.deltaTime;
            CurrentSpeed = Mathf.Min(CurrentSpeed, forwardSpeed * maxDiveSpeedMultiplier);
        }

        if (verticalInput == 0)
        {
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, 0f, forwardSpeed);
        }
        else if (CurrentSpeed < reverseSpeed && verticalInput >= 0)
        {
            CurrentSpeed = Mathf.Lerp(CurrentSpeed, 0f, Time.deltaTime * deceleration);
        }

        transform.Translate(Vector3.forward * CurrentSpeed * Time.deltaTime, Space.Self);

        // --- MENGGUNAKAN INPUT ACTIONS UNTUK MOVEMENT HORIZONTAL (YAW) ---
        float horizontalInput = moveInput.x;
        float yawAmount = horizontalInput * yawSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up, yawAmount, Space.Self);

        // --- MENGGUNAKAN VARIABEL BARU UNTUK VERTICAL MOVEMENT (Climb/Dive) ---
        if (verticalMovementRawInput != 0)
        {
            verticalInputHoldTime += Time.deltaTime;
            verticalInputHoldTime = Mathf.Clamp(verticalInputHoldTime, 0f, verticalRampUpTime);
        }
        else
        {
            verticalInputHoldTime -= Time.deltaTime * (verticalRampUpTime / verticalRampDownSpeed);
            verticalInputHoldTime = Mathf.Max(verticalInputHoldTime, 0f);
        }

        float verticalRampFactor = Mathf.Clamp01(verticalInputHoldTime / verticalRampUpTime);

        float normalizedCurrentSpeed = Mathf.InverseLerp(0, forwardSpeed, Mathf.Abs(CurrentSpeed));

        float currentSpeedInfluence = normalizedCurrentSpeed * (maxVerticalSpeedFactor * forwardSpeed - minVerticalSpeed);
        float baseVerticalSpeed = minVerticalSpeed + currentSpeedInfluence;

        float targetVerticalSpeed = baseVerticalSpeed * (1f + verticalRampFactor * 2f);
        targetVerticalSpeed = Mathf.Clamp(targetVerticalSpeed, minVerticalSpeed, forwardSpeed * maxVerticalSpeedFactor * 3f);

        float lerpSpeedVertical = verticalRampDownSpeed;
        if (Mathf.Sign(verticalMovementRawInput) != Mathf.Sign(currentVerticalSpeed) && currentVerticalSpeed != 0)
        {
            lerpSpeedVertical = verticalInertia;
        }

        currentVerticalSpeed = Mathf.Lerp(currentVerticalSpeed, verticalMovementRawInput * targetVerticalSpeed, Time.deltaTime * lerpSpeedVertical);

        transform.Translate(Vector3.up * currentVerticalSpeed * Time.deltaTime, Space.World);

        if (dragonVisualsTransform != null)
        {
            float visualRotationRampFactorPitch = Mathf.Clamp01(verticalInputHoldTime / visualRotationRampUpTime);

            float horizontalInputHoldTime = 0f;
            if (horizontalInput != 0) // Menggunakan horizontalInput dari moveInput
            {
                horizontalInputHoldTime += Time.deltaTime;
                horizontalInputHoldTime = Mathf.Clamp(horizontalInputHoldTime, 0f, visualRotationRampUpTime);
            }
            else
            {
                horizontalInputHoldTime -= Time.deltaTime * (visualRotationRampUpTime / visualRotationReturnSpeed);
                horizontalInputHoldTime = Mathf.Max(horizontalInputHoldTime, 0f);
            }
            float rollRampFactor = Mathf.Clamp01(horizontalInputHoldTime / visualRotationRampUpTime);

            float speedInfluenceFactor = Mathf.Lerp(minVisualRotationFactor, 1f, normalizedCurrentSpeed);

            float calculatedPitchAngle = visualPitchAngle * speedInfluenceFactor * (1f + visualRotationRampFactorPitch * 0.5f);
            float calculatedRollAngle = visualRollAngle * speedInfluenceFactor * (1f + rollRampFactor * 0.5f);

            float lerpSpeedVisualPitch = visualRotationSmoothness;
            if (Mathf.Sign(-verticalMovementRawInput) != Mathf.Sign(currentTargetPitch) && currentTargetPitch != 0)
            {
                lerpSpeedVisualPitch = visualRotationInertia;
            }
            currentTargetPitch = Mathf.Lerp(currentTargetPitch, -verticalMovementRawInput * calculatedPitchAngle, Time.deltaTime * lerpSpeedVisualPitch);

            float lerpSpeedVisualRoll = visualRotationSmoothness;
            if (Mathf.Sign(-horizontalInput) != Mathf.Sign(currentTargetRoll) && currentTargetRoll != 0)
            {
                lerpSpeedVisualRoll = visualRotationInertia;
            }
            currentTargetRoll = Mathf.Lerp(currentTargetRoll, -horizontalInput * calculatedRollAngle, Time.deltaTime * lerpSpeedVisualRoll);

            currentTargetPitch = Mathf.Clamp(currentTargetPitch, -90f, 90f);
            currentTargetRoll = Mathf.Clamp(currentTargetRoll, -90f, 90f);

            Quaternion targetVisualRotation = Quaternion.Euler(currentTargetPitch, 0f, currentTargetRoll);

            dragonVisualsTransform.localRotation = Quaternion.Slerp(
                dragonVisualsTransform.localRotation,
                targetVisualRotation,
                visualRotationSmoothness * Time.deltaTime
            );
        }
    }

    // --- Fungsi Update Kamera (Hanya FOV) ---
    void UpdateCameraFOV()
    {
        if (mainCamera == null) return;

        float normalizedSpeed = Mathf.InverseLerp(0, forwardSpeed, Mathf.Abs(CurrentSpeed));

        float targetFOV = Mathf.Lerp(defaultCameraFOV, maxCameraFOV, normalizedSpeed);
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * cameraFOVTransitionSpeed);
    }
}