using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic button untuk mengaktifkan mode drawing (Point/Line/Polygon)
/// Menggantikan PointDrawButton, LineDrawButton, dan PolygonDrawButton
/// </summary>
public class DrawModeButton : MonoBehaviour
{
    [Header("Mode Selection")]
    public DrawTool.DrawMode targetMode = DrawTool.DrawMode.Point;

    [Header("References")]
    public DrawTool drawTool;
    public SimpleMapController_Baru mapController;
    public Button myButton;
    public Image buttonImage;
    
    [Header("Colors")]
    public Color activeColor = new Color(0.1f, 0.55f, 0.28f);
    public Color inactiveColor = Color.white;

    private bool isOn = false;

    void Start()
    {
        if (myButton == null) myButton = GetComponent<Button>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        
        myButton?.onClick.AddListener(OnClick);
        UpdateVisuals();
    }

    void OnClick()
    {
        isOn = !isOn;

        if (isOn)
        {
            drawTool?.ActivateMode(targetMode);
        }
        else
        {
            drawTool?.DeactivateMode(targetMode);
        }

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (buttonImage != null)
            buttonImage.color = isOn ? activeColor : inactiveColor;
    }

    void Update()
    {
        if (drawTool == null) return;
        
        bool modeActive = drawTool.IsModeActive(targetMode);
        
        if (modeActive != isOn)
        {
            isOn = modeActive;
            UpdateVisuals();
        }
    }
}
