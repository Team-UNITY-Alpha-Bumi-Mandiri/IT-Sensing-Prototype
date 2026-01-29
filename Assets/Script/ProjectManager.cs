using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Events;

// ============================================================
// ProjectManager - Manager utama untuk CRUD project
// ============================================================
// Fungsi:
// - Create: Buat project baru dengan polygon boundary
// - Read/Select: Pilih project dan load datanya
// - Update: Rename project
// - Delete: Hapus project beserta file TIFF-nya
// - Property Management: Tambah, hapus, update properties (layers)
// - Save/Load: Persistensi data ke JSON
// ============================================================
public class ProjectManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;  // Kontrol peta
    public SearchableDropdown projectDropdown;      // Dropdown pilih project
    public DrawTool drawTool;                       // Tool gambar polygon
    public PropertyPanel propertyPanel;             // Panel property
    public TiffLayerManager tiffLayerManager;       // Manager layer TIFF

    [Header("Form UI")]
    public TMP_InputField newProjectNameInput;      // Input nama project baru
    public Button createProjectButton;              // Tombol buat project
    public Button deleteProjectButton;              // Tombol hapus project
    public Button focusProjectButton;               // Tombol fokus ke project aktif [NEW]

    [Header("Rename UI")]
    public TMP_InputField renameProjectInput;       // Input rename project
    public Button renameProjectButton;              // Tombol rename

    [Header("Other Tools")]
    public AutoplayTool autoplayTool;               // Tool autoplay untuk refresh dropdown
    public SplitViewTool splitViewTool;  //Split View tool - refresh dropdown

    // Event saat project dengan TIFF diload (dipanggil oleh TiffLayerManager)
    public UnityEvent<string> onTiffProjectLoaded;

    // Data internal
    List<ProjectData> projects = new();     // Semua project
    ProjectData current = null;             // Project aktif saat ini
    string SavePath => Path.Combine(Application.persistentDataPath, "projects.json");

    // ============================================================
    // DATA CLASSES
    // ============================================================

    // Entry property untuk serialisasi JSON
    [System.Serializable]
    public class PropertyEntry
    {
        public string key;
        public bool value;
        public bool isDrawing;
        public PropertyEntry(string k, bool v, bool drawing = false) { key = k; value = v; isDrawing = drawing; }
    }

    // Drawing object yang diserialisasi
    [System.Serializable]
    public class SerializedDrawObject
    {
        public string id;                   // ID unik
        public DrawTool.DrawMode type;      // Tipe gambar
        public string layerName;            // Layer
        public List<Vector2> coordinates;   // Koordinat
        public bool useTexture;             // Pakai texture atau tidak
    }

    // Data project
    [System.Serializable]
    public class ProjectData
    {
        public string id, name, tiffPath;           // ID, nama, path TIFF (opsional)
        public double lat, lon;                     // Koordinat center
        public int zoom;                            // Zoom level
        public List<Vector2> polygonCoords;         // Polygon boundary (ROI)
        public List<PropertyEntry> properties = new();       // Properties (layers)
        public List<SerializedDrawObject> drawings = new();  // Drawings dalam project

        // Convert properties ke Dictionary
        public Dictionary<string, PropertyPanel.PropertyInfo> GetProps()
        {
            var dict = new Dictionary<string, PropertyPanel.PropertyInfo>();
            if (properties != null)
            {
                foreach (var p in properties)
                {
                    dict[p.key] = new PropertyPanel.PropertyInfo(p.value, p.isDrawing);
                }
            }
            return dict;
        }

        // Set properties dari Dictionary
        public void SetProps(Dictionary<string, PropertyPanel.PropertyInfo> dict)
        {
            properties = new List<PropertyEntry>();
            foreach (var kv in dict) properties.Add(new PropertyEntry(kv.Key, kv.Value.value, kv.Value.isDrawing));
        }
    }

    // Wrapper untuk serialisasi JSON
    [System.Serializable]
    class Wrapper { public List<ProjectData> items; }

    // ============================================================
    // LIFECYCLE
    // ============================================================

    void Start()
    {
        // Load data tersimpan
        LoadProjects();
        SetupDropdown();

        // Setup button listeners
        createProjectButton?.onClick.AddListener(StartCreate);
        deleteProjectButton?.onClick.AddListener(Delete);
        renameProjectButton?.onClick.AddListener(Rename);
        focusProjectButton?.onClick.AddListener(FocusCurrentProject);

        // Setup PropertyPanel listener
        // Setup PropertyPanel listener
        if (propertyPanel != null)
        {
            propertyPanel.onPropertyChanged.AddListener(OnPropertyChanged);
            propertyPanel.onPropertyRenamed.AddListener(OnPropertyRenamed);
            propertyPanel.onPropertyDeleted.AddListener(OnPropertyDeleted);
        }

        // Setup DrawTool listeners
        if (drawTool != null)
        {
            drawTool.onDrawComplete.AddListener(OnDrawn);
            drawTool.onObjectDeleted.AddListener(OnObjectDeleted);
        }
    }

    // ============================================================
    // DROPDOWN
    // ============================================================

    // Setup dropdown dengan daftar project
    void SetupDropdown()
    {
        if (projectDropdown == null) return;

        var names = new List<string>();
        projects.ForEach(p => names.Add(p.name));

        projectDropdown.SetOptions(names);
        projectDropdown.onValueChanged.RemoveListener(OnSelect);
        projectDropdown.onValueChanged.AddListener(OnSelect);
    }

    // Callback saat project dipilih dari dropdown
    void OnSelect(string name)
    {
        var p = projects.Find(x => x.name == name);
        if (p != null) Select(p);
    }

    // ============================================================
    // CREATE - Buat project baru
    // ============================================================

    // Mulai proses create project (gambar polygon boundary)
    void StartCreate()
    {
        if (string.IsNullOrEmpty(newProjectNameInput?.text) || drawTool == null) return;

        drawTool.ClearAll();
        drawTool.forceTextureOnNext = true;  // Gunakan texture untuk ROI polygon
        drawTool.ActivateMode(DrawTool.DrawMode.Polygon);
    }

    // Callback selesai gambar
    void OnDrawn(DrawTool.DrawObject obj)
    {
        if (obj.layerName != null && (obj.layerName.StartsWith("Loaded") || obj.layerName.Contains("_ROI")))
        {
            Debug.Log($"[OnDrawn] Skipped system layer: {obj.layerName}");
            return;
        }
        // CASE A: Tambah/Update drawing ke project aktif (Layer Drawing)
        if (current != null && !string.IsNullOrEmpty(obj.layerName))
        {
            Debug.Log($"[OnDrawn] Saved drawing id:{obj.id} layer:{obj.layerName}");
            // Cari existing drawing dengan ID yang sama
            var existing = current.drawings.Find(d => d.id == obj.id);
            
            if (existing != null)
            {
                // Update existing
                existing.coordinates = new List<Vector2>(obj.coordinates);
                existing.useTexture = obj.useTexture;
            }
            else
            {
                // Add new
                current.drawings.Add(new SerializedDrawObject
                {
                    id = obj.id, type = obj.type, layerName = obj.layerName,
                    coordinates = new List<Vector2>(obj.coordinates), useTexture = obj.useTexture
                });
            }
            var props = current.GetProps();
            if (props.ContainsKey(obj.layerName))
            {
                bool currentVal = props[obj.layerName].value;
                props[obj.layerName] = new PropertyPanel.PropertyInfo(currentVal, true);
                current.SetProps(props);
                propertyPanel?.ShowPropertiesWithType(props);
            }
            return;
        }

        // CASE B: Buat project baru (ROI Drawing)
        if (string.IsNullOrEmpty(newProjectNameInput?.text) || obj.coordinates.Count == 0) return;

        var proj = new ProjectData
        {
            id = System.Guid.NewGuid().ToString(),
            name = newProjectNameInput.text,
            lat = obj.coordinates[0].x,
            lon = obj.coordinates[0].y,
            zoom = mapController?.zoom ?? 15,
            polygonCoords = new List<Vector2>(obj.coordinates)
        };

        // Jika drawing ada layer name, simpan juga
        if (!string.IsNullOrEmpty(drawTool.currentDrawingLayer))
        {
            proj.drawings.Add(new SerializedDrawObject
            {
                id = obj.id, type = obj.type, layerName = obj.layerName,
                coordinates = new List<Vector2>(obj.coordinates), useTexture = obj.useTexture
            });
        }

        projects.Add(proj);
        SetupDropdown();

        // Select project baru
        projectDropdown.onValueChanged.RemoveListener(OnSelect);
        projectDropdown.SelectItem(proj.name);
        projectDropdown.onValueChanged.AddListener(OnSelect);

        current = proj;
        if (renameProjectInput != null) renameProjectInput.text = proj.name;
        newProjectNameInput.text = "";
        propertyPanel?.ShowPropertiesWithType(proj.GetProps());
    }

    // Buat project otomatis (untuk hasil sharpening, dll)
    public ProjectData CreateProjectAuto(string name, double lat, double lon, int zoom, string tiffPath, List<Vector2> polyCoords = null)
    {
        var proj = new ProjectData
        {
            id = System.Guid.NewGuid().ToString(),
            name = name, lat = lat, lon = lon, zoom = zoom, tiffPath = tiffPath,
            polygonCoords = polyCoords != null ? new List<Vector2>(polyCoords) : new()
        };

        projects.Add(proj);
        Save();
        SetupDropdown();
        Select(proj);
        return proj;
    }

    // ============================================================
    // SELECT - Pilih project
    // ============================================================

    void Select(ProjectData proj)
    {
        current = proj;

        // Update rename input
        if (renameProjectInput != null) renameProjectInput.text = proj.name;

        // Navigasi ke lokasi project
        FocusCurrentProject();

        // Sembunyikan semua visual dulu
        drawTool?.ForceHideAllVisuals();

        // Load ROI Polygon
        if (proj.polygonCoords?.Count > 0)
        {
            string roiId = proj.id + "_ROI";
            if (drawTool.HasDrawing(roiId)) drawTool.ShowDrawing(roiId, true);
            else drawTool.LoadPolygon(proj.polygonCoords, true, "Loaded_" + roiId, roiId);
        }

        // Load drawings project
        if (proj.drawings != null && drawTool != null)
        {
            foreach (var d in proj.drawings)
            {
                if (drawTool.HasDrawing(d.id)) drawTool.ShowDrawing(d.id, true);
                else drawTool.CreateObj(d.type, d.coordinates, d.layerName, d.useTexture, d.id);
            }
            
            // Force show semua drawings yang baru diload
            foreach (var d in proj.drawings)
            {
                drawTool.ShowDrawing(d.id, true);
            }
        }

        // Set layer visibility
        var props = proj.GetProps();
        foreach (var kv in props) drawTool?.SetLayerVisibility(kv.Key, kv.Value.value);

        // Notify TiffLayerManager
        onTiffProjectLoaded?.Invoke(proj.tiffPath ?? "");

        // Update PropertyPanel
        propertyPanel?.ShowPropertiesWithType(proj.GetProps());

        // Update Other Tools dropdown
        autoplayTool?.UpdateDropdownOptions();
        splitViewTool?.UpdateDropdownOptions();        
    }

    // ============================================================
    // FOCUS - Center map to project
    // ============================================================

    void FocusCurrentProject()
    {
        if (current == null || mapController == null) return;
        mapController.GoToLocation(current.lat, current.lon, current.zoom);
    }

    // ============================================================
    // RENAME - Rename project
    // ============================================================

    void Rename()
    {
        if (current == null || string.IsNullOrEmpty(renameProjectInput?.text)) return;
        if (current.name == renameProjectInput.text) return;

        // Rename folder backend jika ada (termasuk .meta)
        string oldBackendPath = Path.Combine(Application.streamingAssetsPath, "Backend", "downloaded_bands", current.name);
        string newBackendPath = Path.Combine(Application.streamingAssetsPath, "Backend", "downloaded_bands", renameProjectInput.text);
        
        if (Directory.Exists(oldBackendPath) && !Directory.Exists(newBackendPath))
        {
            try
            {
                Directory.Move(oldBackendPath, newBackendPath);
                
                string oldMeta = oldBackendPath + ".meta";
                string newMeta = newBackendPath + ".meta";
                if (File.Exists(oldMeta) && !File.Exists(newMeta)) File.Move(oldMeta, newMeta);
            }
            catch (System.Exception e) { Debug.LogError($"[ProjectManager] Rename backend folder failed: {e.Message}"); }
        }

        current.name = renameProjectInput.text;
        Save();
        SetupDropdown();
        projectDropdown?.SelectItem(current.name);
    }

    // ============================================================
    // DELETE - Hapus project
    // ============================================================

    void Delete()
    {
        if (current == null) return;

        // Hapus file TIFF jika ada
        if (!string.IsNullOrEmpty(current.tiffPath) && File.Exists(current.tiffPath))
        {
            try
            {
                File.Delete(current.tiffPath);
                if (File.Exists(current.tiffPath + ".meta")) File.Delete(current.tiffPath + ".meta");
                tiffLayerManager?.UnloadFromCache(current.tiffPath);
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
            catch (System.Exception e) { Debug.LogWarning($"[ProjectManager] Delete TIFF failed: {e.Message}"); }
        }

        // Hapus folder backend jika ada (termasuk .meta)
        string backendPath = Path.Combine(Application.streamingAssetsPath, "Backend", "downloaded_bands", current.name);
        if (Directory.Exists(backendPath))
        {
            try 
            {
                Directory.Delete(backendPath, true);
                if (File.Exists(backendPath + ".meta")) File.Delete(backendPath + ".meta");
            }
            catch (System.Exception e) { Debug.LogError($"[ProjectManager] Delete backend folder failed: {e.Message}"); }
        }

        projects.Remove(current);
        Save();
        current = null;

        // Cleanup
        drawTool?.ClearAll();
        propertyPanel?.ClearPanel();
        SetupDropdown();

        if (renameProjectInput != null) renameProjectInput.text = "";
        projectDropdown?.SelectItem("Select Project");
    }

    // ============================================================
    // PROPERTY MANAGEMENT
    // ============================================================

    // Tambah property baru
    public void AddProperty(string name, bool value = false, bool isDrawing = false)
    {
        if (current == null) return;
        var dict = current.GetProps();
        if (dict.ContainsKey(name)) return;

        dict[name] = new PropertyPanel.PropertyInfo(value, isDrawing);
        current.SetProps(dict);
        Save();
        propertyPanel?.ShowPropertiesWithType(dict);
    }

    // Hapus property
    public void RemoveProperty(string name)
    {
        if (current == null) return;
        var dict = current.GetProps();
        if (!dict.ContainsKey(name)) return;

        dict.Remove(name);
        current.SetProps(dict);
        Save();
        propertyPanel?.ShowPropertiesWithType(dict);
    }

    // Callback saat property berubah dari panel
    public void OnPropertyChanged(string name, bool value)
    {
        if (current == null) return;
        var dict = current.GetProps();

        if (dict.ContainsKey(name))
        {
            bool wasDrawing = dict[name].isDrawing;
            dict[name] = new PropertyPanel.PropertyInfo(value, wasDrawing);
        }
        current.SetProps(dict);
        Save();
        drawTool?.SetLayerVisibility(name, value);
    }

    // Callback saat objek dihapus dari peta
    void OnObjectDeleted(DrawTool.DrawObject obj)
    {
        if (current?.drawings == null) return;
        int idx = current.drawings.FindIndex(x => x.id == obj.id);
        if (idx != -1) current.drawings.RemoveAt(idx);
        // Tidak auto-save, user harus klik Save button
    }

    // Callback saat property dihapus
    public void OnPropertyDeleted(string name)
    {
        if (current == null) return;

        // Hapus dari project props
        var dict = current.GetProps();
        if (dict.ContainsKey(name)) 
        { 
            dict.Remove(name); current.SetProps(dict);
            // Tidak auto-save, user harus klik Save button
        }

        if (current.drawings != null)
        {
            current.drawings.RemoveAll(d => d.layerName == name);
            Save();
        }

        // Hapus layer dan drawing
        tiffLayerManager?.RemoveLayer(name, true);
        drawTool?.DeleteLayer(name);
    }

    // Callback saat property di-rename
    public void OnPropertyRenamed(string oldName, string newName)
    {
        if (current == null || oldName == newName) return;

        var dict = current.GetProps();
        if (!dict.ContainsKey(oldName)) return;

        dict[newName] = dict[oldName];
        dict.Remove(oldName);
        current.SetProps(dict);

        if (current.drawings != null)
        {
            foreach (var drawing in current.drawings)
            {
                if (drawing.layerName == oldName) drawing.layerName = newName;
            }
        }

        Save();
        tiffLayerManager?.RenameLayer(oldName, newName);
        drawTool?.RenameLayer(oldName, newName);
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    // Getter project saat ini
    public ProjectData GetCurrentProject() => current;

    // Set visibility polygon project (ROI dan drawings)
    public void SetProjectPolygonVisibility(bool visible)
    {
        if (drawTool == null || current == null) return;

        // Toggle ROI Polygon
        string roiId = current.id + "_ROI";
        if (drawTool.HasDrawing(roiId)) drawTool.ShowDrawing(roiId, visible);

        // Toggle drawings
        if (current.drawings == null) return;
        var props = current.GetProps();

        foreach (var d in current.drawings)
        {
            if (!visible) { drawTool.ShowDrawing(d.id, false); continue; }

            // Jika visible=true, cek apakah layer drawing aktif
            bool layerActive = string.IsNullOrEmpty(d.layerName) || !props.ContainsKey(d.layerName) || props[d.layerName].value;
            if (layerActive) drawTool.ShowDrawing(d.id, true);
        }
    }

    // ============================================================
    // SAVE / LOAD
    // ============================================================

    // Simpan ke file JSON
    public void Save()
    {
        Debug.Log($"[Save] Writing {projects.Count} projects to {SavePath}");
        if (current != null)
        {
            Debug.Log($"[Save] Current project '{current.name}' has {current.drawings.Count} drawings");
        }
        try { File.WriteAllText(SavePath, JsonUtility.ToJson(new Wrapper { items = projects }, true)); }
        catch (System.Exception e) { Debug.LogError("Save failed: " + e.Message); }
    }

    // Simpan drawings di layer tertentu
    public void SaveLayer(string layerName)
    {
        if (current == null || drawTool == null || string.IsNullOrEmpty(layerName))
        {
            Debug.LogWarning("[SaveLayer] No current project or invalid layer");
            return;
        }
        
        Debug.Log($"[SaveLayer] Saving layer '{layerName}'");
        
        // Ambil drawings dari DrawTool
        var drawingsFromTool = drawTool.GetLayerDrawings(layerName);
        Debug.Log($"[SaveLayer] Found {drawingsFromTool.Count} drawings in DrawTool");
        
        // Hapus drawings lama dengan layer ini dari current.drawings
        current.drawings.RemoveAll(d => d.layerName == layerName);
        
        // Tambah drawings baru
        foreach (var obj in drawingsFromTool)
        {
            current.drawings.Add(new SerializedDrawObject
            {
                id = obj.id,
                type = obj.type,
                layerName = obj.layerName,
                coordinates = new List<Vector2>(obj.coordinates),
                useTexture = obj.useTexture
            });
        }
        
        Debug.Log($"[SaveLayer] Total drawings in project: {current.drawings.Count}");
        
        // Simpan ke JSON
        Save();
    }

    // Load dari file JSON
    void LoadProjects()
    {
        if (!File.Exists(SavePath)) return;
        try
        {
            var wrapper = JsonUtility.FromJson<Wrapper>(File.ReadAllText(SavePath));
            if (wrapper?.items != null) 
            {
                projects = wrapper.items;
                Debug.Log($"[Load] Loaded {projects.Count} projects");
                foreach (var p in projects)
                {
                    Debug.Log($"[Load] Project '{p.name}' has {p.drawings.Count} drawings");
                }
            }
        }
        catch { }
    }

    public void DiscardChanges(string layerName)
    {
        if (current == null || drawTool == null || string.IsNullOrEmpty(layerName)) return;
        
        Debug.Log($"[Discard] Layer: '{layerName}'");
        
        drawTool.DeactivateAllModes();
        drawTool.DeleteLayer(layerName);
        
        // Reload dari JSON untuk mendapat data tersimpan
        try
        {
            if (File.Exists(SavePath))
            {
                var wrapper = JsonUtility.FromJson<Wrapper>(File.ReadAllText(SavePath));
                var savedProject = wrapper?.items?.Find(p => p.id == current.id);
                
                if (savedProject != null)
                {
                    foreach (var d in savedProject.drawings)
                    {
                        if (d.layerName == layerName && !drawTool.HasDrawing(d.id))
                        {
                            drawTool.CreateObj(d.type, d.coordinates, d.layerName, d.useTexture, d.id);
                        }
                    }
                    current.drawings = savedProject.drawings;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Discard] Failed: {e.Message}");
        }
    }
}