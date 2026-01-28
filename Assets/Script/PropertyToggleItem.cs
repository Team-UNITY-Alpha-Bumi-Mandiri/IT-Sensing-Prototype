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

    [Header("Legend")]
    public LegendController legendController;
    public Button plusButton;

    // Callback internal
    string _name;                        // Nama property saat ini
    Action<string, bool> _onChange;      // Callback saat toggle berubah
    Action<string, string> _onRename;    // Callback saat rename (oldName, newName)
    Action<string> _onDelete;            // Callback saat delete

    // Properti untuk akses nama dari luar
    public string PropertyName => _name;

    // State persistence
    private bool _legendActiveState = false;
    private UnityEngine.Events.UnityAction _legendAction;

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

        // Fix Raycasts immediately
        FixRaycasts();

        if (labelText != null) 
        {
            labelText.text = name;
        }

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
        
        // Setup Plus Button for Legend
        if (plusButton != null)
        {
            // Restore state
            plusButton.gameObject.SetActive(_legendActiveState);
            Debug.Log($"[PropertyToggleItem] Setup for {_name}. LegendActive: {_legendActiveState}");

            if (_legendActiveState)
            {
                plusButton.onClick.RemoveAllListeners();
                
                if (_legendAction != null)
                {
                    plusButton.onClick.AddListener(() => {
                        Debug.Log($"[PropertyToggleItem] Executing restored callback for {PropertyName}");
                        _legendAction.Invoke();
                    });
                }
                else
                {
                    plusButton.onClick.AddListener(() => {
                        if (legendController == null) legendController = FindObjectOfType<LegendController>();
                        if (legendController != null) legendController.ToggleExpand();
                    });
                }
            }
        }
        else
        {
            Debug.LogWarning($"[PropertyToggleItem] PlusButton is null in Setup for {_name}");
        }

        // Sembunyikan rename panel
        if (renamePanel != null) renamePanel.SetActive(false);
    }

    // Helper untuk memastikan raycast benar
    void FixRaycasts()
    {
        if (labelText != null)
        {
            var g = labelText.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = false;
        }

        if (plusButton != null)
        {
            var img = plusButton.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;

            // Disable raycast on children (text/icon) to prevent blocking
            foreach (var child in plusButton.GetComponentsInChildren<Graphic>())
            {
                if (child != img) child.raycastTarget = false;
            }
        }
    }

    // Setup tombol plus untuk legend dengan callback khusus
    public void SetupLegend(bool isActive, UnityEngine.Events.UnityAction onPlusClicked)
    {
        _legendActiveState = isActive;
        _legendAction = onPlusClicked;

        if (plusButton == null) 
        {
            Debug.LogWarning($"[PropertyToggleItem] PlusButton is NULL for {PropertyName}");
            return;
        }
        
        Debug.Log($"[PropertyToggleItem] SetupLegend for {PropertyName}, Active: {isActive}");
        plusButton.gameObject.SetActive(isActive);
        plusButton.onClick.RemoveAllListeners();

        // Pastikan raycast target image tombol aktif
        var img = plusButton.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        
        // Jika ada callback eksternal, gunakan itu saja (kontrol penuh)
        if (isActive && onPlusClicked != null)
        {
            Debug.Log($"[PropertyToggleItem] Assigning external callback for {PropertyName}");
            plusButton.onClick.AddListener(() => {
                Debug.Log($"[PropertyToggleItem] Executing external callback for {PropertyName}");
                onPlusClicked.Invoke();
            });
        }
        else
        {
            // Default behavior: toggle expand
            plusButton.onClick.AddListener(() => {
                Debug.Log($"[PropertyToggleItem] Plus clicked for {PropertyName} (Default)");
                if (legendController == null) legendController = FindObjectOfType<LegendController>();
                
                if (legendController != null) 
                {
                    legendController.ToggleExpand();
                }
                else
                {
                     Debug.LogWarning($"[PropertyToggleItem] LegendController not found!");
                }
            });
        }
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
