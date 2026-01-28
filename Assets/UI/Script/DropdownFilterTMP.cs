using UnityEngine;
using UnityEngine.UI; 
using TMPro; 
using System.Collections.Generic;

public class DropdownFilterTMP : MonoBehaviour
{
    // Komponen Dropdown TextMeshPro utama
    public TMP_Dropdown targetDropdown;

    // InputField TextMeshPro yang Anda tambahkan ke UI Dropdown Template
    public TMP_InputField searchInput; 
    
    // Parent dari semua item opsi (Biasanya objek 'Content' di dalam Template)
    public RectTransform itemContainer; 

    private List<string> allOptionTexts;

    void Start()
    {
        if (targetDropdown == null || searchInput == null || itemContainer == null)
        {
            Debug.LogError("Pastikan semua variabel di SearchableTMPDropdown sudah di-assign!");
            return;
        }

        // 1. Simpan semua teks opsi yang ada
        allOptionTexts = new List<string>();
        foreach(var option in targetDropdown.options)
        {
            allOptionTexts.Add(option.text);
        }

        // 2. Tambahkan listener ke InputField
        searchInput.onValueChanged.AddListener(FilterOptions);
    }

    private void FilterOptions(string searchText)
    {
        searchText = searchText.ToLower();
        
        // Dapatkan semua komponen Toggle (setiap item opsi memiliki satu Toggle)
        Toggle[] optionToggles = itemContainer.GetComponentsInChildren<Toggle>(true);

        if (optionToggles.Length == 0)
        {
             // Opsi belum dibuat. Item dibuat saat Dropdown pertama kali dibuka.
             return;
        }

        // Looping melalui Toggle dan Teks
        for (int i = 0; i < optionToggles.Length; i++)
        {
            Toggle itemToggle = optionToggles[i];
            
            // Perlu penanganan jika jumlah toggle dan opsi tidak sesuai, 
            // tetapi asumsikan indeks i adalah indeks opsi yang benar.
            if (i >= allOptionTexts.Count) continue;
            
            string itemText = allOptionTexts[i];

            // Cek apakah teks opsi mengandung teks pencarian
            bool matches = itemText.ToLower().Contains(searchText);
            
            // Aktifkan/nonaktifkan GameObject item opsi itu sendiri
            itemToggle.gameObject.SetActive(matches);
        }
    }
    
    // Fungsi ini harus dipanggil saat Dropdown ditutup
    public void ResetFilter()
    {
        if(searchInput != null)
        {
            searchInput.text = "";
            
            // Panggil filter lagi untuk menampilkan semua item
            FilterOptions("");
        }
    }
}