using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // Karakter yang akan diikuti kamera
    public DragonController dragonController; // BARU: Referensi ke DragonController

    [Header("Camera Settings")]
    public float distance = 5f; // Jarak kamera default dari target (saat lambat)
    public float height = 2f;   // Ketinggian kamera dari target
    public float cameraSpeed = 10f; // Kecepatan interpolasi kamera mengikuti target
    public float rotationSpeed = 3f; // Kecepatan rotasi kamera dengan mouse

    [Header("Speed-Based Distance")] // BARU: Pengaturan untuk jarak berbasis kecepatan
    public float minSpeedDistance = 5f; // Jarak kamera saat kecepatan rendah
    public float maxSpeedDistance = 15f; // Jarak kamera saat kecepatan tinggi
    public float speedDistanceTransitionSpeed = 5f; // Kecepatan transisi jarak kamera berbasis kecepatan

    [Header("Obstruction Handling")]
    public LayerMask obstructionMask; // Layer objek yang bisa menghalangi pandangan kamera
    public float zoomSpeed = 5f; // Kecepatan zoom saat ada halangan
    public float minObstructionDistance = 1f; // Jarak minimal kamera saat terhalang (Ganti nama)
    public float maxObstructionDistance = 7f; // Jarak maksimal kamera (Ganti nama)

    private float currentX = 0.0f;
    private float currentY = 0.0f;
    private float currentEffectiveDistance; // Jarak kamera saat ini, hasil kombinasi speed & obstruction

    void Awake()
    {
        if (target == null)
        {
            Debug.LogError("ThirdPersonCamera: Camera target not assigned! Please assign a target Transform in the Inspector.");
            enabled = false;
            return;
        }
        // BARU: Coba dapatkan DragonController dari target atau parent-nya
        if (dragonController == null)
        {
            dragonController = target.GetComponent<DragonController>();
            if (dragonController == null)
            {
                dragonController = target.GetComponentInParent<DragonController>();
            }
            if (dragonController == null)
            {
                Debug.LogWarning("ThirdPersonCamera: DragonController not found on target or its parent. Speed-based camera distance will not work.");
            }
        }

        currentEffectiveDistance = distance; // Inisialisasi dengan jarak default

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void LateUpdate()
    {
        HandleInput();

        // BARU: Hitung jarak dasar berdasarkan kecepatan naga
        float speedBasedDistance = distance; // Jarak default jika DragonController tidak ada/aktif
        if (dragonController != null && dragonController.isFlightActivated)
        {
            float normalizedSpeed = Mathf.InverseLerp(0, dragonController.forwardSpeed, Mathf.Abs(dragonController.CurrentSpeed));
            speedBasedDistance = Mathf.Lerp(minSpeedDistance, maxSpeedDistance, normalizedSpeed);
        }

        HandleCameraPosition(speedBasedDistance); // Lewatkan jarak dasar ke fungsi posisi
        HandleObstruction(speedBasedDistance); // Lewatkan jarak dasar ke fungsi obstruksi
    }

    void HandleInput()
    {
        currentX += Input.GetAxis("Mouse X") * rotationSpeed;
        currentY -= Input.GetAxis("Mouse Y") * rotationSpeed;
        currentY = Mathf.Clamp(currentY, -60f, 60);
    }

    // Ubah parameter: tambahkan float baseDistance
    void HandleCameraPosition(float baseDistance)
    {
        // currentDistance sudah tidak relevan di sini. Kita pakai currentEffectiveDistance.
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 desiredPosition = target.position + rotation * new Vector3(0, height, -currentEffectiveDistance); // Gunakan currentEffectiveDistance

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * cameraSpeed);
        transform.LookAt(target.position + Vector3.up * height / 2f);
    }

    // Ubah parameter: tambahkan float baseDistance
    void HandleObstruction(float baseDistance)
    {
        RaycastHit hit;
        Vector3 rayStart = target.position;
        Vector3 rayEnd = transform.position; // Gunakan posisi kamera saat ini untuk cek halangan

        float targetObstructionDistance;

        // Cek halangan dari target ke posisi kamera yang sudah di-interpolasi
        if (Physics.Linecast(rayStart, rayEnd, out hit, obstructionMask))
        {
            // Jika ada objek yang menghalangi, mundurkan kamera ke titik tabrakan
            targetObstructionDistance = Mathf.Max(minObstructionDistance, Vector3.Distance(rayStart, hit.point));
        }
        else
        {
            // Jika tidak ada halangan, kembalikan ke baseDistance (yang sudah diperhitungkan speed)
            targetObstructionDistance = baseDistance;
        }

        // Lerp currentEffectiveDistance menuju targetObstructionDistance
        // Gunakan zoomSpeed untuk transisi saat ada/tidak ada halangan
        currentEffectiveDistance = Mathf.Lerp(currentEffectiveDistance, targetObstructionDistance, Time.deltaTime * zoomSpeed);

        // Pastikan currentEffectiveDistance tetap dalam batas min dan max yang ditentukan
        currentEffectiveDistance = Mathf.Clamp(currentEffectiveDistance, minObstructionDistance, maxObstructionDistance);
    }
}