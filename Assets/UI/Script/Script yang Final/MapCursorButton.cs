using UnityEngine;
using UnityEngine.UI;

public class MapCursorButton : MonoBehaviour
{
    [Header("References")]
    public SimpleMapController_Baru mapController;
    public Button myButton;
    public Image buttonImage; // Image komponen tombol ini untuk ganti warna

    [Header("Colors")]
    public Color activeColor = new Color(0.1f, 0.55f, 0.28f); // Hijau (Default)
    public Color inactiveColor = Color.white; // Putih (Saat non-aktif)

    void Start()
    {
        // 1. Pastikan komponen terhubung
        if (myButton == null) myButton = GetComponent<Button>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();

        // 2. Setup kondisi awal (Sesuai settingan di Map Controller)
        UpdateVisuals();

        // 3. Pasang listener klik
        myButton.onClick.AddListener(OnButtonClicked);
    }

    void OnButtonClicked()
    {
        if (mapController != null)
        {
            // Balik status (Toggle): True jadi False, False jadi True
            mapController.isInputEnabled = !mapController.isInputEnabled;
            
            // Update warna tombol
            UpdateVisuals();
        }
    }

    void UpdateVisuals()
    {
        if (mapController != null && buttonImage != null)
        {
            // Jika Enabled = Hijau, Jika Disabled = Putih
            buttonImage.color = mapController.isInputEnabled ? activeColor : inactiveColor;
        }
    }
}