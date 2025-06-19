using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // Karakter yang akan diikuti kamera

    [Header("Camera Settings")]
    public float distance = 5f; // Jarak kamera dari target
    public float height = 2f;   // Ketinggian kamera dari target
    public float cameraSpeed = 10f; // Kecepatan interpolasi kamera mengikuti target
    public float rotationSpeed = 3f; // Kecepatan rotasi kamera dengan mouse

    [Header("Obstruction Handling")]
    public LayerMask obstructionMask; // Layer objek yang bisa menghalangi pandangan kamera
    public float zoomSpeed = 5f; // Kecepatan zoom saat ada halangan
    public float minDistance = 1f; // Jarak minimal kamera saat terhalang
    public float maxDistance = 7f; // Jarak maksimal kamera (opsional, jika ingin batasan zoom)

    private float currentX = 0.0f;
    private float currentY = 0.0f;
    private float currentDistance; // Jarak kamera saat ini, akan berubah jika ada halangan

    void Awake()
    {
        if (target == null)
        {
            Debug.LogError("Camera target not assigned! Please assign a target Transform in the Inspector.");
            enabled = false; // Nonaktifkan script jika target tidak ada
            return;
        }
        currentDistance = distance;
    }

    void LateUpdate() // Gunakan LateUpdate agar kamera bergerak setelah karakter
    {
        HandleInput();
        HandleCameraPosition();
        HandleObstruction();
    }

    void HandleInput()
    {
        // Mendapatkan input mouse untuk rotasi
        currentX += Input.GetAxis("Mouse X") * rotationSpeed;
        currentY -= Input.GetAxis("Mouse Y") * rotationSpeed;

        // Membatasi rotasi Y (vertikal) agar tidak terbalik
        currentY = Mathf.Clamp(currentY, -60f, 60); // Batasan pitch kamera (misal -60 hingga 80 derajat)
    }

    void HandleCameraPosition()
    {
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 desiredPosition = target.position + rotation * new Vector3(0, height, -currentDistance);

        // Interpolasi posisi kamera untuk smoothing
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * cameraSpeed);

        // Arahkan kamera ke target
        transform.LookAt(target.position + Vector3.up * height / 2f); // Arahkan sedikit ke atas target
    }

    void HandleObstruction()
    {
        RaycastHit hit;
        // Posisi awal raycast (target), arah raycast (ke kamera), dan jarak raycast
        if (Physics.Linecast(target.position, transform.position, out hit, obstructionMask))
        {
            // Jika ada objek yang menghalangi, mundurkan kamera
            currentDistance = Mathf.Lerp(currentDistance, Mathf.Max(minDistance, Vector3.Distance(target.position, hit.point)), Time.deltaTime * zoomSpeed);
        }
        else
        {
            // Jika tidak ada halangan, kembalikan kamera ke jarak normal
            currentDistance = Mathf.Lerp(currentDistance, distance, Time.deltaTime * zoomSpeed);
        }
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance); // Pastikan jarak dalam batas
    }
}