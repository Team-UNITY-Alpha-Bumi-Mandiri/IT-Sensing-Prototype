using UnityEngine;
using UnityEngine.UI;

// Mengontrol tombol UI untuk memilih mode gambar (Point, Line, Polygon, Delete).
public class DrawModeButton : MonoBehaviour
{
    public DrawTool.DrawMode targetMode; // Mode yang akan diaktifkan tombol ini
    public DrawTool drawTool; // Referensi ke script DrawTool utama
    public Color activeColor = new Color(0.1f, 0.55f, 0.28f), inactiveColor = Color.white; // Warna indikator
    
    private Image img; // Komponen Image untuk mengubah warna tombol

    void Start()
    {
        // Ambil komponen Image dan tambahkan listener klik ke tombol
        img = GetComponent<Image>();
        if (TryGetComponent(out Button btn)) btn.onClick.AddListener(OnClick);
        UpdateVisuals(); // Set warna awal
    }

    // Dipanggil saat tombol diklik: Toggle mode di DrawTool.
    void OnClick()
    {
        // Jika mode ini belum aktif -> Aktifkan. Jika sudah -> Matikan.
        if (!drawTool.IsModeActive(targetMode)) drawTool.ActivateMode(targetMode);
        else drawTool.DeactivateMode(targetMode);
        UpdateVisuals(); // Perbarui warna
    }

    // Memperbarui warna tombol berdasarkan status aktif mode ini.
    void UpdateVisuals() => img.color = drawTool.IsModeActive(targetMode) ? activeColor : inactiveColor;

    // Cek setiap frame untuk sinkronisasi warna jika mode diubah dari tempat lain.
    void Update()
    {
        if (drawTool && img.color != (drawTool.IsModeActive(targetMode) ? activeColor : inactiveColor))
            UpdateVisuals(); // Update visual hanya jika status berubah
    }
}
