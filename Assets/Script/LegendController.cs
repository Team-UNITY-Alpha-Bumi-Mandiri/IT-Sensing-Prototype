using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LegendController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject legendContainer; // Panel utama legenda
    public RawImage gradientImage;     // Gambar gradasi
    public TMP_Text minText;           // Label nilai min
    public TMP_Text maxText;           // Label nilai max
    public Button expandButton;        // Tombol (+)
    public Button colorPickerButton;   // Tombol (Klik Image)

    [Header("State")]
    public bool isExpanded = false;
    public string currentLayerName;
    
    // Callback saat user klik color picker
    public System.Action<string> onColorPickerRequest;

    void Awake()
    {
        if (legendContainer == null) legendContainer = gameObject;
        if (gradientImage == null) gradientImage = GetComponentInChildren<RawImage>(true);
        if (minText == null || maxText == null)
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0 && minText == null) minText = texts[0];
            if (texts.Length > 1 && maxText == null) maxText = texts[1];
        }
        if (colorPickerButton == null && gradientImage != null)
        {
            colorPickerButton = gradientImage.GetComponent<Button>();
        }
    }

    void Start()
    {
        if (expandButton != null) expandButton.onClick.AddListener(ToggleExpand);
        if (colorPickerButton != null) colorPickerButton.onClick.AddListener(() => onColorPickerRequest?.Invoke(currentLayerName));
        
        // Default hidden
        if (legendContainer != null) legendContainer.SetActive(false);
    }

    public void Setup(string layerName, float min, float max, Gradient currentGradient)
    {
        Debug.Log($"[LegendController] Setup for {layerName}. Min: {min}, Max: {max}");
        currentLayerName = layerName;
        
        // Update Texts
        if (minText != null) minText.text = min.ToString("F2");
        if (maxText != null) maxText.text = max.ToString("F2");

        // Update Gradient Image
        UpdateGradientVisual(currentGradient);
    }

    public void UpdateGradientVisual(Gradient grad)
    {
        if (gradientImage != null && grad != null)
        {
            if (gradientImage.texture != null) Destroy(gradientImage.texture);
            gradientImage.texture = GradientManager.GradientToTexture(grad);
        }
        else
        {
             Debug.LogWarning("[LegendController] GradientImage or Gradient is null");
        }
    }

    public void ToggleExpand()
    {
        isExpanded = !isExpanded;
        Debug.Log($"[LegendController] ToggleExpand. New State: {isExpanded}");
        
        if (legendContainer != null) 
        {
            legendContainer.SetActive(isExpanded);
        }
        else
        {
            Debug.LogError("[LegendController] LegendContainer reference is missing!");
        }
        
        // Update Button Icon (Optional)
        if (expandButton != null)
        {
            var txt = expandButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = isExpanded ? "-" : "+";
        }
    }
}
