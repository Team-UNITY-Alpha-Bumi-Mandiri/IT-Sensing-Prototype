using UnityEngine;
using UnityEngine.UI; // Pastikan ini ada jika Anda menggunakan UI.Button

public class ObjectToggler : MonoBehaviour
{
    // Variabel yang akan menampung objek yang ingin di-toggle (tampilkan/sembunyikan).
    [Tooltip("Seret objek (GameObject/Panel) yang ingin di-toggle ke sini.")]
    public GameObject targetObject;

    // Metode publik ini dipanggil saat tombol diklik.
    public void ToggleObjectVisibility()
    {
        // Pastikan targetObject sudah di-assign (tidak null)
        if (targetObject != null)
        {
            // Ambil status aktif saat ini, lalu balikkan (invert).
            // Jika aktif (true), akan diubah menjadi tidak aktif (false), dan sebaliknya.
            bool isActive = targetObject.activeSelf;
            targetObject.SetActive(!isActive);

            Debug.Log($"Status objek '{targetObject.name}' diubah menjadi: {!isActive}");
        }
        else
        {
            Debug.LogError("Target Object belum di-assign di Inspector!");
        }
    }
}