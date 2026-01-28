using UnityEngine;
using UnityEngine.UI;

public class MeasureAreaButton : MonoBehaviour
{
    public MeasureAreaTool areaTool; // Referensi ke skrip baru
    public Button myButton;
    public Image buttonImage;
    
    public Color activeColor = new Color(0.1f, 0.55f, 0.28f); // Hijau
    public Color inactiveColor = Color.white;

    private bool isOn = false;

    void Start()
    {
        if (myButton != null) myButton.onClick.AddListener(OnClick);
        UpdateVisuals();
    }

    void OnClick()
    {
        isOn = !isOn;
        if (areaTool != null) areaTool.ToggleAreaTool(isOn);
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (buttonImage != null)
            buttonImage.color = isOn ? activeColor : inactiveColor;
    }
}