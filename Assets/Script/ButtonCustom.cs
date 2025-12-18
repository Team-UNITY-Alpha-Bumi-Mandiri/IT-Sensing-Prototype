using UnityEngine;

// Script sederhana untuk mengaktifkan/menonaktifkan objek UI.
public class ButtonCustom : MonoBehaviour
{
    [SerializeField] private GameObject button; // Referensi objek yang akan di-toggle

    // Fungsi ini dipanggil (misal dari Event Trigger) untuk mengubah status aktif objek.
    public void CreateButtonActive() 
    {
        // Cek jika null agar tidak error, lalu balikkan status aktifnya (Toggle)
        if (button) button.SetActive(!button.activeSelf);
    }
}