using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

// ============================================================
// SearchableDropdown - Dropdown dengan fitur search/filter
// ============================================================
// Fitur:
// - Menampilkan list opsi yang bisa dicari/filter
// - Callback saat item dipilih
// - Auto-scroll ke atas saat dibuka
// ============================================================
public class SearchableDropdown : MonoBehaviour
{
    [Header("UI References")]
    public Button mainButton;        // Button utama untuk buka dropdown
    public TMP_Text mainButtonText;  // Label di button utama
    public GameObject dropdownPanel; // Panel dropdown (hidden by default)
    public TMP_InputField searchInput; // Input untuk search/filter
    public Transform content;        // Container untuk item (biasanya Content dalam ScrollView)
    public GameObject itemPrefab;    // Prefab SearchableDropdownItem
    public ScrollRect scrollRect;    // ScrollRect untuk scroll handling

    [Header("Settings")]
    public string placeholder = "Pilih...";  // Placeholder text saat belum ada yang dipilih

    // Event saat value berubah (item dipilih)
    public UnityEvent<string> onValueChanged;

    List<string> options = new List<string>();  // Daftar opsi
    bool isOpen = false;                        // Status dropdown terbuka/tertutup
    RectTransform contentRect;                  // RectTransform content untuk rebuild layout

    void Start()
    {
        // Sembunyikan dropdown di awal
        if (dropdownPanel != null) dropdownPanel.SetActive(false);
        
        // Setup listeners
        mainButton?.onClick.AddListener(ToggleDropdown);
        searchInput?.onValueChanged.AddListener(Filter);
        
        contentRect = content as RectTransform;
        
        // Auto-find ScrollRect jika tidak di-assign
        if (scrollRect == null && dropdownPanel != null)
            scrollRect = dropdownPanel.GetComponentInChildren<ScrollRect>();

        // Set placeholder
        if (mainButtonText != null && string.IsNullOrEmpty(mainButtonText.text))
            mainButtonText.text = placeholder;
    }

    // Set daftar opsi dari luar
    public void SetOptions(List<string> opts)
    {
        options = new List<string>(opts);
        ShowItems(options);
    }

    // Pilih item tertentu (set label dan invoke event)
    public void SelectItem(string item)
    {
        if (mainButtonText != null) mainButtonText.text = item;
        if (isOpen) ToggleDropdown();
        onValueChanged?.Invoke(item);
    }

    // Toggle buka/tutup dropdown
    public void ToggleDropdown()
    {
        if (dropdownPanel == null) return;

        isOpen = !dropdownPanel.activeSelf;
        dropdownPanel.SetActive(isOpen);

        if (isOpen)
        {
            // Reset search dan focus
            if (searchInput != null)
            {
                searchInput.text = "";
                searchInput.ActivateInputField();
            }
            ShowItems(options);
            
            // Scroll ke atas
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    // Filter opsi berdasarkan query
    void Filter(string query)
    {
        var hasil = string.IsNullOrEmpty(query) 
            ? options 
            : options.FindAll(x => x.ToLower().Contains(query.ToLower()));
        ShowItems(hasil);
    }

    // Tampilkan items dalam dropdown
    void ShowItems(List<string> items)
    {
        if (content == null || itemPrefab == null) return;

        // Hapus item lama
        foreach (Transform child in content) Destroy(child.gameObject);

        // Buat item baru
        foreach (string item in items)
        {
            var obj = Instantiate(itemPrefab, content);
            
            // Coba gunakan SearchableDropdownItem script
            if (obj.TryGetComponent(out SearchableDropdownItem script))
            {
                script.Setup(item, SelectItem);
            }
            else
            {
                // Fallback: setup manual
                var txt = obj.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = item;

                var btn = obj.GetComponent<Button>();
                btn?.onClick.AddListener(() => SelectItem(item));
            }
        }

        StartCoroutine(RebuildLayout());
    }

    // Rebuild layout setelah item berubah (harus di coroutine)
    IEnumerator RebuildLayout()
    {
        yield return null;
        
        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Canvas.ForceUpdateCanvases();
        }
        
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.GetComponent<RectTransform>());
        }
    }
}
