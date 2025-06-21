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
    private float currentVerticalSpeed = 0f;

    [Header("Rotation Settings")]
    [SerializeField] private float yawSpeed = 90f; // Kecepatan putar YAW Player (global)

    // PENTING: Referensi ke GameObject VISUAL naga (child dari GameObject Dragon ini)
    [Header("Visual Rotation Settings")]
    [SerializeField] private Transform dragonVisualsTransform; // Seret GameObject DragonVisuals ke sini di Inspector!
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

    // Variabel untuk melacak target pitch dan roll saat ini
    private float currentTargetPitch = 0f;
    private float currentTargetRoll = 0f;

    // Referensi ke GameObject Player (Root)
    private Transform playerRootTransform; // Akan diisi saat ActivateFlight

    // --- VARIABEL UNTUK INPUT ACTIONS ---
    private PlayerInputActions playerInputActions;
    private float throttleInput;        // Input dari aksi 'Throttle' (Analog kiri Y / W/S)
    private float yawInput;             // Input dari aksi 'Yaw' (Analog kiri X / A/D)
    private float verticalFlightInput;  // Input dari aksi 'VerticalFlight' (Analog kanan Y / Space/LeftControl)


    void Awake()
    {
        // Mencoba mencari DragonVisuals sebagai child dari GameObject ini (Dragon)
        if (dragonVisualsTransform == null)
        {
            dragonVisualsTransform = transform.Find("DragonVisuals");
            if (dragonVisualsTransform == null)
            {
                Debug.LogError("DragonController: 'DragonVisualsTransform' belum diatur atau tidak ditemukan sebagai child bernama 'DragonVisuals'!" +
                               "Seret GameObject model naga Anda (yang melakukan rotasi visual) ke slot ini di Inspector.", this);
            }
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("DragonController: Main Camera tidak ditemukan! Pastikan ada Camera di scene dengan tag 'MainCamera'.");
            }
        }

        playerInputActions = new PlayerInputActions();

        // Bind aksi dari Action Map 'DragonFlight'
        playerInputActions.DragonFlight.Throttle.performed += ctx => throttleInput = ctx.ReadValue<float>();
        playerInputActions.DragonFlight.Throttle.canceled += ctx => throttleInput = 0f;

        playerInputActions.DragonFlight.Yaw.performed += ctx => yawInput = ctx.ReadValue<float>();
        playerInputActions.DragonFlight.Yaw.canceled += ctx => yawInput = 0f;

        playerInputActions.DragonFlight.VerticalFlight.performed += ctx => verticalFlightInput = ctx.ReadValue<float>();
        playerInputActions.DragonFlight.VerticalFlight.canceled += ctx => verticalFlightInput = 0f;

        playerInputActions.DragonFlight.ToggleFlight.performed += ctx => ToggleFlight();
    }

    void OnEnable()
    {
        // DragonController ini aktif saat naga dinaiki.
        // Di sini kita pastikan playerRootTransform sudah diatur jika OnEnable dipanggil setelah parenting.
        if (transform.parent != null)
        {
            playerRootTransform = transform.parent;
        }
    }

    void OnDisable()
    {
        playerInputActions.DragonFlight.Disable();
    }

    void Start()
    {
        // DragonController dimulai dalam keadaan non-aktif saat game dimulai
        isFlightActivated = false;
        CurrentSpeed = 0f;
        verticalInputHoldTime = 0f;
        currentVerticalSpeed = 0f;
        if (dragonVisualsTransform != null)
        {
            dragonVisualsTransform.localRotation = Quaternion.identity;
        }
        currentTargetPitch = 0f;
        currentTargetRoll = 0f;

        if (mainCamera != null)
        {
            mainCamera.fieldOfView = defaultCameraFOV;
        }

        // Nonaktifkan DragonController ini secara default saat Start
        enabled = false;
    }

    // Dipanggil oleh PlayerInteraction saat naga dinaiki
    public void ActivateFlight(Transform playerRoot)
    {
        if (!isFlightActivated)
        {
            isFlightActivated = true;
            enabled = true; // Aktifkan script ini
            playerRootTransform = playerRoot; // Simpan referensi ke Player root

            playerInputActions.DragonFlight.Enable(); // Aktifkan Action Map DragonFlight

            CurrentSpeed = 0f;
            verticalInputHoldTime = 0f;
            currentVerticalSpeed = 0f;
            if (dragonVisualsTransform != null)
            {
                dragonVisualsTransform.localRotation = Quaternion.identity; // Reset visual naga
            }
            currentTargetPitch = 0f;
            currentTargetRoll = 0f;
            Debug.Log(gameObject.name + ": Penerbangan diaktifkan!");
        }
    }

    // Dipanggil oleh ToggleFlight atau PlayerInteraction saat naga dinonaktifkan
    public void DeactivateFlight()
    {
        if (isFlightActivated)
        {
            isFlightActivated = false;
            enabled = false; // Nonaktifkan script ini
            playerInputActions.DragonFlight.Disable(); // Nonaktifkan Action Map DragonFlight

            CurrentSpeed = 0f;
            verticalInputHoldTime = 0f;
            currentVerticalSpeed = 0f;
            Debug.Log(gameObject.name + ": Penerbangan dinonaktifkan.");
            onDragonDeactivated.Invoke(); // Panggil event untuk PlayerInteraction

            if (dragonVisualsTransform != null)
            {
                dragonVisualsTransform.localRotation = Quaternion.identity; // Reset visual naga
            }
            currentTargetPitch = 0f;
            currentTargetRoll = 0f;

            if (mainCamera != null)
            {
                mainCamera.fieldOfView = defaultCameraFOV;
            }

            playerRootTransform = null; // Hapus referensi Player root
        }
    }

    private void ToggleFlight()
    {
        if (isFlightActivated)
        {
            DeactivateFlight();
        }
        // else
        // {
        //     // Jangan aktifkan flight dari sini, hanya dari PlayerInteraction
        //     // Ini untuk mencegah naga tiba-tiba terbang tanpa interaksi yang benar
        // }
    }

    void Update()
    {
        if (playerRootTransform == null)
        {
            Debug.LogError("DragonController: Player Root Transform belum diatur! Tidak bisa menggerakkan naga.", this);
            DeactivateFlight(); // Kembali ke kondisi non-terbang
            return;
        }

        HandleFlightInput();
        UpdateCameraFOV();
    }

    void HandleFlightInput()
    {
        // --- MENGGERAKKAN PLAYER (ROOT) ---

        // Throttle (Maju/Mundur/Rem)
        float throttleAxis = throttleInput;

        if (throttleAxis > 0)
        {
            CurrentSpeed = Mathf.Min(CurrentSpeed + acceleration * Time.deltaTime, forwardSpeed);
        }
        else if (throttleAxis < 0)
        {
            if (CurrentSpeed > 0)
            {
                CurrentSpeed = Mathf.Max(CurrentSpeed + throttleAxis * brakeAcceleration * Time.deltaTime, 0f);
            }
            else
            {
                CurrentSpeed = Mathf.Max(CurrentSpeed + throttleAxis * acceleration * Time.deltaTime, reverseSpeed);
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

        if (throttleAxis == 0)
        {
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, 0f, forwardSpeed);
        }
        else if (CurrentSpeed < reverseSpeed && throttleAxis >= 0)
        {
            CurrentSpeed = Mathf.Lerp(CurrentSpeed, 0f, Time.deltaTime * deceleration);
        }

        // --- Rotasi Yaw (Belok Kiri/Kanan) pada PLAYER (GLOBAL ROTATION) ---
        float horizontalInput = yawInput;
        float yawAmount = horizontalInput * yawSpeed * Time.deltaTime;
        playerRootTransform.Rotate(Vector3.up, yawAmount, Space.Self); // Putar GameObject Player (Root)

        // --- Pergerakan Maju pada PLAYER (ROOT) ---
        playerRootTransform.Translate(Vector3.forward * CurrentSpeed * Time.deltaTime, Space.Self); // Gerakkan GameObject Player (Root)

        // --- Pergerakan Vertikal pada PLAYER (ROOT) ---
        if (verticalFlightInput != 0)
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
        float normalizedCurrentFlightSpeed = Mathf.InverseLerp(0, forwardSpeed, Mathf.Abs(CurrentSpeed));
        float currentSpeedInfluence = normalizedCurrentFlightSpeed * (maxVerticalSpeedFactor * forwardSpeed - minVerticalSpeed);
        float baseVerticalSpeed = minVerticalSpeed + currentSpeedInfluence;
        float targetVerticalSpeed = baseVerticalSpeed * (1f + verticalRampFactor * 2f);
        targetVerticalSpeed = Mathf.Clamp(targetVerticalSpeed, minVerticalSpeed, forwardSpeed * maxVerticalSpeedFactor * 3f);

        float lerpSpeedVertical = verticalRampDownSpeed;
        if (Mathf.Sign(verticalFlightInput) != Mathf.Sign(currentVerticalSpeed) && currentVerticalSpeed != 0)
        {
            lerpSpeedVertical = verticalInertia;
        }

        currentVerticalSpeed = Mathf.Lerp(currentVerticalSpeed, verticalFlightInput * targetVerticalSpeed, Time.deltaTime * lerpSpeedVertical);

        playerRootTransform.Translate(Vector3.up * currentVerticalSpeed * Time.deltaTime, Space.World); // Gerakkan GameObject Player (Root) secara vertikal

        // --- VISUAL ROTATION (Pitch & Roll) pada DRAGONVISUALS ---
        if (dragonVisualsTransform != null)
        {
            // Pitch (dari input VerticalFlight)
            float visualRotationRampFactorPitch = Mathf.Clamp01(verticalInputHoldTime / visualRotationRampUpTime);
            float speedInfluenceFactor = Mathf.Lerp(minVisualRotationFactor, 1f, normalizedCurrentFlightSpeed);
            float calculatedPitchAngle = visualPitchAngle * speedInfluenceFactor * (1f + visualRotationRampFactorPitch * 0.5f);

            float lerpSpeedVisualPitch = visualRotationSmoothness;
            if (Mathf.Sign(-verticalFlightInput) != Mathf.Sign(currentTargetPitch) && currentTargetPitch != 0)
            {
                lerpSpeedVisualPitch = visualRotationInertia;
            }
            currentTargetPitch = Mathf.Lerp(currentTargetPitch, -verticalFlightInput * calculatedPitchAngle, Time.deltaTime * lerpSpeedVisualPitch);

            // Roll (dari input Yaw)
            float horizontalInputForRoll = yawInput;
            float horizontalInputHoldTime = 0f;
            if (horizontalInputForRoll != 0)
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
            float calculatedRollAngle = visualRollAngle * speedInfluenceFactor * (1f + rollRampFactor * 0.5f);

            float lerpSpeedVisualRoll = visualRotationSmoothness;
            if (Mathf.Sign(-horizontalInputForRoll) != Mathf.Sign(currentTargetRoll) && currentTargetRoll != 0)
            {
                lerpSpeedVisualRoll = visualRotationInertia;
            }
            currentTargetRoll = Mathf.Lerp(currentTargetRoll, -horizontalInputForRoll * calculatedRollAngle, Time.deltaTime * lerpSpeedVisualRoll);

            currentTargetPitch = Mathf.Clamp(currentTargetPitch, -90f, 90f);
            currentTargetRoll = Mathf.Clamp(currentTargetRoll, -90f, 90f);

            // Terapkan rotasi VISUAL LOKAL pada DragonVisualsTransform
            Quaternion targetVisualRotation = Quaternion.Euler(currentTargetPitch, 0f, currentTargetRoll);

            dragonVisualsTransform.localRotation = Quaternion.Slerp(
                dragonVisualsTransform.localRotation,
                targetVisualRotation,
                visualRotationSmoothness * Time.deltaTime
            );
        }
    }

    void UpdateCameraFOV()
    {
        if (mainCamera == null) return;

        float normalizedSpeed = Mathf.InverseLerp(0, forwardSpeed, Mathf.Abs(CurrentSpeed));

        float targetFOV = Mathf.Lerp(defaultCameraFOV, maxCameraFOV, normalizedSpeed);
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * cameraFOVTransitionSpeed);
    }
}