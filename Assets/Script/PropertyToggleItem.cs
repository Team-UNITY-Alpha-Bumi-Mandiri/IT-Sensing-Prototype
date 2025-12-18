using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// Komponen UI untuk satu item toggle property.
public class PropertyToggleItem : MonoBehaviour
{
    [Header("UI References")]
    public Toggle toggle;         // Komponen toggle
    public TMP_Text labelText;    // Label nama property

    private string _propertyName;
    private Action<string, bool> _onValueChanged;

    // Setup item dengan nama dan callback
    public void Setup(string propertyName, bool initialValue, Action<string, bool> onChanged)
    {
        _propertyName = propertyName;
        _onValueChanged = onChanged;

        if (labelText) labelText.text = propertyName;
        if (toggle)
        {
            toggle.isOn = initialValue;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(OnToggleChanged);
        }
    }

    // Callback saat toggle berubah
    private void OnToggleChanged(bool value)
    {
        _onValueChanged?.Invoke(_propertyName, value);
    }

    // Getter nama property
    public string PropertyName => _propertyName;

    // Setter nilai toggle tanpa trigger event
    public void SetValueWithoutNotify(bool value)
    {
        if (toggle) toggle.SetIsOnWithoutNotify(value);
    }
}
