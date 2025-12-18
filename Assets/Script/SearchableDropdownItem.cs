using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// Mewakili satu item/baris dalam list dropdown yang bisa dicari.
public class SearchableDropdownItem : MonoBehaviour
{
    public TMP_Text itemText; // Label teks item
    public Button button;     // Tombol untuk memilih item

    // Mengatur isi item (teks & logika klik).
    // value: Teks yang ditampilkan.
    // onSelectAction: Fungsi callback saat item ini diklik.
    public void Setup(string value, Action<string> onSelectAction)
    {
        // Set teks label jika komponen ada
        if (itemText) itemText.text = value;
        
        // Setup tombol
        if (button)
        {
            button.onClick.RemoveAllListeners(); // Bersihkan listener lama
            // Tambahkan listener baru yang memanggil callback dengan value item ini
            button.onClick.AddListener(() => onSelectAction?.Invoke(value));
        }
    }
}
