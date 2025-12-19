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

    // Variabel internal
    string _name;
    Action<string, bool> _onChange;

    // Getter nama property
    public string PropertyName => _name;

    // Setup toggle dengan nama, nilai awal, dan callback saat berubah
    public void Setup(string name, bool value, Action<string, bool> onChange)
    {
        _name = name;
        _onChange = onChange;

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
