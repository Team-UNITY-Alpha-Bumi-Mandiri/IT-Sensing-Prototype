using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// DrawModeButton - Tombol untuk mengaktifkan mode gambar
// ============================================================
// Mode: Point, Line, Polygon, Delete
// Tombol hanya interactable jika:
// 1. Ada layer aktif (dari label "Layer : xxx")
// 2. Layer tersebut ON di project properties
// 3. Semua layer lain OFF (hanya satu layer yang boleh aktif)
// ============================================================
public class DrawModeButton : MonoBehaviour
{
    public DrawTool.DrawMode mode;  // Mode yang diaktifkan tombol ini
    public DrawTool drawTool;       // Referensi ke DrawTool
    
    [Header("Settings")]
    public bool isEditLayerButton;
    
    [Header("Validation")]
    public TextMeshProUGUI layerInfoText;  // Label "Layer : xxx" untuk cek layer aktif
    public ProjectManager projectManager;   // Manager project untuk cek properties
    
    Button _btn;  // Referensi ke Button component

    [Header("Visual")]
    public Color activeColor = new Color(0.5f, 1f, 0.5f);
    private Color originalNormalColor;

    [Header("Validation (Edit Mode)")]
    public PropertyPanel propertyPanel;

    void Start()
    {
        if (TryGetComponent(out _btn))
        {
            _btn.onClick.AddListener(OnClick);
            originalNormalColor = _btn.colors.normalColor;
        }
        
        // Auto-find ProjectManager jika tidak di-assign
        if (projectManager == null) projectManager = FindObjectOfType<ProjectManager>();

        if (propertyPanel == null) propertyPanel = FindObjectOfType<PropertyPanel>();
    }

    void Update()
    {
        // Update interactable berdasarkan validasi
        if (_btn != null) _btn.interactable = IsValid();
    }

    // Validasi kondisi untuk enable tombol
    bool IsValid()
    {
        if (isEditLayerButton)
        {
            // Valid jika ada layer yang sedang diedit
            return propertyPanel != null && !string.IsNullOrEmpty(propertyPanel.editLayerName);
        }

        if (layerInfoText == null || projectManager == null) return false;
        
        var proj = projectManager.GetCurrentProject();
        if (proj == null) return false;

        // Parse nama layer dari label "Layer : xxx"
        string txt = layerInfoText.text;
        if (string.IsNullOrEmpty(txt) || !txt.StartsWith("Layer : ")) return false;
        
        string layerName = txt.Substring("Layer : ".Length).Trim();
        var props = proj.GetProps();

        // Layer harus ada dan ON
        if (!props.ContainsKey(layerName) || !props[layerName].value) return false;
        
        // Semua layer lain harus OFF
        foreach (var kv in props)
        {
            if (kv.Key != layerName && kv.Value.value) return false;
        }
        
        return true;
    }

    // Dipanggil saat tombol diklik
    void OnClick()
    {
        if (!_btn.interactable) return;

        switch (mode)
        {
            case DrawTool.DrawMode.Edit:
                ToggleEditMode();
                break;
            case DrawTool.DrawMode.Polygon:
            case DrawTool.DrawMode.Point:
            case DrawTool.DrawMode.Line:
            case DrawTool.DrawMode.Delete:
            case DrawTool.DrawMode.Cut:
                ToggleDrawMode();
                break;
        }
    }

    void ToggleEditMode()
    {
        if (!drawTool.IsModeActive(DrawTool.DrawMode.Edit))
        {
            string targetLayer = "";

            if (isEditLayerButton && propertyPanel != null) targetLayer = propertyPanel.editLayerName;

            drawTool.currentDrawingLayer = targetLayer; 
            drawTool.ActivateMode(DrawTool.DrawMode.Edit);
            drawTool.EditLayer(targetLayer);
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
            string targetLayer = "";

            if (isEditLayerButton)
            {
                if (propertyPanel != null) targetLayer = propertyPanel.editLayerName;
            }
            else
            {
                if (layerInfoText != null && layerInfoText.text.StartsWith("Layer : "))
                {
                    targetLayer = layerInfoText.text.Substring("Layer : ".Length).Trim();
                }
            }

            drawTool.currentDrawingLayer = targetLayer;
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

        if (isActive && isEditLayerButton && propertyPanel != null)
        {
             if (drawTool.currentDrawingLayer != propertyPanel.editLayerName)
                 isActive = false;
        }
        
        var colors = _btn.colors;
        colors.normalColor = isActive ? activeColor : originalNormalColor;
        colors.selectedColor = isActive ? activeColor : originalNormalColor;
        _btn.colors = colors;
    }
}
