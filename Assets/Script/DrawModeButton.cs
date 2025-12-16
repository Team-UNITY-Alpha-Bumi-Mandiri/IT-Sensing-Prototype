using UnityEngine;
using UnityEngine.UI;

// Script tombol untuk pilih mode gambar (Point, Line, Polygon, Delete)
public class DrawModeButton : MonoBehaviour
{
    public DrawTool.DrawMode targetMode;
    public DrawTool drawTool;
    public Color activeColor = new Color(0.1f, 0.55f, 0.28f), inactiveColor = Color.white;
    
    Button btn;
    Image img;
    bool isOn;

    void Start()
    {
        if (TryGetComponent(out btn)) btn.onClick.AddListener(OnClick);
        TryGetComponent(out img);
        UpdateVisuals();
    }

    // Toggle mode saat diklik
    void OnClick()
    {
        isOn = !isOn;
        if (isOn) drawTool?.ActivateMode(targetMode);
        else drawTool?.DeactivateMode(targetMode);
        UpdateVisuals();
    }

    // Ubah warna tombol sesuai status aktif
    void UpdateVisuals() => img.color = isOn ? activeColor : inactiveColor;

    // Sinkronisasi status tombol dengan mode yang aktif di DrawTool
    void Update()
    {
        if (!drawTool) return;
        bool active = drawTool.IsModeActive(targetMode);
        if (active != isOn) { isOn = active; UpdateVisuals(); }
    }
}
