using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// ============================================================
// SearchableDropdownItem - Item dalam SearchableDropdown
// ============================================================
// Digunakan sebagai prefab untuk setiap opsi dalam dropdown.
// Menampilkan teks dan memanggil callback saat diklik.
// ============================================================
public class SearchableDropdownItem : MonoBehaviour
{
    public TMP_Text itemText;  // Label untuk menampilkan nama item
    public Button button;      // Button untuk interaksi klik

    // Setup item dengan teks dan callback
    // text    - Teks yang ditampilkan
    // onClick - Callback dipanggil dengan teks item saat diklik
    public void Setup(string text, Action<string> onClick)
    {
        if (itemText != null) itemText.text = text;
        
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(text));
        }
    }
}
