using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// ============================================================
// GeeSceneItem - Item hasil pencarian Google Earth Engine
// ============================================================
// Menampilkan thumbnail, tanggal, dan mendukung seleksi.
// Digunakan dalam list hasil search di GeeDownloadController.
// ============================================================
public class GeeSceneItem : MonoBehaviour
{
    [Header("UI Components")]
    public RawImage thumbnailImage;      // Gambar thumbnail scene
    public TMP_Text infoText;            // Label info (tanggal)
    public Button selectionButton;       // Button untuk memilih scene
    public GameObject highlightEffect;   // Opsional: border/glow saat selected
    
    [Header("Color Settings")]
    public bool changeButtonColor = true;                    // Apakah ubah warna saat selected
    public Color normalColor = Color.white;                  // Warna default
    public Color selectedColor = new Color(0.2f, 0.6f, 1f);  // Warna saat selected (biru)

    string _sceneId;                          // ID scene dari GEE
    Action<string, GeeSceneItem> _onSelected; // Callback saat item dipilih

    // Setup item dengan data scene
    // sceneId    - ID unik scene dari GEE
    // date       - Tanggal akuisisi (ditampilkan di label)
    // tex        - Thumbnail texture (bisa null)
    // onSelected - Callback saat item dipilih, terima (sceneId, thisItem)
    public void Setup(string sceneId, string date, Texture2D tex, Action<string, GeeSceneItem> onSelected)
    {
        _sceneId = sceneId;
        _onSelected = onSelected;
        
        if (infoText != null) infoText.text = date;
        if (thumbnailImage != null && tex != null) thumbnailImage.texture = tex;

        if (selectionButton != null)
        {
            selectionButton.onClick.RemoveAllListeners();
            selectionButton.onClick.AddListener(() => _onSelected?.Invoke(_sceneId, this));
            
            // Set warna awal
            if (changeButtonColor)
            {
                var img = selectionButton.GetComponent<Image>();
                if (img != null) img.color = normalColor;
            }
        }
    }

    // Set state seleksi (untuk radio button behavior)
    // Dipanggil dari parent untuk menandai item ini selected/unselected
    public void SetSelected(bool isSelected)
    {
        // Ubah warna button
        if (changeButtonColor && selectionButton != null)
        {
            var img = selectionButton.GetComponent<Image>();
            if (img != null) img.color = isSelected ? selectedColor : normalColor;
        }
        
        // Toggle highlight effect
        if (highlightEffect != null) highlightEffect.SetActive(isSelected);
    }
}
