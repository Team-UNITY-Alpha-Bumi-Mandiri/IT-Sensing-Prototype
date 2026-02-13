using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// LayerInputController - Controller untuk menambah layer baru
// ============================================================
// Tombol aktif hanya jika:
// 1. Ada project aktif
// 2. Dropdown mode = "New"
// 3. Input nama tidak kosong
// ============================================================
[RequireComponent(typeof(Button))]
public class LayerInputController : MonoBehaviour
{
    [Header("External References")]
    public ProjectManager projectManager;    // Manager project untuk AddProperty
    public TMP_Dropdown modeDropdown;        // Dropdown pilihan mode (harus = "New")
    public TMP_InputField layerNameInput;    // Input nama layer baru
    public TextMeshProUGUI targetLayerLabel; // Label yang akan diupdate setelah submit

    [Header("Settings")]
    public string newOptionName = "New";     // Nama opsi "New" di dropdown

    Button _btn;  // Referensi ke Button component

    void Start()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnSubmit);
        
        // Auto-find ProjectManager jika tidak di-assign
        if (projectManager == null)
            projectManager = Object.FindFirstObjectByType<ProjectManager>();
    }

    void Update()
    {
        // Update interactable berdasarkan validasi
        if (_btn != null) _btn.interactable = IsValid();
    }

    // Validasi kondisi untuk enable tombol
    bool IsValid()
    {
        // 1. Cek project aktif
        if (projectManager?.GetCurrentProject() == null) return false;

        // 2. Cek dropdown terpilih "New"
        if (modeDropdown == null) return false;
        if (modeDropdown.value < 0 || modeDropdown.value >= modeDropdown.options.Count) return false;
        if (modeDropdown.options[modeDropdown.value].text != newOptionName) return false;

        // 3. Cek input tidak kosong
        return !string.IsNullOrEmpty(layerNameInput?.text);
    }

    // Dipanggil saat tombol diklik
    void OnSubmit()
    {
        if (layerNameInput == null || targetLayerLabel == null) return;
        
        // Update label target
        targetLayerLabel.text = "Layer : " + layerNameInput.text;
        
        // Tambah property ke project (default ON)
        projectManager?.AddProperty(layerNameInput.text, true, false);
        
        Debug.Log($"[LayerInputController] Layer dibuat: {layerNameInput.text}");
    }
}
