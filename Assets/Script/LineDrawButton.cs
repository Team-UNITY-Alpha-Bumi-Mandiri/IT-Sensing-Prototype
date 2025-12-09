using UnityEngine;
using UnityEngine.UI;

public class LineDrawButton : MonoBehaviour
{
    [Header("References")]
    public DrawTool drawTool;
    public SimpleMapController_Baru mapController;
    public Button myButton;
    public Image buttonImage;
    
    [Header("Colors")]
    public Color activeColor = new Color(0.1f, 0.55f, 0.28f); // Hijau
    public Color inactiveColor = Color.white;

    private bool isOn = false;

    void Start()
    {
        if (myButton == null) myButton = GetComponent<Button>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        
        myButton.onClick.AddListener(OnClick);
        UpdateVisuals();
    }

    void OnClick()
    {
        isOn = !isOn;

        if (isOn)
        {
            // Aktifkan Line mode
            drawTool.ActivateMode(DrawTool.DrawMode.Line);
            
            // Matikan input peta agar tidak drag saat klik titik
            if (mapController != null)
            {
                mapController.isInputEnabled = false;
            }
        }
        else
        {
            // Deaktifkan Line mode
            drawTool.DeactivateMode(DrawTool.DrawMode.Line);
            
            // Hidupkan kembali input peta
            if (mapController != null)
            {
                mapController.isInputEnabled = true;
            }
        }

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (buttonImage != null)
        {
            buttonImage.color = isOn ? activeColor : inactiveColor;
        }
    }

    // Auto-update jika drawing selesai (double-click)
    void Update()
    {
        if (drawTool != null)
        {
            // Cek apakah mode masih aktif
            bool modeActive = drawTool.IsModeActive(DrawTool.DrawMode.Line);
            
            // Jika mode di-deactivate (misal setelah finish/cancel)
            if (!modeActive && isOn)
            {
                isOn = false;
                UpdateVisuals();
                
                // Restore map input
                if (mapController != null)
                {
                    mapController.isInputEnabled = true;
                }
                
                Debug.Log("Line mode auto-disabled (selesai drawing)");
            }
        }
    }
}