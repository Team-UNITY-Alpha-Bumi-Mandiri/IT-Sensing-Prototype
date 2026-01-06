using UnityEngine;
using UnityEngine.UI;
using TMPro; // Moved here

// =========================================
// Tombol untuk pilih mode gambar
// Mode: Point, Line, Polygon, Delete
// =========================================
public class DrawModeButton : MonoBehaviour
{
    // Mode yang dikontrol tombol ini
    public DrawTool.DrawMode mode;
    
    // Referensi ke DrawTool
    public DrawTool drawTool;

    // (Color logic removed as requested)
    
    [Header("Validation")]
    public TextMeshProUGUI layerInfoText;
    public ProjectManager projectManager;
    
    Button btn;

    void Start()
    {
        // Tambah listener ke tombol
        if (TryGetComponent(out btn))
        {
            btn.onClick.AddListener(OnClick);
        }
        
        // Auto-find ProjectManager if missing
        if (projectManager == null)
        {
            projectManager = FindObjectOfType<ProjectManager>();
        }
    }

    void Update()
    {
        if (btn == null) return;
        
        // Default not interactable unless valid
        bool isValid = false;

        if (layerInfoText != null && projectManager != null)
        {
            var proj = projectManager.GetCurrentProject();
            if (proj != null)
            {
                // Parse "Layer : Name"
                string txt = layerInfoText.text;
                if (!string.IsNullOrEmpty(txt) && txt.StartsWith("Layer : "))
                {
                    // Parse "Layer : Name" -> "Name"
                    string layerName = txt.Substring("Layer : ".Length).Trim();
                    
                    // Check if property exists and is active
                    var props = proj.GetProps();

                    if (props.ContainsKey(layerName) && props[layerName] == true)
                    {
                        // Check if ALL OTHER toggles are OFF
                        bool othersOff = true;
                        foreach (var kv in props)
                        {
                            if (kv.Key != layerName && kv.Value == true)
                            {
                                othersOff = false;
                                break;
                            }
                        }

                        if (othersOff)
                        {
                            isValid = true;
                        }
                    }
                }
            }
        }
        
        btn.interactable = isValid;
    }

    // Saat diklik: aktifkan mode jika belum aktif, matikan jika sudah
    void OnClick()
    {
        // Double check validation (optional, as interactable handles it)
        if (!btn.interactable) return;
        
        if (!drawTool.IsModeActive(mode))
        {
            // Set layer name ke DrawTool agar objek yang dibuat tahu dia punya siapa
            if (layerInfoText != null && layerInfoText.text.StartsWith("Layer : "))
            {
                drawTool.currentDrawingLayer = layerInfoText.text.Substring("Layer : ".Length).Trim();
            }

            drawTool.ActivateMode(mode);
        }
        else
        {
            drawTool.DeactivateMode(mode);
            drawTool.currentDrawingLayer = ""; // Reset
        }
    }
}
