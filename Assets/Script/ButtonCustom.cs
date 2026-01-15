using UnityEngine;
using UnityEngine.UI;

// ============================================================
// ButtonCustom - Toggle visibility dan warna objek UI
// ============================================================
// Fungsi:
// - ToggleVisibility: Show/hide objek target
// - ToggleColor: Ubah warna berdasarkan Toggle state
// ============================================================
public class ButtonCustom : MonoBehaviour
{
    [SerializeField] GameObject target;       // Objek yang akan di-toggle show/hide
    [SerializeField] Toggle toggleComponent;  // Toggle untuk referensi state warna

    // Toggle visibility objek target (show <-> hide)
    public void ToggleVisibility()
    {
        if (target != null)
            target.SetActive(!target.activeSelf);
    }

    // Ubah warna target berdasarkan state toggle
    // Hijau (#36B768) jika ON, Putih jika OFF
    public void ToggleColor()
    {
        if (target == null || toggleComponent == null) return;
        
        var img = target.GetComponent<Image>();
        if (img == null) return;
        
        img.color = toggleComponent.isOn 
            ? new Color32(0x36, 0xB7, 0x68, 255)  // Hijau
            : Color.white;
    }
}