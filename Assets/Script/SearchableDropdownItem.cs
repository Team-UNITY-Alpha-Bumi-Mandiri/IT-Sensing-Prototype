using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// =========================================
// Satu item di dalam dropdown list
// Digunakan oleh: SearchableDropdown
// =========================================
public class SearchableDropdownItem : MonoBehaviour
{
    // Teks yang ditampilkan
    public TMP_Text itemText;
    
    // Tombol untuk klik
    public Button button;

    // Setup item ini dengan teks dan aksi saat diklik
    public void Setup(string text, Action<string> onClick)
    {
        // Set teks
        if (itemText != null)
        {
            itemText.text = text;
        }

        // Set aksi tombol
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(text));
        }
    }
}
