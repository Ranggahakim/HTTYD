using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f; // Jarak Raycast untuk F
    [SerializeField] private LayerMask dragonLayer; // Layer untuk GameObject Dragon

    [Header("UI Detection Settings")]
    [SerializeField] private float uiDetectionRadius = 5f; // Radius deteksi UI
    [SerializeField] private LayerMask uiDetectionDragonLayer; // Layer untuk deteksi naga UI

    [Header("Unity Events")]
    public UnityEvent onDragonEnterRange; // Dipanggil saat naga masuk radius UI
    public UnityEvent onDragonExitRange;  // Dipanggil saat naga keluar radius UI

    [Header("Player Control Settings")]
    [SerializeField] private MonoBehaviour playerMovementScript; // Script kontrol karakter pemain di darat
    [SerializeField] private GameObject playerVisuals; // GameObject yang mengandung model 3D karakter pemain
    [SerializeField] private ThirdPersonCamera thirdPersonCamera; // Referensi ke script kamera

    private DragonController dragonController; // Ini adalah DragonController yang ada di GameObject Dragon
    private GameObject currentDragonGameObject; // Referensi ke GameObject Dragon (root naga di scene)
    private bool uiActive = false; // Status UI

    // Untuk menyimpan parent asli Dragon (misal: World)
    private Transform originalDragonParent;
    // Untuk menyimpan local position dan rotation Dragon saat menjadi child Player
    [SerializeField] private Vector3 dragonLocalPosOnPlayer = new Vector3(0, 0, 0); // Posisi relatif Dragon di bawah Player
    [SerializeField] private Quaternion dragonLocalRotOnPlayer = Quaternion.identity; // Rotasi relatif Dragon di bawah Player

    // --- INPUT SYSTEM VARIABLES ---
    private PlayerInputActions playerInputActions;

    void Awake()
    {
        // Cari DragonController di scene, karena dia ada di GameObject Dragon terpisah
        dragonController = FindObjectOfType<DragonController>();
        if (dragonController == null)
        {
            Debug.LogError("PlayerInteraction: DragonController tidak ditemukan di scene! Pastikan ada DragonController di GameObject Dragon.", this);
            enabled = false;
            return;
        }
        currentDragonGameObject = dragonController.gameObject; // Simpan referensi ke GameObject Dragon

        if (playerMovementScript == null)
        {
            Debug.LogError("PlayerMovementScript tidak diatur di PlayerInteraction!", this);
        }
        if (playerVisuals == null)
        {
            Debug.LogWarning("PlayerVisuals tidak diatur. Karakter mungkin tetap terlihat saat mengendarai naga.");
        }
        if (thirdPersonCamera == null)
        {
            thirdPersonCamera = FindObjectOfType<ThirdPersonCamera>();
            if (thirdPersonCamera == null)
            {
                Debug.LogError("ThirdPersonCamera tidak ditemukan di scene! Pastikan ada script ThirdPersonCamera.", this);
                enabled = false;
                return;
            }
        }

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Interact.performed += ctx => TryInteractWithDragonViaRaycast();

        // Daftarkan handler untuk event Deactivated dari DragonController
        dragonController.onDragonDeactivated.AddListener(OnDragonDeactivatedHandler);

        // Simpan parent asli Dragon (sebelum dia menjadi anak Player)
        originalDragonParent = currentDragonGameObject.transform.parent;
        if (originalDragonParent == null) // Jika Dragon ada di root scene
        {
            originalDragonParent = null; // Menandakan dia tidak punya parent lain
        }
    }

    void OnEnable()
    {
        playerInputActions.Player.Enable();
    }

    void OnDisable()
    {
        playerInputActions.Player.Disable();
        if (dragonController != null && dragonController.isFlightActivated)
        {
            // Jika script ini dinonaktifkan saat terbang, pastikan naga juga kembali
            dragonController.DeactivateFlight(); // Ini akan memanggil OnDragonDeactivatedHandler
        }
        if (dragonController != null)
        {
            dragonController.onDragonDeactivated.RemoveListener(OnDragonDeactivatedHandler);
        }
    }

    void Update()
    {
        // Hanya deteksi naga di darat jika tidak sedang terbang
        if (!dragonController.isFlightActivated)
        {
            DetectNearbyDragonForUI();
        }
        else // Jika sedang terbang, pastikan UI tidak aktif
        {
            if (uiActive)
            {
                onDragonExitRange.Invoke();
                uiActive = false;
            }
        }
    }

    void DetectNearbyDragonForUI()
    {
        // Pastikan currentDragonGameObject ada dan belum dinaiki
        if (currentDragonGameObject == null || dragonController.isFlightActivated) return;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, uiDetectionRadius, uiDetectionDragonLayer);

        bool foundRelevantDragon = false;
        if (hitColliders.Length > 0)
        {
            foreach (Collider col in hitColliders)
            {
                // Pastikan collider yang terdeteksi adalah bagian dari currentDragonGameObject
                if (col.transform.root.gameObject == currentDragonGameObject)
                {
                    foundRelevantDragon = true;
                    break;
                }
            }
        }

        if (foundRelevantDragon && !uiActive)
        {
            onDragonEnterRange.Invoke();
            uiActive = true;
            Debug.Log("Naga masuk radius UI. Panggil UI!");
        }
        else if (!foundRelevantDragon && uiActive)
        {
            onDragonExitRange.Invoke();
            uiActive = false;
            Debug.Log("Naga keluar radius UI. Sembunyikan UI!");
        }
    }

    void TryInteractWithDragonViaRaycast()
    {
        // Hanya interaksi jika UI sedang aktif (naga dalam jangkauan) dan belum terbang
        if (!uiActive || currentDragonGameObject == null || dragonController.isFlightActivated) return;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 rayDirection = transform.forward;

        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, interactionRange, dragonLayer))
        {
            // Pastikan yang di-hit adalah GameObject Dragon yang kita deteksi
            if (hit.transform.root.gameObject == currentDragonGameObject)
            {
                // Menonaktifkan kontrol pemain di darat
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

                // >>> PENTING: Mengatur Action Map di sini <<<
                playerInputActions.Player.Disable(); // Nonaktifkan input karakter pemain di darat
                // Input DragonFlight akan diaktifkan oleh DragonController.ActivateFlight()

                // Simpan posisi dan rotasi Dragon di dunia sebelum diparenting
                Vector3 dragonWorldPos = currentDragonGameObject.transform.position;
                Quaternion dragonWorldRot = currentDragonGameObject.transform.rotation;

                // Atur parent Dragon ke Player
                currentDragonGameObject.transform.SetParent(this.transform); // 'this.transform' adalah Player
                currentDragonGameObject.transform.localPosition = dragonLocalPosOnPlayer; // Set posisi relatif
                currentDragonGameObject.transform.localRotation = dragonLocalRotOnPlayer; // Set rotasi relatif

                // Beri tahu DragonController (yang ada di Dragon) untuk mengaktifkan mode terbang
                dragonController.ActivateFlight(this.transform); // Kirim referensi Player root ke DragonController

                // Beri tahu kamera untuk menargetkan Player (root)
                if (thirdPersonCamera != null)
                {
                    thirdPersonCamera.SetCurrentCameraTarget(this.transform);
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

        // >>> PENTING: Mengatur Action Map di sini <<<
        playerInputActions.Player.Enable(); // Aktifkan kembali input karakter pemain di darat
        // Input DragonFlight sudah dinonaktifkan oleh DragonController.DeactivateFlight()

        // Mengembalikan parent Dragon ke aslinya (root scene)
        if (currentDragonGameObject != null)
        {
            // Ambil posisi dan rotasi Player saat turun untuk menempatkan naga di sana
            Vector3 playerCurrentPos = transform.position;
            Quaternion playerCurrentRot = transform.rotation;

            currentDragonGameObject.transform.SetParent(originalDragonParent); // Kembalikan ke parent asli
            currentDragonGameObject.transform.position = playerCurrentPos + playerCurrentRot * new Vector3(0, 0, 2f); // Contoh: di depan pemain
            currentDragonGameObject.transform.rotation = playerCurrentRot; // Rotasi sama dengan pemain

            // Beri tahu kamera untuk menargetkan pemain kembali
            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.SetCurrentCameraTarget(this.transform);
            }
        }
        Debug.Log("Naga berhasil diturunkan.");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 rayDirection = transform.forward;
        Gizmos.DrawRay(rayOrigin, rayDirection * interactionRange);
        Gizmos.DrawWireSphere(rayOrigin + rayDirection * interactionRange, 0.2f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, uiDetectionRadius);
    }
}