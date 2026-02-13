using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// DrawModeButton - Tombol untuk mengaktifkan mode gambar
// ============================================================
// Mode: Point, Line, Polygon, Delete, Cut, Edit
// Mendapat layer dari:
// - propertyPanel.editLayerName (jika ada)
// - ATAU dari layerInfoText "Layer : xxx"
// ============================================================
public class DrawModeButton : MonoBehaviour
{
    public DrawTool.DrawMode mode;
    public DrawTool drawTool;
    
    [Header("Layer Sources")]
    public PropertyPanel propertyPanel;
    public TextMeshProUGUI layerInfoText;
    public ProjectManager projectManager;
    
    [Header("Visual")]
    public Color activeColor = new Color(0.5f, 1f, 0.5f);
    Color originalColor;
    Button _btn;

    void Start()
    {
        if (TryGetComponent(out _btn))
        {
            _btn.onClick.AddListener(OnClick);
            originalColor = _btn.colors.normalColor;
        }
        if (projectManager == null) projectManager = Object.FindFirstObjectByType<ProjectManager>();
        if (propertyPanel == null) propertyPanel = Object.FindFirstObjectByType<PropertyPanel>();
    }

    void Update()
    {
        if (_btn != null) _btn.interactable = IsValid();
    }

    // Ambil layer name dari sumber yang tersedia
    string GetTargetLayer()
    {
        // Prioritas 1: dari PropertyPanel (edit popup)
        if (propertyPanel != null && !string.IsNullOrEmpty(propertyPanel.editLayerName))
            return propertyPanel.editLayerName;
        
        // Prioritas 2: dari layerInfoText
        if (layerInfoText != null && layerInfoText.text.StartsWith("Layer : "))
            return layerInfoText.text.Substring("Layer : ".Length).Trim();
        
        return "";
    }

    bool IsValid()
    {
        string layer = GetTargetLayer();
        
        // Jika ada layer, valid
        if (!string.IsNullOrEmpty(layer)) return true;
        
        // Jika tidak ada layer, cek apakah ada project untuk validasi layer
        if (projectManager == null) return false;
        var proj = projectManager.GetCurrentProject();
        if (proj == null) return false;
        
        return false;
    }

    void OnClick()
    {
        if (!_btn.interactable) return;

        switch (mode)
        {
            case DrawTool.DrawMode.Edit:
                ToggleEditMode();
                break;
            default:
                ToggleDrawMode();
                break;
        }
    }

    void ToggleEditMode()
    {
        if (!drawTool.IsModeActive(DrawTool.DrawMode.Edit))
        {
            string layer = GetTargetLayer();
            drawTool.currentDrawingLayer = layer; 
            drawTool.ActivateMode(DrawTool.DrawMode.Edit);
            drawTool.EditLayer(layer);
        }
        else
        {
            drawTool.CancelEditLayer();
            drawTool.DeactivateMode(DrawTool.DrawMode.Edit);
            drawTool.currentDrawingLayer = "";
        }
    }

    void ToggleDrawMode()
    {
        if (!drawTool.IsModeActive(mode))
        {
            drawTool.currentDrawingLayer = GetTargetLayer();
            drawTool.ActivateMode(mode);
        }
        else
        {
            drawTool.DeactivateMode(mode);
            drawTool.currentDrawingLayer = "";
        }
    }

    void LateUpdate()
    {
        if (_btn == null) return;
        
        bool isActive = drawTool.IsModeActive(mode);
        
        // Cek apakah mode aktif untuk layer yang sama
        string myLayer = GetTargetLayer();
        if (isActive && !string.IsNullOrEmpty(myLayer))
        {
            if (drawTool.currentDrawingLayer != myLayer)
                isActive = false;
        }
        
        var colors = _btn.colors;
        colors.normalColor = isActive ? activeColor : originalColor;
        colors.selectedColor = isActive ? activeColor : originalColor;
        _btn.colors = colors;
    }
}
