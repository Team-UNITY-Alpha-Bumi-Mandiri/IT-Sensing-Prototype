using UnityEngine;
using UnityEngine.UI; // Masih diperlukan untuk komponen Toggle
using TMPro; // PENTING: Untuk TextMeshPro
using System.Collections.Generic;

public class SearchableTMPDropdown : MonoBehaviour
{
    // Komponen Dropdown TextMeshPro utama
    [Header("Components")]
    [Tooltip("Seret komponen TMP_Dropdown utama Anda.")]
    public TMP_Dropdown targetDropdown;

    // InputField TextMeshPro yang Anda tambahkan ke UI Dropdown
    [Tooltip("Seret komponen TMP_InputField yang Anda letakkan di atas daftar opsi.")]
    public TMP_InputField searchInput; 
    
    // Parent dari semua item opsi (Biasanya objek 'Content' atau 'Viewport')
    [Tooltip("Seret RectTransform yang merupakan Parent langsung dari semua item opsi (Toggle).")]
    public RectTransform itemContainer; 

    private List<string> allOptionTexts;

    void Start()
    {
        if (targetDropdown == null || searchInput == null || itemContainer == null)
        {
            Debug.LogError("Pastikan semua variabel di SearchableTMPDropdown sudah di-assign!");
            return;
        }

        // Simpan teks dari semua opsi yang ada (Diperlukan untuk membandingkan)
        allOptionTexts = new List<string>();
        foreach(var option in targetDropdown.options)
        {
            allOptionTexts.Add(option.text);
        }

        // Tambahkan listener ke InputField
        searchInput.onValueChanged.AddListener(FilterOptions);
        
        // Atur InputField non-aktif pada awalnya
        searchInput.gameObject.SetActive(false); 

        // Tambahkan listener kustom untuk menangani saat Dropdown dibuka dan ditutup
        // TMP_Dropdown tidak memiliki event 'OnOpen', jadi kita perlu cara lain,
        // misal lewat tombol yang membuka dropdown, atau event khusus jika Anda menggunakan UI Extension.
    }

    private void FilterOptions(string searchText)
    {
        searchText = searchText.ToLower();
        
        // Dapatkan semua komponen Toggle yang merupakan child dari itemContainer.
        // Setiap item opsi di TMP Dropdown memiliki Toggle.
        Toggle[] optionToggles = itemContainer.GetComponentsInChildren<Toggle>(true);

        if (optionToggles.Length == 0 || optionToggles.Length != allOptionTexts.Count)
        {
             // Opsi belum dibuat. Item opsi dibuat saat Dropdown pertama kali dibuka.
             // Kita perlu memastikan opsi sudah ada.
             Debug.LogWarning("Opsi Dropdown belum siap. Coba buka Dropdown di runtime dan atur ulang itemContainer jika perlu.");
             return;
        }
        
        // Looping melalui Toggle dan Teks, lalu filter
        for (int i = 0; i < optionToggles.Length; i++)
        {
            // Ambil Toggle untuk mendapatkan GameObject-nya
            Toggle itemToggle = optionToggles[i];
            // Ambil teks opsi yang sesuai berdasarkan indeks
            string itemText = allOptionTexts[i];

            // Cek apakah teks opsi mengandung teks pencarian
            bool matches = itemText.ToLower().Contains(searchText);
            
            // Aktifkan/nonaktifkan GameObject parent dari Toggle (yaitu item opsi itu sendiri)
            itemToggle.gameObject.SetActive(matches);
            
            // Note: Saat item disembunyikan, ScrollRect mungkin perlu di-refresh.
            // Ini biasanya ditangani secara otomatis, tetapi mungkin butuh kode tambahan
            // jika itemContainer memiliki komponen LayoutGroup.
        }
    }
    
    // Dipanggil saat dropdown ditutup
    public void ResetFilter()
    {
        if(searchInput != null)
        {
            searchInput.text = "";
            searchInput.gameObject.SetActive(false); // Sembunyikan InputField
        }
        
        // Panggil filter lagi untuk menampilkan semua item
        FilterOptions("");
    }
}