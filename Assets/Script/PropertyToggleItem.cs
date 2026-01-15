using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// ============================================================
// PropertyToggleItem - Item toggle di panel property
// ============================================================
// Menampilkan toggle dengan label, mendukung rename dan delete.
// Contoh tampilan: "[x] Layer Name  [Rename] [Delete]"
// ============================================================
public class PropertyToggleItem : MonoBehaviour
{
    [Header("UI Components")]
    public Toggle toggle;           // Toggle checkbox
    public TMP_Text labelText;      // Label nama property

    [Header("Rename & Delete")]
    public Button renameButton;     // Tombol buka rename UI
    public Button deleteButton;     // Tombol hapus property
    public GameObject renamePanel;  // Panel rename (hidden by default)
    public TMP_InputField renameInput;   // Input nama baru
    public Button confirmRenameBtn;      // Tombol konfirmasi rename
    public Button cancelRenameBtn;       // Tombol batal rename

    // Callback internal
    string _name;                        // Nama property saat ini
    Action<string, bool> _onChange;      // Callback saat toggle berubah
    Action<string, string> _onRename;    // Callback saat rename (oldName, newName)
    Action<string> _onDelete;            // Callback saat delete

    // Properti untuk akses nama dari luar
    public string PropertyName => _name;

    // Setup toggle dengan data dan callbacks
    // name     - Nama property
    // value    - Nilai awal toggle (on/off)
    // onChange - Callback saat nilai berubah
    // onRename - Callback saat rename (opsional)
    // onDelete - Callback saat delete (opsional)
    public void Setup(string name, bool value, Action<string, bool> onChange, 
                      Action<string, string> onRename = null, Action<string> onDelete = null)
    {
        _name = name;
        _onChange = onChange;
        _onRename = onRename;
        _onDelete = onDelete;

        if (labelText != null) labelText.text = name;

        // Setup toggle
        if (toggle != null)
        {
            toggle.isOn = value;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(val => _onChange?.Invoke(_name, val));
        }

        // Setup buttons
        SetupButton(renameButton, OpenRenameUI);
        SetupButton(deleteButton, () => _onDelete?.Invoke(_name));
        SetupButton(confirmRenameBtn, OnRenameConfirm);
        SetupButton(cancelRenameBtn, CloseRenameUI);

        // Sembunyikan rename panel
        if (renamePanel != null) renamePanel.SetActive(false);
    }

    // Helper: Setup button dengan listener
    void SetupButton(Button btn, Action action)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => action());
    }

    // Buka UI rename
    void OpenRenameUI()
    {
        if (renamePanel == null) return;
        renamePanel.SetActive(true);
        if (renameInput != null) renameInput.text = _name;
    }

    // Tutup UI rename
    void CloseRenameUI()
    {
        if (renamePanel != null) renamePanel.SetActive(false);
    }

    // Konfirmasi rename
    void OnRenameConfirm()
    {
        if (renameInput != null && !string.IsNullOrEmpty(renameInput.text))
        {
            _onRename?.Invoke(_name, renameInput.text);
            CloseRenameUI();
        }
    }

    // Set nilai toggle tanpa trigger event (untuk update dari luar)
    public void SetValueWithoutNotify(bool value)
    {
        if (toggle != null) toggle.SetIsOnWithoutNotify(value);
    }
}
