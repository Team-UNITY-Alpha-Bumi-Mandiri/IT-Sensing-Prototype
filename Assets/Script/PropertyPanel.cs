using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

// =========================================
// Panel untuk menampilkan daftar property toggle
// Contoh: Panel berisi checkbox "Night Mode", "Show Grid", dll
// =========================================
public class PropertyPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;           // Panel utama
    public Transform content;          // Wadah item toggle
    public GameObject togglePrefab;    // Prefab PropertyToggleItem
    public ScrollRect scrollRect;      // Untuk scroll

    // Event saat toggle berubah (nama property, nilai baru)
    public UnityEvent<string, bool> onPropertyChanged;

    // Variabel internal
    Dictionary<string, bool> props = new Dictionary<string, bool>();
    List<PropertyToggleItem> items = new List<PropertyToggleItem>();
    RectTransform contentRect;

    void Start()
    {
        // Cache RectTransform
        if (content != null)
        {
            contentRect = content as RectTransform;
        }

        // Auto-detect ScrollRect
        if (scrollRect == null && panel != null)
        {
            scrollRect = panel.GetComponentInChildren<ScrollRect>();
        }
    }

    // Tampilkan property dari dictionary
    public void ShowProperties(Dictionary<string, bool> properties)
    {
        props = new Dictionary<string, bool>(properties);
        RefreshList();

        // Reset scroll ke atas
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    // Kosongkan panel
    public void ClearPanel()
    {
        ClearItems();
        props.Clear();
    }

    // Refresh tampilan list
    void RefreshList()
    {
        ClearItems();

        if (content == null || togglePrefab == null) return;

        // Buat toggle untuk setiap property
        foreach (var kv in props)
        {
            GameObject obj = Instantiate(togglePrefab, content);
            PropertyToggleItem item = obj.GetComponent<PropertyToggleItem>();

            if (item != null)
            {
                item.Setup(kv.Key, kv.Value, OnToggle);
                items.Add(item);
            }
        }

        // Rebuild layout
        StartCoroutine(RebuildLayout());
    }

    // Hapus semua item yang di-spawn
    void ClearItems()
    {
        foreach (PropertyToggleItem item in items)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        items.Clear();
    }

    // Callback saat toggle berubah
    void OnToggle(string name, bool value)
    {
        // Update dictionary internal
        if (props.ContainsKey(name))
        {
            props[name] = value;
        }

        // Trigger event ke luar
        onPropertyChanged?.Invoke(name, value);
    }

    // Getter semua property saat ini
    public Dictionary<string, bool> GetCurrentProperties()
    {
        return new Dictionary<string, bool>(props);
    }

    // Tambah property baru
    public void AddProperty(string name, bool value = false)
    {
        if (!props.ContainsKey(name))
        {
            props[name] = value;
            RefreshList();
        }
    }

    // Hapus property
    public void RemoveProperty(string name)
    {
        if (props.ContainsKey(name))
        {
            props.Remove(name);
            RefreshList();
        }
    }

    // Set nilai property tertentu
    public void SetPropertyValue(string name, bool value, bool notify = true)
    {
        if (!props.ContainsKey(name)) return;

        props[name] = value;

        // Update UI
        PropertyToggleItem item = items.Find(x => x.PropertyName == name);
        if (item != null)
        {
            if (notify)
            {
                item.Setup(name, value, OnToggle);
            }
            else
            {
                item.SetValueWithoutNotify(value);
            }
        }

        // Trigger event
        if (notify)
        {
            onPropertyChanged?.Invoke(name, value);
        }
    }

    // Rebuild layout
    IEnumerator RebuildLayout()
    {
        yield return null;

        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Canvas.ForceUpdateCanvases();
        }

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.GetComponent<RectTransform>());
        }
    }
}
