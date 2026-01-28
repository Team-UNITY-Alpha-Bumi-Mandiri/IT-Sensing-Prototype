using UnityEngine;
using UnityEngine.UI;

public class MeasureButton : MonoBehaviour
{
    public MeasureTool2 measureTool;
    public SimpleMapController_Baru mapController; // Untuk matikan drag map jika perlu
    public Button myButton;
    public Image buttonImage;
    
    public Color activeColor = new Color(0.1f, 0.55f, 0.28f); // Hijau
    public Color inactiveColor = Color.white;

    private bool isOn = false;

    void Start()
    {
        myButton.onClick.AddListener(OnClick);
        UpdateVisuals();
    }

    void OnClick()
    {
        isOn = !isOn;
        measureTool.ToggleMeasure(isOn);
        
        // Opsional: Matikan input peta saat mengukur agar tidak drag saat klik titik
        // mapController.isInputEnabled = !isOn; 

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        buttonImage.color = isOn ? activeColor : inactiveColor;
    }
}