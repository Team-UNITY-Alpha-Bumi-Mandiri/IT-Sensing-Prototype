using UnityEngine;
using UnityEngine.UI;

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
    
    // Warna saat aktif (hijau)
    public Color onColor = new Color(0.1f, 0.55f, 0.28f);
    
    // Warna saat tidak aktif (putih)
    public Color offColor = Color.white;

    // Komponen internal
    Image img;

    void Start()
    {
        // Ambil komponen Image
        img = GetComponent<Image>();

        // Tambah listener ke tombol
        if (TryGetComponent(out Button btn))
        {
            btn.onClick.AddListener(OnClick);
        }

        // Set warna awal
        UpdateColor();
    }

    // Saat diklik: aktifkan mode jika belum aktif, matikan jika sudah
    void OnClick()
    {
        if (!drawTool.IsModeActive(mode))
        {
            drawTool.ActivateMode(mode);
        }
        else
        {
            drawTool.DeactivateMode(mode);
        }

        UpdateColor();
    }

    // Update warna tombol sesuai status
    void UpdateColor()
    {
        if (drawTool.IsModeActive(mode))
        {
            img.color = onColor;
        }
        else
        {
            img.color = offColor;
        }
    }

    // Cek terus tiap frame untuk sync warna (jika diubah dari luar)
    void Update()
    {
        if (drawTool != null && img != null)
        {
            UpdateColor();
        }
    }
}
