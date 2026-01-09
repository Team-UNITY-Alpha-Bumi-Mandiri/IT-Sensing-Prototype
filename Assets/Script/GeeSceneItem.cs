using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =========================================
// Script untuk item di daftar hasil GEE
// Menampilkan thumbnail, tanggal, dan awan
// =========================================
public class GeeSceneItem : MonoBehaviour
{
    [Header("UI Components")]
    public RawImage thumbnailImage;
    public TMP_Text infoText;
    public Button selectionButton;
    public GameObject highlightEffect; // Opsional: tetap bisa dipakai untuk border/overlay
    
    [Header("Color Settings")]
    public bool changeButtonColor = true;
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(0.2f, 0.6f, 1f); // Biru sebagai default

    private string _sceneId;
    private System.Action<string, GeeSceneItem> _onSelected;

    public void Setup(string sceneId, string date, float cloud, Texture2D tex, System.Action<string, GeeSceneItem> onSelected)
    {
        _sceneId = sceneId;
        _onSelected = onSelected;
        
        if (infoText != null) infoText.text = date;
        
        if (thumbnailImage != null && tex != null)
        {
            thumbnailImage.texture = tex;
        }

        // Gunakan Button untuk seleksi
        if (selectionButton != null)
        {
            selectionButton.onClick.RemoveAllListeners();
            selectionButton.onClick.AddListener(() => {
                _onSelected?.Invoke(_sceneId, this);
            });
            
            // Set warna awal
            if (changeButtonColor) selectionButton.GetComponent<Image>().color = normalColor;
        }
    }

    public void SetSelected(bool isSelected)
    {
        UpdateHighlight(isSelected);
    }

    private void UpdateHighlight(bool isSelected)
    {
        // 1. Ganti warna Button (jika checkbox aktif)
        if (changeButtonColor && selectionButton != null)
        {
            Image btnImg = selectionButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = isSelected ? selectedColor : normalColor;
            }
        }

        // 2. Tetap dukung HighlightEffect (jika ada yang dipasang)
        if (highlightEffect != null) highlightEffect.SetActive(isSelected);
    }
}
