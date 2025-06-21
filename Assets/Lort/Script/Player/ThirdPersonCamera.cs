using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform playerTarget; // Seret GameObject Player ke sini di Inspector!
    public Transform currentCameraTarget; // Akan diatur secara dinamis oleh PlayerInteraction
    public DragonController dragonController; // Referensi ke DragonController di scene

    [Header("Camera Settings - Ground")]
    public float groundCameraDistance = 5f;
    public float groundCameraHeight = 2f;
    public float groundCameraSpeed = 10f;
    [SerializeField] private float groundRotationSpeed = 3f;

    [Header("Camera Settings - Flight")]
    public float flightCameraDistance = 10f;
    public float flightCameraHeight = 3f;
    public float flightCameraSmoothSpeed = 8f;
    public float flightRotationSpeed = 15f; // KECEPATAN ROTASI KAMERA SAAT KEMBALI KE DEPAN
    [Range(0f, 45f)] public float maxFlightLookYawAngle = 25f; // Batasan putar kiri/kanan saat terbang
    [Range(0f, 45f)] public float maxFlightLookPitchAngle = 25f; // Batasan putar atas/bawah saat terbang
    [SerializeField] private float flightLookSensitivity = 1f; // Sensitivitas input look saat terbang

    [Header("Speed-Based Distance")]
    public float minSpeedDistance = 5f;
    public float maxSpeedDistance = 15f;
    public float speedDistanceTransitionSpeed = 5f;

    [Header("Obstruction Handling")]
    public LayerMask obstructionMask;
    public float zoomSpeed = 5f;
    public float minObstructionDistance = 1f;
    public float maxObstructionDistance = 7f;

    // --- DEKLARASI VARIABEL ANGGOTA (MEMBER VARIABLES) ---
    private float currentEffectiveDistance; // Dideklarasikan di sini
    private float currentX = 0.0f; // Rotasi Y kamera (yaw)
    private float currentY = 0.0f; // Rotasi X kamera (pitch)

    private PlayerInputActions playerInputActions;
    private Vector2 lookInput;
    // --- AKHIR DEKLARASI VARIABEL ANGGOTA ---


    void Awake()
    {
        if (playerTarget == null)
        {
            Debug.LogError("ThirdPersonCamera: Player Target not assigned! Please assign the Player Transform to the target slot in the Inspector.");
            enabled = false;
            return;
        }

        // Awalnya, target kamera adalah pemain itu sendiri
        currentCameraTarget = playerTarget;

        // Cari DragonController di scene, karena dia ada di GameObject Dragon terpisah
        dragonController = FindObjectOfType<DragonController>();
        if (dragonController == null)
        {
            Debug.LogWarning("ThirdPersonCamera: DragonController not found in scene. Speed-based camera distance and flight camera will not work as expected.");
        }

        // Inisialisasi currentEffectiveDistance di Awake
        currentEffectiveDistance = groundCameraDistance;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        playerInputActions = new PlayerInputActions();

        // Bind Look input untuk kedua Action Map (Player dan DragonFlight)
        playerInputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        playerInputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        playerInputActions.DragonFlight.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        playerInputActions.DragonFlight.Look.canceled += ctx => lookInput = Vector2.zero;
    }

    void OnEnable()
    {
        playerInputActions.Player.Enable();
        // DragonFlight Look akan diaktifkan/dinonaktifkan oleh PlayerInteraction / DragonController
        // Sebaiknya DragonFlight.Look diaktifkan/dinonaktifkan di PlayerInteraction juga
        // agar konsisten dengan Action Map lainnya.
    }

    void OnDisable()
    {
        playerInputActions.Player.Disable();
        playerInputActions.DragonFlight.Disable(); // Pastikan dinonaktifkan saat kamera dinonaktifkan
    }

    // Fungsi publik untuk mengatur target kamera oleh PlayerInteraction
    public void SetCurrentCameraTarget(Transform newTarget)
    {
        currentCameraTarget = newTarget;
        // Tidak perlu mereset currentX dan currentY di sini secara paksa
        // Biarkan logika HandleFlightCameraInput/HandleGroundCameraInput yang mengatur
        // agar transisi kamera lebih halus (kembali ke tengah secara smooth).
    }

    void LateUpdate()
    {
        if (currentCameraTarget == null) return;

        float targetBaseDistance;
        float targetHeight;
        float currentLerpSpeed;

        // Tentukan apakah kita dalam mode terbang atau tidak
        bool inFlightMode = (dragonController != null && dragonController.isFlightActivated);

        if (inFlightMode)
        {
            HandleFlightCameraInput();
            targetBaseDistance = flightCameraDistance;
            targetHeight = flightCameraHeight;
            currentLerpSpeed = flightCameraSmoothSpeed;

            // Pengaruh kecepatan pada jarak kamera
            if (dragonController != null)
            {
                float normalizedSpeed = Mathf.InverseLerp(0, dragonController.forwardSpeed, Mathf.Abs(dragonController.CurrentSpeed));
                targetBaseDistance = Mathf.Lerp(minSpeedDistance, maxSpeedDistance, normalizedSpeed);
            }
        }
        else
        {
            HandleGroundCameraInput();
            targetBaseDistance = groundCameraDistance;
            targetHeight = groundCameraHeight;
            currentLerpSpeed = groundCameraSpeed;
        }

        HandleCameraPosition(targetBaseDistance, targetHeight, currentLerpSpeed, inFlightMode);
        HandleObstruction(targetBaseDistance, inFlightMode);
    }

    void HandleGroundCameraInput()
    {
        // Menggerakkan kamera berdasarkan input mouse/stick
        currentX += lookInput.x * groundRotationSpeed;
        currentY -= lookInput.y * groundRotationSpeed;
        currentY = Mathf.Clamp(currentY, -60f, 60f); // Clamp untuk ground (lebih bebas)
    }

    void HandleFlightCameraInput()
    {
        // Gerakkan currentX dan currentY berdasarkan input look (offset dari arah pandang naga)
        currentX += lookInput.x * flightLookSensitivity;
        currentY -= lookInput.y * flightLookSensitivity;

        // Smoothly return currentX and currentY to 0 when no input (camera centers)
        // Ini memastikan kamera kembali menghadap ke depan setelah input look dilepas
        if (lookInput.magnitude < 0.1f)
        {
            currentX = Mathf.Lerp(currentX, 0f, Time.deltaTime * flightRotationSpeed);
            currentY = Mathf.Lerp(currentY, 0f, Time.deltaTime * flightRotationSpeed);
        }

        // Clamp the relative angles (currentX dan currentY adalah offset dari arah forward Player)
        currentX = Mathf.Clamp(currentX, -maxFlightLookYawAngle, maxFlightLookYawAngle);
        currentY = Mathf.Clamp(currentY, -maxFlightLookPitchAngle, maxFlightLookPitchAngle);
    }

    // Fungsi WrapAngle tidak lagi diperlukan dengan cara clamping dan lerping di atas.

    void HandleCameraPosition(float baseDistance, float currentCamHeight, float lerpSpeed, bool inFlightMode)
    {
        Quaternion targetRotation;

        if (inFlightMode)
        {
            // Kamera selalu menghadap ke depan Player (currentCameraTarget) secara default
            // dan ditambahi offset dari input look (currentX, currentY) yang sudah di-clamp
            Quaternion playerBaseRotation = currentCameraTarget.rotation; // Ini adalah rotasi Player (root)
            Quaternion lookOffset = Quaternion.Euler(currentY, currentX, 0); // Yaw dan Pitch dari input
            targetRotation = playerBaseRotation * lookOffset; // Gabungkan base rotation dengan offset look
        }
        else
        {
            // Saat di darat, kamera menggunakan rotasi dari input look (currentX, currentY) relatif terhadap playerTarget
            targetRotation = Quaternion.Euler(currentY, currentX, 0);
            playerTarget.rotation = Quaternion.Euler(0, currentX, 0); // Putar player body hanya di sumbu Y saat di darat
        }

        // Posisi yang diinginkan relatif terhadap currentCameraTarget
        Vector3 desiredPosition = currentCameraTarget.position + targetRotation * new Vector3(0, currentCamHeight, -currentEffectiveDistance);

        // Interpolasi posisi kamera
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * lerpSpeed);

        // Kamera selalu melihat ke currentCameraTarget
        transform.LookAt(currentCameraTarget.position + Vector3.up * currentCamHeight / 2f);
    }

    void HandleObstruction(float baseDistance, bool inFlightMode)
    {
        RaycastHit hit;
        // Raycast dari currentCameraTarget ke posisi kamera
        Vector3 rayStart = currentCameraTarget.position + Vector3.up * (inFlightMode ? flightCameraHeight / 2f : groundCameraHeight / 2f);
        Vector3 rayEnd = transform.position;

        float targetObstructionDistance;

        // Lakukan Linecast untuk mendeteksi halangan
        if (Physics.Linecast(rayStart, rayEnd, out hit, obstructionMask))
        {
            targetObstructionDistance = Mathf.Max(minObstructionDistance, Vector3.Distance(rayStart, hit.point));
        }
        else
        {
            targetObstructionDistance = baseDistance;
        }

        // Interpolasi jarak efektif kamera untuk menghindari halangan
        currentEffectiveDistance = Mathf.Lerp(currentEffectiveDistance, targetObstructionDistance, Time.deltaTime * zoomSpeed);
        currentEffectiveDistance = Mathf.Clamp(currentEffectiveDistance, minObstructionDistance, maxObstructionDistance);
    }
}