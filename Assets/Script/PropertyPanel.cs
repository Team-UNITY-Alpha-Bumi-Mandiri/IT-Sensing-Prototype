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

    [Header("Atribut Popup")]
    public GameObject sharedAtributPopup;       // Popup shared untuk atribut
    public Transform atributContent;            // Container untuk tabel
    public GameObject atributHeaderPrefab;      // Prefab untuk header cell
    public GameObject atributRowPrefab;         // Prefab untuk satu row
    public GameObject atributCellPrefab;        // Prefab untuk cell (dengan InputField)
    public Button addColumnButton;              // Tombol tambah kolom
    public Button deleteColumnButton;           // Tombol hapus kolom selected
    public Button deleteRowButton;              // Tombol hapus row selected
    public TMP_InputField columnNameInput;      // Input nama kolom baru
    public Color selectedColor = new Color(0.8f, 0.9f, 1f);  // Warna highlight
    string currentAtributLayer = "";            // Layer yang sedang ditampilkan
    string selectedColumnName = "";             // Kolom yang sedang dipilih
    string selectedRowId = "";                  // Drawing ID row yang dipilih
    public Button saveButton;                   // Tombol save
    Dictionary<string, List<Image>> columnCells = new();  // Cells per kolom untuk highlight
    Dictionary<string, List<Image>> rowCells = new();     // Cells per row untuk highlight

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

        if (addColumnButton != null)
            addColumnButton.onClick.AddListener(OnAddColumnClicked);
        
        if (deleteColumnButton != null)
            deleteColumnButton.onClick.AddListener(OnDeleteColumnClicked);
        
        if (deleteRowButton != null)
            deleteRowButton.onClick.AddListener(OnDeleteRowClicked);

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);
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
                item.Setup(kv.Key, kv.Value.value, OnToggle, OnRename, OnDelete, this);
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
        var drawTool = Object.FindFirstObjectByType<DrawTool>();
        
        // Ambil layer dari editLayerName atau dari DrawTool
        string layer = !string.IsNullOrEmpty(editLayerName) 
            ? editLayerName 
            : drawTool?.currentDrawingLayer ?? "";
        
        Debug.Log($"[SaveEditedLayer] Saving layer: '{layer}'");
        
        if (string.IsNullOrEmpty(layer)) return;
        Object.FindFirstObjectByType<ProjectManager>()?.SaveLayer(layer);
    }

    // Tutup popup dan discard perubahan
    // PENTING: Di Unity, assign HANYA method ini ke Close button
    // JANGAN assign DeactivateAllModes terpisah karena akan clear layer duluan
    public void CloseEditPopup()
    {
        var drawTool = Object.FindFirstObjectByType<DrawTool>();
        
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
        Object.FindFirstObjectByType<ProjectManager>()?.DiscardChanges(layer);
    }

    // Tampilkan popup atribut dengan tabel dinamis
    public void ShowAtribut(string layerName)
    {
        if (atributContent == null) return;
        currentAtributLayer = layerName;
        
        // Clear existing rows
        foreach (Transform child in atributContent)
            Destroy(child.gameObject);
        
        var pm = Object.FindFirstObjectByType<ProjectManager>();
        var proj = pm?.GetCurrentProject();
        if (proj == null) return;
        
        // Reset selection state
        columnCells.Clear();
        rowCells.Clear();
        selectedColumnName = "";
        selectedRowId = "";
        
        // Get kolom: ID (default) + custom columns
        var customColumns = proj.GetColumns(layerName);
        
        // === HEADER ROW ===
        if (atributRowPrefab != null)
        {
            var headerRow = Instantiate(atributRowPrefab, atributContent);
            
            // Header ID (tidak bisa di-select/delete)
            CreateHeaderCell(headerRow.transform, "ID", true);
            
            // Header custom columns (bisa di-select)
            foreach (var col in customColumns)
                CreateHeaderCell(headerRow.transform, col, false);
        }
        
        // === DATA ROWS ===
        var layerDrawings = proj.drawings.FindAll(d => d.layerName == layerName);
        
        foreach (var drawing in layerDrawings)
        {
            if (atributRowPrefab == null) continue;
            
            var row = Instantiate(atributRowPrefab, atributContent);
            
            // Cell ID (read-only)
            CreateDataCell(row.transform, drawing.sequentialId.ToString(), null, null, true);
            
            // Cell custom columns (editable)
            foreach (var col in customColumns)
            {
                string currentValue = drawing.GetAttribute(col);
                CreateDataCell(row.transform, currentValue, drawing.id, col, false);
            }
        }
        
        Debug.Log($"[ShowAtribut] Layer '{layerName}' has {layerDrawings.Count} rows, {customColumns.Count} custom columns");
    }

    // Buat header cell (clickable untuk select kolom)
    void CreateHeaderCell(Transform parent, string columnName, bool isIdColumn = false)
    {
        if (atributHeaderPrefab == null) return;
        var cell = Instantiate(atributHeaderPrefab, parent);
        var tmp = cell.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = columnName;
        
        var img = cell.GetComponent<Image>();
        if (img != null && !isIdColumn)
        {
            // Tambah click handler - highlight header cell saja
            var btn = cell.GetComponent<Button>() ?? cell.AddComponent<Button>();
            string col = columnName;
            Image cellImg = img;
            btn.onClick.AddListener(() => SelectCell("", col, cellImg));
        }
    }

    // Buat data cell (editable + clickable untuk select kolom/row)
    void CreateDataCell(Transform parent, string value, string drawingId, string columnName, bool readOnly)
    {
        if (atributCellPrefab == null) return;
        var cell = Instantiate(atributCellPrefab, parent);
        var input = cell.GetComponentInChildren<TMP_InputField>();
        
        var img = cell.GetComponent<Image>();
        
        // Track cell untuk row highlight
        if (img != null && !string.IsNullOrEmpty(drawingId))
        {
            if (!rowCells.ContainsKey(drawingId))
                rowCells[drawingId] = new List<Image>();
            rowCells[drawingId].Add(img);
        }
        
        // Tambah click handler (select cell, track row + kolom)
        if (img != null && !string.IsNullOrEmpty(drawingId))
        {
            var btn = cell.GetComponent<Button>() ?? cell.AddComponent<Button>();
            string rowId = drawingId;
            string col = columnName;
            Image cellImg = img;
            
            btn.onClick.AddListener(() => SelectCell(rowId, col, cellImg));
        }
        
        if (input != null)
        {
            input.text = value;
            input.readOnly = readOnly;
            input.ForceLabelUpdate();
            
            if (!readOnly && !string.IsNullOrEmpty(drawingId) && !string.IsNullOrEmpty(columnName))
            {
                string id = drawingId;
                string col = columnName;
                input.onEndEdit.AddListener(newValue => OnCellValueChanged(id, col, newValue));
            }
        }
        else
        {
            var tmp = cell.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = value;
        }
    }

    // Callback saat cell value berubah
    void OnCellValueChanged(string drawingId, string columnName, string newValue)
    {
        var pm = Object.FindFirstObjectByType<ProjectManager>();
        var proj = pm?.GetCurrentProject();
        if (proj == null) return;
        
        var drawing = proj.drawings.Find(d => d.id == drawingId);
        if (drawing != null)
        {
            drawing.SetAttribute(columnName, newValue);
            Debug.Log($"[Atribut] Set {columnName}='{newValue}' for drawing {drawingId}");
        }
    }

    // Public method untuk tambah kolom
    public void AddAtributColumn(string columnName)
    {
        if (string.IsNullOrEmpty(currentAtributLayer) || string.IsNullOrEmpty(columnName)) return;
        
        var pm = Object.FindFirstObjectByType<ProjectManager>();
        var proj = pm?.GetCurrentProject();
        proj?.AddColumn(currentAtributLayer, columnName);
        
        // Refresh tabel
        ShowAtribut(currentAtributLayer);
    }

    // Public method untuk hapus kolom
    public void RemoveAtributColumn(string columnName)
    {
        if (string.IsNullOrEmpty(currentAtributLayer) || string.IsNullOrEmpty(columnName)) return;
        
        var pm = Object.FindFirstObjectByType<ProjectManager>();
        var proj = pm?.GetCurrentProject();
        proj?.RemoveColumn(currentAtributLayer, columnName);
        
        // Refresh tabel
        ShowAtribut(currentAtributLayer);
    }

    void OnAddColumnClicked()
    {
        if (columnNameInput == null) return;
        string colName = columnNameInput.text.Trim();
        if (string.IsNullOrEmpty(colName)) return;
        
        AddAtributColumn(colName);
        columnNameInput.text = "";
    }

    // Select cell - highlight hanya cell yang diklik, track row & kolom
    Image lastSelectedCell;
    Color lastOriginalColor;
    
    void SelectCell(string rowId, string colName, Image cellImg)
    {
        selectedRowId = rowId;
        selectedColumnName = colName;
        
        // Reset highlight sebelumnya ke warna asli
        if (lastSelectedCell != null)
            lastSelectedCell.color = lastOriginalColor;
        
        // Simpan warna asli cell yang diklik
        if (cellImg != null)
        {
            lastOriginalColor = cellImg.color;
            cellImg.color = selectedColor;
        }
        
        lastSelectedCell = cellImg;
        Debug.Log($"[Atribut] Selected cell - row: {rowId}, column: {colName}");
    }

    // Hapus kolom yang sedang dipilih
    void OnDeleteColumnClicked()
    {
        if (string.IsNullOrEmpty(selectedColumnName))
        {
            Debug.Log("[Atribut] No column selected");
            return;
        }
        
        RemoveAtributColumn(selectedColumnName);
        selectedColumnName = "";
    }

    // Hapus row yang sedang dipilih
    void OnDeleteRowClicked()
    {
        if (string.IsNullOrEmpty(selectedRowId))
        {
            Debug.Log("[Atribut] No row selected");
            return;
        }
        
        DeleteDrawing(selectedRowId);
        selectedRowId = "";
    }

    // Hapus drawing dan polygon terkait
    void DeleteDrawing(string drawingId)
    {
        var pm = Object.FindFirstObjectByType<ProjectManager>();
        var proj = pm?.GetCurrentProject();
        if (proj == null) return;
        
        // Cari drawing
        var drawing = proj.drawings.Find(d => d.id == drawingId);
        if (drawing == null) return;
        
        // Hapus polygon visual dari DrawTool
        var drawTool = Object.FindFirstObjectByType<DrawTool>();
        if (drawTool != null)
        {
            drawTool.DeletePolygonById(drawingId);
        }
        
        // Hapus dari project data
        proj.drawings.Remove(drawing);        
        // Refresh tabel
        ShowAtribut(currentAtributLayer);
        
        Debug.Log($"[Atribut] Deleted drawing {drawingId}");
    }

    // Update sequential IDs setelah delete
    void UpdateSequentialIds(ProjectManager.ProjectData proj, string layerName)
    {
        var layerDrawings = proj.drawings.FindAll(d => d.layerName == layerName);
        layerDrawings.Sort((a, b) => a.sequentialId.CompareTo(b.sequentialId));
        
        for (int i = 0; i < layerDrawings.Count; i++)
        {
            layerDrawings[i].sequentialId = i + 1;
        }
    }

    void OnSaveClicked()
    {
        var pm = Object.FindFirstObjectByType<ProjectManager>();
        if (pm != null)
        {
            pm.Save();
            Debug.Log("[Atribut] Project saved!");
        }
    }
}
