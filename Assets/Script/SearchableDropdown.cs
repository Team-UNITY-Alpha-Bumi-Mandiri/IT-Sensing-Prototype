using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

// =========================================
// Dropdown dengan fitur search/filter
// Contoh: Dropdown pilih project
// =========================================
public class SearchableDropdown : MonoBehaviour
{
    [Header("UI References")]
    public Button mainButton;          // Tombol pembuka dropdown
    public TMP_Text mainButtonText;    // Teks di tombol utama
    public GameObject dropdownPanel;   // Panel dropdown
    public TMP_InputField searchInput; // Input pencarian
    public Transform content;          // Wadah item-item
    public GameObject itemPrefab;      // Prefab satu item
    public ScrollRect scrollRect;      // Untuk scroll

    [Header("Settings")]
    public string placeholder = "Pilih..."; // Teks default

    // Event saat pilihan berubah
    public UnityEvent<string> onValueChanged;

    // Variabel internal
    List<string> options = new List<string>();
    bool isOpen = false;
    RectTransform contentRect;

    void Start()
    {
        // Sembunyikan dropdown saat mulai
        if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(false);
        }

        // Setup listener tombol utama
        if (mainButton != null)
        {
            mainButton.onClick.AddListener(ToggleDropdown);
        }

        // Setup listener input pencarian
        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(Filter);
        }

        // Cache RectTransform content
        if (content != null)
        {
            contentRect = content as RectTransform;
        }

        // Auto-detect ScrollRect jika tidak diassign
        if (scrollRect == null && dropdownPanel != null)
        {
            scrollRect = dropdownPanel.GetComponentInChildren<ScrollRect>();
        }

        // Set placeholder jika kosong
        if (mainButtonText != null && string.IsNullOrEmpty(mainButtonText.text))
        {
            mainButtonText.text = placeholder;
        }
    }

    // Set daftar opsi dari luar
    public void SetOptions(List<string> opts)
    {
        options = new List<string>(opts);
        ShowItems(options);
    }

    // Pilih item tertentu
    public void SelectItem(string item)
    {
        // Update teks tombol
        if (mainButtonText != null)
        {
            mainButtonText.text = item;
        }

        // Tutup dropdown
        if (isOpen)
        {
            ToggleDropdown();
        }

        // Trigger event
        onValueChanged?.Invoke(item);
    }

    // Buka/tutup dropdown
    public void ToggleDropdown()
    {
        if (dropdownPanel == null) return;

        isOpen = !dropdownPanel.activeSelf;
        dropdownPanel.SetActive(isOpen);

        // Jika baru dibuka
        if (isOpen)
        {
            // Reset pencarian
            if (searchInput != null)
            {
                searchInput.text = "";
                searchInput.ActivateInputField();
            }

            // Tampilkan semua item
            ShowItems(options);

            // Reset scroll ke atas
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }

    // Filter berdasarkan teks pencarian
    void Filter(string query)
    {
        List<string> hasil;

        if (string.IsNullOrEmpty(query))
        {
            // Jika kosong, tampilkan semua
            hasil = options;
        }
        else
        {
            // Filter yang mengandung query (case insensitive)
            hasil = options.FindAll(x => x.ToLower().Contains(query.ToLower()));
        }

        ShowItems(hasil);
    }

    // Tampilkan item-item di dropdown
    void ShowItems(List<string> items)
    {
        if (content == null || itemPrefab == null) return;

        // Hapus item lama
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        // Buat item baru
        foreach (string item in items)
        {
            GameObject obj = Instantiate(itemPrefab, content);

            // Setup menggunakan script SearchableDropdownItem
            if (obj.TryGetComponent(out SearchableDropdownItem script))
            {
                script.Setup(item, SelectItem);
            }
            else
            {
                // Fallback manual jika tidak pakai script
                TMP_Text txt = obj.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = item;

                Button btn = obj.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => SelectItem(item));
            }
        }

        // Rebuild layout
        StartCoroutine(RebuildLayout());
    }

    // Rebuild layout setelah item berubah
    IEnumerator RebuildLayout()
    {
        // Tunggu 1 frame agar Destroy selesai
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
