using UnityEngine;
using UnityEngine.Events; // Penting untuk UnityEvent

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f; // Jarak Raycast untuk F
    [SerializeField] private LayerMask dragonLayer; // Layer untuk naga

    [Header("UI Detection Settings")]
    [SerializeField] private float uiDetectionRadius = 5f; // Radius deteksi UI
    [SerializeField] private LayerMask uiDetectionDragonLayer; // Layer untuk deteksi naga UI

    [Header("Unity Events")]
    public UnityEvent onDragonEnterRange; // Dipanggil saat naga masuk radius UI
    public UnityEvent onDragonExitRange;  // Dipanggil saat naga keluar radius UI

    [Header("Player Control Settings")]
    // Referensi ke script kontrol karakter pemain
    // Ganti 'NamaScriptKontrolKaraktermu' dengan nama script yang kamu gunakan
    [SerializeField] private MonoBehaviour playerMovementScript;
    // Referensi ke GameObject yang mengandung visual karakter pemain
    [SerializeField] private GameObject playerVisuals; // Misalnya, model 3D karakter

    private DragonController currentNearbyDragon; // Menyimpan referensi naga yang dekat
    private bool uiActive = false; // Status UI
    private Transform originalDragonParent; // Untuk menyimpan parent asli naga

    void Awake()
    {
        if (playerMovementScript == null)
        {
            Debug.LogError("PlayerMovementScript tidak diatur di PlayerInteraction!", this);
        }
        if (playerVisuals == null)
        {
            Debug.LogWarning("PlayerVisuals tidak diatur. Karakter mungkin tetap terlihat saat mengendarai naga.");
        }
    }

    void Update()
    {
        DetectNearbyDragonForUI();
        if (Input.GetKeyDown(KeyCode.F))
        {
            TryInteractWithDragonViaRaycast();
        }
    }

    void DetectNearbyDragonForUI()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, uiDetectionRadius, uiDetectionDragonLayer);

        DragonController foundDragon = null;
        if (hitColliders.Length > 0)
        {
            // Ambil naga pertama yang ditemukan yang belum diaktifkan penerbangannya
            foreach (Collider col in hitColliders)
            {
                DragonController potentialDragon = col.GetComponent<DragonController>();
                if (potentialDragon != null && !potentialDragon.isFlightActivated)
                {
                    foundDragon = potentialDragon;
                    break;
                }
            }
        }

        // Logic untuk menampilkan/menyembunyikan UI
        if (foundDragon != null && currentNearbyDragon == null)
        {
            currentNearbyDragon = foundDragon;
            currentNearbyDragon.onDragonDeactivated.AddListener(OnDragonDeactivatedHandler); // Langganan event
            if (!uiActive)
            {
                onDragonEnterRange.Invoke();
                uiActive = true;
                Debug.Log("Naga masuk radius UI. Panggil UI!");
            }
        }
        else if (foundDragon == null && currentNearbyDragon != null)
        {
            // Jika naga keluar radius atau tidak ada naga lagi
            currentNearbyDragon.onDragonDeactivated.RemoveListener(OnDragonDeactivatedHandler); // Batalkan langganan
            currentNearbyDragon = null;
            if (uiActive)
            {
                onDragonExitRange.Invoke();
                uiActive = false;
                Debug.Log("Naga keluar radius UI. Sembunyikan UI!");
            }
        }
        // Jika naga sudah diaktifkan (pemain sedang terbang) dan UI masih aktif, sembunyikan UI
        else if (foundDragon != null && foundDragon.isFlightActivated && uiActive)
        {
            onDragonExitRange.Invoke();
            uiActive = false;
        }
    }

    void TryInteractWithDragonViaRaycast()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 rayDirection = transform.forward;

        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, interactionRange, dragonLayer))
        {
            DragonController dragon = hit.collider.GetComponent<DragonController>();
            if (dragon != null && !dragon.isFlightActivated)
            {
                // Menonaktifkan kontrol pemain
                if (playerMovementScript != null)
                {
                    playerMovementScript.enabled = false;
                    Debug.Log("Kontrol pemain dinonaktifkan.");
                }
                // Sembunyikan visual karakter pemain
                if (playerVisuals != null)
                {
                    playerVisuals.SetActive(false);
                    Debug.Log("Visual pemain disembunyikan.");
                }

                // >>> Perubahan Penting di sini <<<
                originalDragonParent = dragon.transform.parent; // Simpan parent asli naga
                dragon.transform.SetParent(this.transform); // Set pemain sebagai parent baru naga
                // Atur posisi relatif naga agar pas di bawah/depan pemain
                // Kamu mungkin perlu menyesuaikan nilai 'y' atau 'z' di editor untuk posisi duduk yang pas
                dragon.transform.localPosition = Vector3.zero; // Awalnya samakan posisinya dengan parent (pemain)
                dragon.transform.localRotation = Quaternion.identity; // Reset rotasi relatif

                // Beri tahu naga untuk mengaktifkan mode terbang
                dragon.ActivateFlight();

                // Langganan event deaktivasi naga (jika belum berlangganan atau naga berbeda)
                if (currentNearbyDragon != dragon)
                {
                    currentNearbyDragon = dragon;
                    currentNearbyDragon.onDragonDeactivated.AddListener(OnDragonDeactivatedHandler);
                }

                if (uiActive)
                {
                    onDragonExitRange.Invoke(); // Sembunyikan UI setelah interaksi
                    uiActive = false;
                }
                Debug.Log("Interaksi F dengan naga berhasil! Naga siap terbang.");
            }
        }
    }

    private void OnDragonDeactivatedHandler()
    {
        // Mengembalikan kontrol dan visual pemain
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true; // Mengaktifkan kembali kontrol pemain
            Debug.Log("Kontrol pemain diaktifkan kembali.");
        }
        if (playerVisuals != null)
        {
            playerVisuals.SetActive(true); // Tampilkan kembali visual pemain
            Debug.Log("Visual pemain diaktifkan kembali.");
        }

        // Mengembalikan naga ke parent aslinya
        if (currentNearbyDragon != null)
        {
            currentNearbyDragon.transform.SetParent(originalDragonParent);
            // Opsional: atur posisi naga setelah mendarat agar tidak di tempat yang aneh
            // currentNearbyDragon.transform.position = transform.position + transform.forward * 2f; // Contoh
            // currentNearbyDragon.transform.rotation = Quaternion.identity; // Reset rotasi jika perlu

            currentNearbyDragon.onDragonDeactivated.RemoveListener(OnDragonDeactivatedHandler); // Batalkan langganan
            currentNearbyDragon = null;
            originalDragonParent = null; // Reset original parent
        }
    }

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