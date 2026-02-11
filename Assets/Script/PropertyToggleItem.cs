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
    PropertyPanel _parentPanel;
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

    [Header("Actions")]
    public Button editModeButton;        // Tombol buka edit popup
    public Button atributButton;         // Tombol buka popup atribut/ID list

    // [REMOVED] Edit Layer Actions and Callbacks for rollback

    // Callback internal
    string _name;                        // Nama property saat ini
    Action<string, bool> _onChange;      // Callback saat toggle berubah
    Action<string, string> _onRename;    // Callback saat rename (oldName, newName)
    Action<string> _onDelete;            // Callback saat delete

    // Properti untuk akses nama dari luar
    public string PropertyName => _name;

    void Start()
    {
        if (editModeButton != null) editModeButton.onClick.AddListener(ToggleEditPopup);
        if (atributButton != null) atributButton.onClick.AddListener(ToggleAtributPopup);
    }

    // Setup toggle dengan data dan callbacks
    // name     - Nama property
    // value    - Nilai awal toggle (on/off)
    // onChange - Callback saat nilai berubah
    // onRename - Callback saat rename (opsional)
    // onDelete - Callback saat delete (opsional)
    public void Setup(string name, bool value, Action<string, bool> onChange, Action<string, string> onRename = null, Action<string> onDelete = null, PropertyPanel parentPanel = null)
    {
        _name = name;
        _onChange = onChange;
        _onRename = onRename;
        _onDelete = onDelete;
        _parentPanel = parentPanel;

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

    void ToggleEditPopup()
    {
        if (_parentPanel?.sharedEditPopup == null) return;
        
        bool willBeActive = !_parentPanel.sharedEditPopup.activeSelf;
        _parentPanel.sharedEditPopup.SetActive(willBeActive);
        _parentPanel.SetEditMode(_name, willBeActive);
    }

    public void SetInteractable(bool interactable)
    {
        if (toggle != null) toggle.interactable = interactable;
        if (renameButton != null) renameButton.interactable = interactable;
        if (deleteButton != null) deleteButton.interactable = interactable;
        if (editModeButton != null) editModeButton.interactable = interactable;
        if (atributButton != null) atributButton.interactable = interactable;
    }

    // Toggle popup atribut dan populate dengan ID dari layer ini
    void ToggleAtributPopup()
    {
        if (_parentPanel?.sharedAtributPopup == null) return;
        
        bool willBeActive = !_parentPanel.sharedAtributPopup.activeSelf;
        _parentPanel.sharedAtributPopup.SetActive(willBeActive);
        
        if (willBeActive)
        {
            // Populate popup dengan ID dari layer ini
            _parentPanel.ShowAtribut(_name);
        }
    }
}
