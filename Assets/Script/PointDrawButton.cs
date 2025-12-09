using UnityEngine;
using UnityEngine.UI;

public class PointDrawButton : MonoBehaviour
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
            // Aktifkan Point mode
            drawTool.ActivateMode(DrawTool.DrawMode.Point);
            
            // LANGSUNG spawn point di center peta (atau tunggu user klik)
            // Untuk Point mode, kita langsung spawn saat button diklik
            Vector2 centerScreen = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 centerLatLon = mapController.ScreenToLatLon(centerScreen);
            
            // Spawn point di center
            // (Atau bisa dikosongkan, tunggu user klik di peta)
            
            // Matikan input peta agar user bisa klik point tanpa drag
            if (mapController != null)
            {
                mapController.isInputEnabled = false;
            }
        }
        else
        {
            // Deaktifkan Point mode
            drawTool.DeactivateMode(DrawTool.DrawMode.Point);
            
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

    // Update state jika mode berubah dari luar
    void Update()
    {
        if (drawTool != null)
        {
            bool shouldBeOn = drawTool.IsModeActive(DrawTool.DrawMode.Point);
            
            if (shouldBeOn != isOn)
            {
                isOn = shouldBeOn;
                UpdateVisuals();
                
                // Update map input
                if (mapController != null)
                {
                    mapController.isInputEnabled = !isOn;
                }
            }
        }
    }
}