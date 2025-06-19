using UnityEngine;
using UnityEngine.Events; // Penting untuk UnityEvent

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f; // Jarak Raycast untuk F
    [SerializeField] private LayerMask dragonLayer; // Layer untuk naga

    [Header("UI Detection Settings")]
    [SerializeField] private float uiDetectionRadius = 5f; // Radius deteksi UI
    [SerializeField] private LayerMask uiDetectionDragonLayer; // Pastikan ini sama dengan dragonLayer, atau layer khusus jika ada naga lain

    [Header("Unity Events")]
    public UnityEvent onDragonEnterRange; // Dipanggil saat naga masuk radius UI
    public UnityEvent onDragonExitRange;  // Dipanggil saat naga keluar radius UI

    private DragonController currentNearbyDragon; // Menyimpan referensi naga yang dekat
    private bool uiActive = false; // Status UI

    void Update()
    {
        // 1. Deteksi Naga untuk UI (berbasis area)
        DetectNearbyDragonForUI();

        // 2. Deteksi Interaksi Tombol F (berbasis Raycast)
        if (Input.GetKeyDown(KeyCode.F))
        {
            TryInteractWithDragonViaRaycast();
        }
    }

    void DetectNearbyDragonForUI()
    {
        // Cari semua collider dalam radius deteksi UI
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, uiDetectionRadius, uiDetectionDragonLayer);

        DragonController foundDragon = null;
        if (hitColliders.Length > 0)
        {
            // Ambil naga pertama yang ditemukan (atau yang paling dekat jika perlu logika lebih lanjut)
            foundDragon = hitColliders[0].GetComponent<DragonController>();
        }

        // Jika ada naga di dekat kita DAN belum ada naga yang terdeteksi ATAU naga yang terdeteksi berbeda
        if (foundDragon != null && currentNearbyDragon == null)
        {
            currentNearbyDragon = foundDragon;
            if (!uiActive) // Pastikan hanya panggil sekali
            {
                onDragonEnterRange.Invoke();
                uiActive = true;
                Debug.Log("Naga masuk radius UI. Panggil UI!");
            }
        }
        // Jika tidak ada naga di dekat kita, TAPI sebelumnya ada naga yang terdeteksi
        else if (foundDragon == null && currentNearbyDragon != null)
        {
            currentNearbyDragon = null;
            if (uiActive) // Pastikan hanya panggil sekali
            {
                onDragonExitRange.Invoke();
                uiActive = false;
                Debug.Log("Naga keluar radius UI. Sembunyikan UI!");
            }
        }
        // Kondisi jika masih ada naga yang sama di radius (tidak perlu panggil event lagi)
        // Kondisi jika tidak ada naga dan memang belum ada (tidak perlu panggil event)
    }

    void TryInteractWithDragonViaRaycast()
    {
        // Asal Raycast dari posisi pemain, sedikit ke atas
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        // Arah Raycast ke depan pemain (sesuaikan jika perlu forward dari kamera/karakter)
        Vector3 rayDirection = transform.forward; // Asumsikan transform.forward adalah arah hadap pemain

        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, interactionRange, dragonLayer))
        {
            DragonController dragon = hit.collider.GetComponent<DragonController>();
            if (dragon != null && !dragon.isFlightActivated) // Pastikan naga belum diaktifkan
            {
                dragon.ActivateFlight();
                // Opsional: Jika naga sudah diaktifkan, sembunyikan UI interaksi
                if (uiActive)
                {
                    onDragonExitRange.Invoke();
                    uiActive = false;
                }
                Debug.Log("Interaksi F dengan naga berhasil! Naga siap terbang.");
            }
        }
    }

    // Untuk visualisasi di editor
    void OnDrawGizmosSelected()
    {
        // Visualisasi Raycast Interaksi (F)
        Gizmos.color = Color.cyan;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 rayDirection = transform.forward;
        Gizmos.DrawRay(rayOrigin, rayDirection * interactionRange);
        Gizmos.DrawWireSphere(rayOrigin + rayDirection * interactionRange, 0.2f);

        // Visualisasi OverlapSphere untuk UI
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, uiDetectionRadius);
    }
}