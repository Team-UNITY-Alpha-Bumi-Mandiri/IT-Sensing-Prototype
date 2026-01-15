using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

// ============================================================
// PropertyPanel - Panel untuk menampilkan daftar property toggle
// ============================================================
// Fitur:
// - Menampilkan list toggle dengan nama property
// - Mendukung rename dan delete property
// - Callback untuk setiap perubahan
// ============================================================
public class PropertyPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;         // Root panel
    public Transform content;        // Container untuk toggle items
    public GameObject togglePrefab;  // Prefab PropertyToggleItem
    public ScrollRect scrollRect;    // ScrollRect untuk scroll handling

    // Events untuk komunikasi dengan ProjectManager
    public UnityEvent<string, bool> onPropertyChanged;      // (name, value)
    public UnityEvent<string, string> onPropertyRenamed;    // (oldName, newName)
    public UnityEvent<string> onPropertyDeleted;            // (name)

    Dictionary<string, bool> props = new Dictionary<string, bool>();  // Data property
    List<PropertyToggleItem> items = new List<PropertyToggleItem>();  // Referensi item UI
    RectTransform contentRect;

    void Start()
    {
        contentRect = content as RectTransform;
        
        // Auto-find ScrollRect
        if (scrollRect == null && panel != null)
            scrollRect = panel.GetComponentInChildren<ScrollRect>();
    }

    // Tampilkan property dari dictionary
    public void ShowProperties(Dictionary<string, bool> properties)
    {
        props = new Dictionary<string, bool>(properties);
        RefreshList();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    // Kosongkan panel
    public void ClearPanel()
    {
        ClearItems();
        props.Clear();
    }

    // Refresh list item berdasarkan props
    void RefreshList()
    {
        ClearItems();
        if (content == null || togglePrefab == null) return;

        foreach (var kv in props)
        {
            var obj = Instantiate(togglePrefab, content);
            var item = obj.GetComponent<PropertyToggleItem>();
            if (item != null)
            {
                item.Setup(kv.Key, kv.Value, OnToggle, OnRename, OnDelete);
                items.Add(item);
            }
        }
        StartCoroutine(RebuildLayout());
    }

    // Hapus semua item UI
    void ClearItems()
    {
        foreach (var item in items)
            if (item != null) Destroy(item.gameObject);
        items.Clear();
    }

    // Callback saat toggle berubah
    void OnToggle(string name, bool value)
    {
        if (props.ContainsKey(name)) props[name] = value;
        onPropertyChanged?.Invoke(name, value);
    }

    // Callback saat rename
    void OnRename(string oldName, string newName)
    {
        if (!props.ContainsKey(oldName)) return;
        
        // Update dictionary
        props[newName] = props[oldName];
        props.Remove(oldName);
        
        onPropertyRenamed?.Invoke(oldName, newName);
        RefreshList();
    }

    // Callback saat delete
    void OnDelete(string name)
    {
        if (!props.ContainsKey(name)) return;
        
        props.Remove(name);
        onPropertyDeleted?.Invoke(name);
        RefreshList();
    }

    // Dapatkan properties saat ini
    public Dictionary<string, bool> GetCurrentProperties() => new Dictionary<string, bool>(props);

    // Tambah property baru
    public void AddProperty(string name, bool value = false)
    {
        if (props.ContainsKey(name)) return;
        props[name] = value;
        RefreshList();
    }

    // Hapus property
    public void RemoveProperty(string name)
    {
        if (!props.ContainsKey(name)) return;
        props.Remove(name);
        RefreshList();
    }

    // Set nilai property tertentu
    // notify: true untuk trigger event, false untuk silent update
    public void SetPropertyValue(string name, bool value, bool notify = true)
    {
        if (!props.ContainsKey(name)) return;
        props[name] = value;

        // Update UI item
        var item = items.Find(x => x.PropertyName == name);
        if (item != null)
        {
            if (notify) item.Setup(name, value, OnToggle);
            else item.SetValueWithoutNotify(value);
        }

        if (notify) onPropertyChanged?.Invoke(name, value);
    }

    // Rebuild layout setelah item berubah
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
