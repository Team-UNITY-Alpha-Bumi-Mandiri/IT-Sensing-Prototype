using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Events;

// Dropdown kustom dengan fitur pencarian filter.
public class SearchableDropdown : MonoBehaviour
{
    [Header("UI References")]
    public Button mainButton;         // Tombol pembuka dropdown
    public TMP_Text mainButtonText;   // Teks pada tombol utama
    public GameObject dropdownPanel;  // Panel yang berisi list & search bar
    public TMP_InputField searchInput;// Input field pencarian
    public Transform contentContainer;// Wadah tempat item di-spawn
    public GameObject itemPrefab;     // Prefab item dropdown
    public ScrollRect scrollRect;     // ScrollRect untuk scroll list (opsional, auto-detect)

    [Header("Settings")]
    public string defaultPlaceholder = "Select Option"; // Teks default jika kosong

    [Header("Events")]
    public UnityEvent<string> onValueChanged; // Event saat pilihan berubah

    // Data internal
    private List<string> _allOptions = new List<string>();
    private bool _isOpen = false;
    private RectTransform _contentRect; // Cache RectTransform content

    void Start()
    {
        // Setup awal UI
        if (dropdownPanel) dropdownPanel.SetActive(false);
        if (mainButton) mainButton.onClick.AddListener(ToggleDropdown);
        if (searchInput) searchInput.onValueChanged.AddListener(OnSearchInput);
        
        // Cache content RectTransform
        if (contentContainer) _contentRect = contentContainer as RectTransform;
        
        // Auto-detect ScrollRect jika tidak diassign manual
        if (!scrollRect && dropdownPanel) scrollRect = dropdownPanel.GetComponentInChildren<ScrollRect>();
        
        // Set placeholder default jika teks kosong
        if (mainButtonText && string.IsNullOrEmpty(mainButtonText.text))
            UpdateMainText(defaultPlaceholder);
    }

    // Mengisi daftar opsi dropdown dari luar string list
    public void SetOptions(List<string> options)
    {
        _allOptions = new List<string>(options);
        RefreshList(_allOptions); // Tampilkan semua opsi awal
    }

    // Memilih item: update teks, tutup dropdown, dan trigger event
    public void SelectItem(string item)
    {
        UpdateMainText(item);
        if (_isOpen) ToggleDropdown(); // Tutup jika sedang terbuka
        onValueChanged?.Invoke(item);
    }

    // Buka/Tutup panel dropdown
    public void ToggleDropdown()
    {
        if (!dropdownPanel) return;
        
        _isOpen = !dropdownPanel.activeSelf;
        dropdownPanel.SetActive(_isOpen);

        if (_isOpen)
        {
            // Reset pencarian saat dibuka
            if (searchInput) { searchInput.text = ""; searchInput.ActivateInputField(); }
            RefreshList(_allOptions); // Tampilkan semua item kembali
            // Reset scroll ke atas
            if (scrollRect) scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    // Filter list berdasarkan query input
    private void OnSearchInput(string query)
    {
        // Jika kosong tampilkan semua, jika tidak filter yang mengandung teks query (case-insensitive)
        var filtered = string.IsNullOrEmpty(query) ? _allOptions : _allOptions.FindAll(x => x.ToLower().Contains(query.ToLower()));
        RefreshList(filtered);
    }

    // Render ulang daftar item di UI
    private void RefreshList(List<string> displayOptions)
    {
        if (!contentContainer || !itemPrefab) return;

        // Hapus item-item lama
        foreach (Transform child in contentContainer) Destroy(child.gameObject);

        // Buat item baru
        foreach (string opt in displayOptions)
        {
            var obj = Instantiate(itemPrefab, contentContainer);
            // Setup script item jika ada, atau fallback manual
            if (obj.TryGetComponent(out SearchableDropdownItem itemScript))
                itemScript.Setup(opt, SelectItem); 
            else
            {
                // Fallback sederhana jika prefab tidak pakai script khusus
                var txt = obj.GetComponentInChildren<TMP_Text>();
                if (txt) txt.text = opt;
                obj.GetComponent<Button>()?.onClick.AddListener(() => SelectItem(opt));
            }
        }
        
        // Rebuild layout di frame berikutnya agar ukuran content ter-update dengan benar
        StartCoroutine(RebuildLayoutDelayed());
    }

    // Coroutine untuk rebuild layout - diperlukan karena Destroy baru execute di akhir frame
    private IEnumerator RebuildLayoutDelayed()
    {
        yield return null; // Tunggu 1 frame agar Destroy selesai
        
        if (_contentRect)
        {
            // Rebuild layout dari content ke atas
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
            Canvas.ForceUpdateCanvases();
            
            // Update ScrollRect agar mengenali ukuran content baru
            if (scrollRect)
            {
                scrollRect.verticalNormalizedPosition = 1f; // Reset ke atas
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.GetComponent<RectTransform>());
            }
        }
    }

    // Update teks visual tombol utama
    private void UpdateMainText(string text) { if (mainButtonText) mainButtonText.text = text; }
}
