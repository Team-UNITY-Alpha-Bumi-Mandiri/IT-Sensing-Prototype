using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

// Panel UI untuk menampilkan daftar property toggle per project.
public class PropertyPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;              // Panel utama (untuk show/hide)
    public Transform contentContainer;    // Wadah tempat item toggle di-spawn
    public GameObject toggleItemPrefab;   // Prefab PropertyToggleItem
    public ScrollRect scrollRect;         // ScrollRect untuk scroll list

    [Header("Events")]
    public UnityEvent<string, bool> onPropertyChanged; // Event saat property toggle berubah (name, value)

    // Data internal
    private Dictionary<string, bool> _currentProperties = new();
    private List<PropertyToggleItem> _spawnedItems = new();
    private RectTransform _contentRect;

    void Start()
    {
        // Cache content RectTransform
        if (contentContainer) _contentRect = contentContainer as RectTransform;
        
        // Auto-detect ScrollRect jika tidak diassign
        if (!scrollRect && panel) scrollRect = panel.GetComponentInChildren<ScrollRect>();
    }

    // Tampilkan property list tertentu
    public void ShowProperties(Dictionary<string, bool> properties)
    {
        _currentProperties = new Dictionary<string, bool>(properties);
        
        // Refresh list item
        RefreshList();
        
        // Reset scroll ke atas
        if (scrollRect) scrollRect.verticalNormalizedPosition = 1f;
    }

    // Kosongkan panel (clear semua item)
    public void ClearPanel()
    {
        ClearItems();
        _currentProperties.Clear();
    }

    // Refresh list toggle berdasarkan _currentProperties
    private void RefreshList()
    {
        ClearItems();

        if (!contentContainer || !toggleItemPrefab) return;

        foreach (var kvp in _currentProperties)
        {
            var obj = Instantiate(toggleItemPrefab, contentContainer);
            var item = obj.GetComponent<PropertyToggleItem>();
            
            if (item)
            {
                item.Setup(kvp.Key, kvp.Value, OnToggleValueChanged);
                _spawnedItems.Add(item);
            }
        }

        // Rebuild layout
        StartCoroutine(RebuildLayoutDelayed());
    }

    // Hapus semua item yang di-spawn
    private void ClearItems()
    {
        foreach (var item in _spawnedItems)
        {
            if (item) Destroy(item.gameObject);
        }
        _spawnedItems.Clear();
    }

    // Callback saat toggle berubah
    private void OnToggleValueChanged(string propertyName, bool value)
    {
        // Update dictionary internal
        if (_currentProperties.ContainsKey(propertyName))
            _currentProperties[propertyName] = value;

        // Trigger event ke luar
        onPropertyChanged?.Invoke(propertyName, value);
    }

    // Ambil semua property values saat ini
    public Dictionary<string, bool> GetCurrentProperties()
    {
        return new Dictionary<string, bool>(_currentProperties);
    }

    // Tambah property baru
    public void AddProperty(string name, bool defaultValue = false)
    {
        if (!_currentProperties.ContainsKey(name))
        {
            _currentProperties[name] = defaultValue;
            RefreshList();
        }
    }

    // Hapus property
    public void RemoveProperty(string name)
    {
        if (_currentProperties.ContainsKey(name))
        {
            _currentProperties.Remove(name);
            RefreshList();
        }
    }

    // Set nilai property tertentu
    public void SetPropertyValue(string name, bool value, bool notify = true)
    {
        if (_currentProperties.ContainsKey(name))
        {
            _currentProperties[name] = value;
            
            // Update UI
            var item = _spawnedItems.Find(x => x.PropertyName == name);
            if (item)
            {
                if (notify)
                    item.Setup(name, value, OnToggleValueChanged);
                else
                    item.SetValueWithoutNotify(value);
            }
            
            if (notify) onPropertyChanged?.Invoke(name, value);
        }
    }

    // Coroutine untuk rebuild layout
    private IEnumerator RebuildLayoutDelayed()
    {
        yield return null;
        
        if (_contentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
            Canvas.ForceUpdateCanvases();
            
            if (scrollRect)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.GetComponent<RectTransform>());
            }
        }
    }
}
