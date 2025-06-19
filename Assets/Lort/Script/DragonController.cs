using UnityEngine;

public class DragonController : MonoBehaviour
{
    public bool isFlightActivated { get; private set; } = false; // Status apakah naga siap terbang

    // Mungkin nanti kita tambahkan kecepatan terbang, dll.
    [SerializeField] private float flightSpeed = 10f;
    [SerializeField] private float rotationSpeed = 2f;

    void Start()
    {
        // Naga tidak bisa terbang saat game dimulai
        isFlightActivated = false;
    }

    public void ActivateFlight()
    {
        isFlightActivated = true;
        Debug.Log(gameObject.name + ": Penerbangan diaktifkan!");
        // Di sini kita bisa trigger animasi 'siap terbang' atau UI prompt
    }

    // Method untuk mengelola logika terbang (nanti akan kita kembangkan)
    void Update()
    {
        if (isFlightActivated)
        {
            // Untuk sementara, kita bisa membuat naga bergerak maju jika sudah diaktifkan
            // Ini akan kita ganti dengan input pemain nanti
            // transform.Translate(Vector3.forward * flightSpeed * Time.deltaTime);
            // Debug.Log(gameObject.name + " sedang terbang...");
        }
    }

    // Ini bisa dipanggil oleh sistem terbang nanti untuk menonaktifkan terbang
    public void DeactivateFlight()
    {
        isFlightActivated = false;
        Debug.Log(gameObject.name + ": Penerbangan dinonaktifkan.");
    }
}