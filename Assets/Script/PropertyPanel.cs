using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using TMPro;

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
    [HideInInspector] public string editLayerName;
    [Header("UI References")]
    public GameObject panel;         // Root panel
    public Transform content;        // Container untuk toggle items
    public GameObject togglePrefab;  // Prefab PropertyToggleItem
    public GameObject drawTogglePrefab;  // Prefab PropertyDrawToggleItem
    public ScrollRect scrollRect;    // ScrollRect untuk scroll handling
    public GameObject sharedEditPopup;
    public TextMeshProUGUI layerInfoText;

    // Events untuk komunikasi dengan ProjectManager
    public UnityEvent<string, bool> onPropertyChanged;      // (name, value)
    public UnityEvent<string, string> onPropertyRenamed;    // (oldName, newName)
    public UnityEvent<string> onPropertyDeleted;            // (name)
    string lastName = "";

    Dictionary<string, PropertyInfo> props = new Dictionary<string, PropertyInfo>();  // Data property
    List<PropertyToggleItem> items = new List<PropertyToggleItem>();  // Referensi item UI
    RectTransform contentRect;

    [System.Serializable]
    public class PropertyInfo
    {
        public bool value;
        public bool isDrawing;

        public PropertyInfo(bool val, bool drawing = false)
        {
            value = val;
            isDrawing = drawing;
        }
    }

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
        props.Clear();
        foreach(var kv in properties)
            props[kv.Key] = new PropertyInfo(kv.Value, false);
        RefreshList();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    public void ShowPropertiesWithType(Dictionary<string, PropertyInfo> properties)
    {
        props = new Dictionary<string, PropertyInfo>(properties);
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
        if (content == null) return;

        foreach (var kv in props)
        {
            GameObject prefab = kv.Value.isDrawing ? drawTogglePrefab : togglePrefab;
            if (prefab == null) prefab = togglePrefab;

            var obj = Instantiate(prefab, content);
            var item = obj.GetComponent<PropertyToggleItem>();
            if (item != null)
            {
                GameObject popup = kv.Value.isDrawing ? sharedEditPopup : null;
                item.Setup(kv.Key, kv.Value.value, OnToggle, OnRename, OnDelete, popup, this);
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
        if (props.ContainsKey(name)) props[name].value = value;
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
    public Dictionary<string, bool> GetCurrentProperties()
    {
        var result = new Dictionary<string, bool>();
        foreach (var kv in props)
            result[kv.Key]  = kv.Value.value;
        return result;
    }

    // Tambah property baru
    public void AddProperty(string name, bool value, bool isDrawing)
    {
        if (props.ContainsKey(name)) return;
        props[name] = new PropertyInfo(value, isDrawing);
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
        props[name].value = value;

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

    public void SetEditMode(string layerName, bool isEdit)
    {
        editLayerName = isEdit ? layerName : null;

        if (layerInfoText != null)
        {
            if (isEdit)
            {
                if (!layerInfoText.text.StartsWith("Editing : ")) lastName = layerInfoText.text;
                
                layerInfoText.text = "Editing : " + layerName;
            }
            else
            {
                if (!string.IsNullOrEmpty(lastName)) layerInfoText.text = lastName;
            }
        }

        foreach (var item in items)
        {
            if (item == null) continue;
            bool shouldDisable = isEdit && item.PropertyName != layerName;
            item.SetInteractable(!shouldDisable);
        }
    }

    // Simpan layer yang sedang diedit
    public void SaveEditedLayer()
    {
        var drawTool = FindObjectOfType<DrawTool>();
        
        // Ambil layer dari editLayerName atau dari DrawTool
        string layer = !string.IsNullOrEmpty(editLayerName) 
            ? editLayerName 
            : drawTool?.currentDrawingLayer ?? "";
        
        Debug.Log($"[SaveEditedLayer] Saving layer: '{layer}'");
        
        if (string.IsNullOrEmpty(layer)) return;
        FindObjectOfType<ProjectManager>()?.SaveLayer(layer);
    }

    // Tutup popup dan discard perubahan
    // PENTING: Di Unity, assign HANYA method ini ke Close button
    // JANGAN assign DeactivateAllModes terpisah karena akan clear layer duluan
    public void CloseEditPopup()
    {
        var drawTool = FindObjectOfType<DrawTool>();
        
        // PENTING: Ambil layer SEBELUM deactivate karena DeactivateAllModes akan reset currentDrawingLayer
        string layer = !string.IsNullOrEmpty(editLayerName) 
            ? editLayerName 
            : drawTool?.currentDrawingLayer ?? "";
        
        Debug.Log($"[CloseEditPopup] Layer to discard: '{layer}'");
        
        if (sharedEditPopup != null) sharedEditPopup.SetActive(false);
        SetEditMode(null, false);
        
        // Deactivate setelah ambil layer
        drawTool?.DeactivateAllModes();
        
        // Discard changes untuk layer tersebut
        FindObjectOfType<ProjectManager>()?.DiscardChanges(layer);
    }
}
