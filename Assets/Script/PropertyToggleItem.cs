using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// =========================================
// Satu item toggle di panel property
// Contoh: "Night Mode [x]", "Show Grid [ ]"
// =========================================
public class PropertyToggleItem : MonoBehaviour
{
    // Checkbox on/off
    public Toggle toggle;
    
    // Label nama property
    public TMP_Text labelText;

    [Header("Rename & Delete UI")]
    public Button renameButton;
    public Button deleteButton;
    public GameObject renamePanel;     // Wadah input rename (Objek "Rename" di gambar)
    public TMP_InputField renameInput; // Input untuk nama baru
    public Button confirmRenameBtn;    // Tombol Save
    public Button cancelRenameBtn;     // Tombol Cancel

    // Variabel internal
    string _name;
    Action<string, bool> _onChange;
    Action<string, string> _onRename;
    Action<string> _onDelete;

    // Getter nama property
    public string PropertyName => _name;

    // Setup toggle dengan nama, nilai awal, dan callback saat berubah
    public void Setup(string name, bool value, Action<string, bool> onChange, Action<string, string> onRename = null, Action<string> onDelete = null)
    {
        _name = name;
        _onChange = onChange;
        _onRename = onRename;
        _onDelete = onDelete;

        // Set label
        if (labelText != null)
        {
            labelText.text = name;
        }

        // Set toggle
        if (toggle != null)
        {
            toggle.isOn = value;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(OnValueChanged);
        }

        // Setup Buttons
        if (renameButton != null)
        {
            renameButton.onClick.RemoveAllListeners();
            renameButton.onClick.AddListener(OpenRenameUI);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteClick);
        }

        if (confirmRenameBtn != null)
        {
            confirmRenameBtn.onClick.RemoveAllListeners();
            confirmRenameBtn.onClick.AddListener(OnRenameConfirm);
        }

        if (cancelRenameBtn != null)
        {
            cancelRenameBtn.onClick.RemoveAllListeners();
            cancelRenameBtn.onClick.AddListener(CloseRenameUI);
        }

        if (renamePanel != null) renamePanel.SetActive(false);
    }

    void OpenRenameUI()
    {
        if (renamePanel != null)
        {
            renamePanel.SetActive(true);
            if (renameInput != null) renameInput.text = _name;
        }
    }

    void CloseRenameUI()
    {
        if (renamePanel != null) renamePanel.SetActive(false);
    }

    void OnRenameConfirm()
    {
        if (renameInput != null && !string.IsNullOrEmpty(renameInput.text))
        {
            _onRename?.Invoke(_name, renameInput.text);
            CloseRenameUI();
        }
    }

    void OnDeleteClick()
    {
        _onDelete?.Invoke(_name);
    }

    // Callback internal saat toggle berubah
    void OnValueChanged(bool value)
    {
        _onChange?.Invoke(_name, value);
    }

    // Set nilai tanpa trigger event (untuk update dari luar)
    public void SetValueWithoutNotify(bool value)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(value);
        }
    }
}
