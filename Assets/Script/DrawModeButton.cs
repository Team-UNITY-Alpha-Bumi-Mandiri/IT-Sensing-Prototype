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
    
    [Header("Validation")]
    public TextMeshProUGUI layerInfoText;  // Label "Layer : xxx" untuk cek layer aktif
    public ProjectManager projectManager;   // Manager project untuk cek properties
    
    Button _btn;  // Referensi ke Button component

    [Header("Visual")]
    public Color activeColor = new Color(0.5f, 1f, 0.5f);
    private Color originalNormalColor;

    void Start()
    {
        if (TryGetComponent(out _btn))
        {
            _btn.onClick.AddListener(OnClick);
            originalNormalColor = _btn.colors.normalColor;
        }
        
        // Auto-find ProjectManager jika tidak di-assign
        if (projectManager == null)
            projectManager = FindObjectOfType<ProjectManager>();
    }

    void Update()
    {
        // Update interactable berdasarkan validasi
        if (_btn != null) _btn.interactable = IsValid();
    }

    // Validasi kondisi untuk enable tombol
    bool IsValid()
    {
        if (layerInfoText == null || projectManager == null) return false;
        
        var proj = projectManager.GetCurrentProject();
        if (proj == null) return false;

        // Parse nama layer dari label "Layer : xxx"
        string txt = layerInfoText.text;
        if (string.IsNullOrEmpty(txt) || !txt.StartsWith("Layer : ")) return false;
        
        string layerName = txt.Substring("Layer : ".Length).Trim();
        var props = proj.GetProps();

        // Layer harus ada dan ON
        if (!props.ContainsKey(layerName) || !props[layerName]) return false;
        
        // Semua layer lain harus OFF
        foreach (var kv in props)
        {
            if (kv.Key != layerName && kv.Value) return false;
        }
        
        return true;
    }

    // Dipanggil saat tombol diklik
    void OnClick()
    {
        if (!_btn.interactable) return;

        // Toggle mode: aktifkan jika belum aktif, nonaktifkan jika sudah aktif
        if (!drawTool.IsModeActive(mode))
        {
            // Set layer name untuk objek yang akan digambar
            if (layerInfoText != null && layerInfoText.text.StartsWith("Layer : "))
                drawTool.currentDrawingLayer = layerInfoText.text.Substring("Layer : ".Length).Trim();

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
        var colors = _btn.colors;
        colors.normalColor = isActive ? activeColor : originalNormalColor;
        colors.selectedColor = isActive ? activeColor : originalNormalColor;
        _btn.colors = colors;
    }
}
