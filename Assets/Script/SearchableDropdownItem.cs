using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SearchableDropdownItem : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text itemText;
    public Button button;

    private string _itemValue;
    private Action<string> _onSelect;

    public void Setup(string value, Action<string> onSelectAction)
    {
        _itemValue = value;
        _onSelect = onSelectAction;

        if (itemText != null) itemText.text = value;
        
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnItemClicked);
        }
    }

    private void OnItemClicked()
    {
        _onSelect?.Invoke(_itemValue);
    }
}
