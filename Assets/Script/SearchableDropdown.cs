using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;

public class SearchableDropdown : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The main button that opens the dropdown")]
    public Button mainButton;
    [Tooltip("Text on the main button to show selected item")]
    public TMP_Text mainButtonText;
    
    [Tooltip("The panel containing the search input and list")]
    public GameObject dropdownPanel;
    [Tooltip("The input field for searching")]
    public TMP_InputField searchInput;
    
    [Tooltip("The parent object where items will be instantiated")]
    public Transform contentContainer;
    [Tooltip("The prefab for a single item (must have SearchableDropdownItem script)")]
    public GameObject itemPrefab;

    [Header("Settings")]
    public string defaultPlaceholder = "Select Option";

    [Header("Events")]
    public UnityEvent<string> onValueChanged; // Triggered when selection changes

    // Internal Data
    private List<string> _allOptions = new List<string>();
    private bool _isOpen = false;

    void Start()
    {
        // Setup initial UI state
        if (dropdownPanel) dropdownPanel.SetActive(false);
        if (mainButton) mainButton.onClick.AddListener(ToggleDropdown);
        if (searchInput) searchInput.onValueChanged.AddListener(OnSearchInput);
        
        // Only set placeholder if text is empty (allows external scripts to pre-set val)
        if (mainButtonText && string.IsNullOrEmpty(mainButtonText.text))
            UpdateMainText(defaultPlaceholder);
    }

    public void SetOptions(List<string> options)
    {
        _allOptions = new List<string>(options);
        RefreshList(_allOptions);
    }

    public void SelectItem(string item)
    {
        UpdateMainText(item);
        if (_isOpen) ToggleDropdown(); // Close if open
        onValueChanged?.Invoke(item);
    }

    public void ToggleDropdown()
    {
        if (dropdownPanel == null) return;
        
        bool intentOpen = !dropdownPanel.activeSelf;
        dropdownPanel.SetActive(intentOpen);
        _isOpen = intentOpen;

        if (_isOpen)
        {
            // Reset search when opening
            if (searchInput) searchInput.text = "";
            RefreshList(_allOptions); // Show all
            
            // Focus search field
            if (searchInput) searchInput.ActivateInputField();
        }
    }

    private void OnSearchInput(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            RefreshList(_allOptions);
            return;
        }

        string lowerQuery = query.ToLower();
        List<string> filtered = _allOptions.FindAll(x => x.ToLower().Contains(lowerQuery));
        RefreshList(filtered);
    }

    private void RefreshList(List<string> displayOptions)
    {
        if (!contentContainer || !itemPrefab) return;

        // Clear existing
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        // Populate new
        foreach (string opt in displayOptions)
        {
            GameObject obj = Instantiate(itemPrefab, contentContainer);
            SearchableDropdownItem itemScript = obj.GetComponent<SearchableDropdownItem>();
            
            if (itemScript)
            {
                itemScript.Setup(opt, (selectedVal) => {
                    SelectItem(selectedVal);
                });
            }
            else
            {
                // Fallback if script is missing, try standard setup
                TMP_Text txt = obj.GetComponentInChildren<TMP_Text>();
                if (txt) txt.text = opt;
                Button btn = obj.GetComponent<Button>();
                if (btn) btn.onClick.AddListener(() => SelectItem(opt));
            }
        }
    }

    private void UpdateMainText(string text)
    {
        if (mainButtonText) mainButtonText.text = text;
    }
}
