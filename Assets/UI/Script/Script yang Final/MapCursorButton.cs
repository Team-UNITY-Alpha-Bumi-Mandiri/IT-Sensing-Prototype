using NUnit.Framework;
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

    [Header("Disable on Pop-Up activations")]
     GameObject popUpContainer;
    public GameObject[] selectedPopUp;

    bool isActive;

    void Start()
    {
        // 1. Pastikan komponen terhubung
        if (myButton == null) myButton = GetComponent<Button>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();

        // 2. Setup kondisi awal (Sesuai settingan di Map Controller)
        UpdateVisuals();

        // 3. Pasang listener klik
        myButton.onClick.AddListener(OnButtonClicked);

        OnButtonClicked();
    }

    void Update()
    {
        if (isActive)
            mapController.isInputEnabled = AllowedToDragMapToo() ? true : false;
    }

    bool AllowedToDragMap()
    {
        foreach (Transform t in popUpContainer.transform)
        {
            if (t.gameObject.activeSelf == true)
            {
                foreach (Transform c in t)
                {
                    if (c.gameObject.activeSelf == true)
                    {
                        return false;
                    }
                }
                return false;
            }
        }
        return true;
    }

    bool AllowedToDragMapToo()
    {
        foreach (GameObject t in selectedPopUp)
        {
            if (t.activeSelf == true)
            {
                return false;
            }
        }
        return true;
    }
    void OnButtonClicked()
    {
        isActive = !isActive;
        EnableDragFunction(isActive);
        UpdateVisuals();   // Update warna tombol
    }

    void EnableDragFunction(bool mapControllerState)
    {
        if (mapController != null)
        {
            mapController.isInputEnabled = mapControllerState;
            // Balik status (Toggle): True jadi False, False jadi True
            //   mapController.isInputEnabled = !mapController.isInputEnabled;
        }

    }

    void UpdateVisuals()
    {
        if (mapController != null && buttonImage != null)
        {
            buttonImage.color = isActive ? activeColor : inactiveColor;
            // Jika Enabled = Hijau, Jika Disabled = Putih
            //   buttonImage.color = mapController.isInputEnabled ? activeColor : inactiveColor;
        }
    }
}