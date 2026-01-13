using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// =========================================
// Manager utama untuk CRUD project
// Create, Read, Update, Delete
// =========================================
public class ProjectManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController; // Kontrol peta
    public SearchableDropdown projectDropdown;     // Dropdown pilih project
    public DrawTool drawTool;                      // Tool gambar
    public PropertyPanel propertyPanel;            // Panel property
    public TiffLayerManager tiffLayerManager;      // Manager layer TIFF

    [Header("Form UI")]
    public TMP_InputField newProjectNameInput; // Input nama project baru
    public Button createProjectButton;         // Tombol buat project
    public Button deleteProjectButton;         // Tombol hapus project

    [Header("Rename UI")]
    public TMP_InputField renameProjectInput; // Input rename project
    public Button renameProjectButton;        // Tombol rename

    // =========================================
    // STRUKTUR DATA
    // =========================================

    // Entry property (untuk serialisasi JSON)
    [System.Serializable]
    public class PropertyEntry
    {
        public string key;
        public bool value;

        public PropertyEntry(string k, bool v)
        {
            key = k;
            value = v;
        }
    }

    // Objek gambar yang diserialisasi
    [System.Serializable]
    public class SerializedDrawObject
    {
        public string id; // Simpan ID agar bisa dihapus dengan akurat
        public DrawTool.DrawMode type;
        public string layerName;
        public List<Vector2> coordinates;
        public bool useTexture;
    }

    // Data project
    [System.Serializable]
    public class ProjectData
    {
        public string id;
        public string name;
        public double lat;
        public double lon;
        public int zoom;
        public string tiffPath; // Path ke file TIFF (opsional)
        public List<Vector2> polygonCoords;
        public List<PropertyEntry> properties = new List<PropertyEntry>();
        public List<SerializedDrawObject> drawings = new List<SerializedDrawObject>(); // Daftar gambar

        // Convert list ke dictionary
        public Dictionary<string, bool> GetProps()
        {
            Dictionary<string, bool> dict = new Dictionary<string, bool>();
            
            if (properties != null)
            {
                foreach (PropertyEntry p in properties)
                {
                    dict[p.key] = p.value;
                }
            }
            
            return dict;
        }

        // Set dari dictionary
        public void SetProps(Dictionary<string, bool> dict)
        {
            properties = new List<PropertyEntry>();
            
            foreach (var kv in dict)
            {
                properties.Add(new PropertyEntry(kv.Key, kv.Value));
            }
        }
    }

    // Wrapper untuk serialisasi JSON
    [System.Serializable]
    class Wrapper
    {
        public List<ProjectData> items;
    }

    // Variabel internal
    List<ProjectData> projects = new List<ProjectData>();
    ProjectData current = null;

    // =========================================
    // INISIALISASI
    // =========================================
    void Start()
    {
        // Load data tersimpan
        LoadProjects();

        // Setup dropdown
        SetupDropdown();

        // Setup listener tombol
        if (createProjectButton != null)
        {
            createProjectButton.onClick.AddListener(StartCreate);
        }

        if (deleteProjectButton != null)
        {
            deleteProjectButton.onClick.AddListener(Delete);
        }

        if (renameProjectButton != null)
        {
            renameProjectButton.onClick.AddListener(Rename);
        }

        // Listener Property Panel
        if (propertyPanel != null)
        {
            propertyPanel.onPropertyChanged.AddListener(OnPropertyChanged);
        }

        if (drawTool != null)
        {
            drawTool.onDrawComplete.AddListener(OnDrawn);
            drawTool.onObjectDeleted.AddListener(OnObjectDeleted); // Subscribe delete
        }

        current = null;
    }

    // =========================================
    // CUSTOM EVENT
    // =========================================
    // Event saat project dengan TIFF diload
    public UnityEngine.Events.UnityEvent<string> onTiffProjectLoaded; 

    // =========================================
    // DROPDOWN
    // =========================================
    void SetupDropdown()
    {
        if (projectDropdown == null) return;

        // Buat list nama project
        List<string> names = new List<string>();
        foreach (ProjectData p in projects)
        {
            names.Add(p.name);
        }

        // Set ke dropdown
        projectDropdown.SetOptions(names);
        
        // Setup listener
        projectDropdown.onValueChanged.RemoveListener(OnSelect);
        projectDropdown.onValueChanged.AddListener(OnSelect);
    }

    // Saat project dipilih dari dropdown
    void OnSelect(string name)
    {
        ProjectData p = projects.Find(x => x.name == name);
        if (p != null)
        {
            Select(p);
        }
    }

    // =========================================
    // CREATE (Buat Project Baru)
    // =========================================
    void StartCreate()
    {
        // Validasi nama
        if (newProjectNameInput == null || string.IsNullOrEmpty(newProjectNameInput.text))
        {
            Debug.LogWarning("Isi nama project terlebih dahulu!");
            return;
        }

        if (drawTool == null) return;

        // Mulai gambar polygon
        drawTool.ClearAll();
        drawTool.forceTextureOnNext = true;
        drawTool.ActivateMode(DrawTool.DrawMode.Polygon);
    }

    // Callback selesai gambar
    void OnDrawn(DrawTool.DrawObject obj)
    {
        // KASUS A: Tambah gambar ke project yang sedang aktif (Layer Drawing)
        if (current != null && !string.IsNullOrEmpty(obj.layerName))
        {
            current.drawings.Add(new SerializedDrawObject
            {
                id = obj.id,
                type = obj.type,
                layerName = obj.layerName,
                coordinates = new List<Vector2>(obj.coordinates),
                useTexture = obj.useTexture
            });
            Save();
            return; // Selesai
        }

        // KASUS B: Buat project baru (ROI Drawing)
        if (newProjectNameInput == null || string.IsNullOrEmpty(newProjectNameInput.text))
        {
            return;
        }

        if (obj.coordinates.Count == 0)
        {
            return;
        }

        // Buat project baru
        ProjectData proj = new ProjectData
        {
            id = System.Guid.NewGuid().ToString(),
            name = newProjectNameInput.text,
            lat = obj.coordinates[0].x,
            lon = obj.coordinates[0].y,
            zoom = (mapController != null) ? mapController.zoom : 15,
            polygonCoords = new List<Vector2>(obj.coordinates)
        };

        // Jika gambar diawali dari layer tertentu (selain auto-project)
        if (!string.IsNullOrEmpty(drawTool.currentDrawingLayer))
        {
            proj.drawings.Add(new SerializedDrawObject
            {
                id = obj.id,
                type = obj.type,
                layerName = obj.layerName,
                coordinates = new List<Vector2>(obj.coordinates),
                useTexture = obj.useTexture
            });
        }



        // Simpan
        projects.Add(proj);
        Save();

        // Update dropdown
        SetupDropdown();
        projectDropdown.onValueChanged.RemoveListener(OnSelect);
        projectDropdown.SelectItem(proj.name);
        projectDropdown.onValueChanged.AddListener(OnSelect);

        // Set sebagai current
        current = proj;
        
        if (renameProjectInput != null)
        {
            renameProjectInput.text = proj.name;
        }

        newProjectNameInput.text = "";

        // Tampilkan property
        if (propertyPanel != null)
        {
            propertyPanel.ShowProperties(proj.GetProps());
        }
    }

    // Buat Project Otomatis (untuk hasil sharpening)
    public ProjectData CreateProjectAuto(string name, double lat, double lon, int zoom, string tiffPath, List<Vector2> polyCoords = null)
    {
        ProjectData proj = new ProjectData
        {
            id = System.Guid.NewGuid().ToString(),
            name = name,
            lat = lat,
            lon = lon,
            zoom = zoom,
            tiffPath = tiffPath,
            polygonCoords = (polyCoords != null) ? new List<Vector2>(polyCoords) : new List<Vector2>()
        };

        // Simpan
        projects.Add(proj);
        Save();

        // Update dropdown
        SetupDropdown();
        
        // Select project ini
        Select(proj);
        if (projectDropdown != null)
        {
            projectDropdown.onValueChanged.RemoveListener(OnSelect);
            projectDropdown.SelectItem(proj.name);
            projectDropdown.onValueChanged.AddListener(OnSelect);
        }

        return proj;
    }



    // =========================================
    // SELECT / READ (Pilih Project)
    // =========================================
    void Select(ProjectData proj)
    {
        current = proj;

        // CATATAN: Tidak ada reset property di sini.
        // Toggle diatur via OverlayToggleController.


        // Set nama di input rename
        if (renameProjectInput != null)
        {
            renameProjectInput.text = proj.name;
        }

        // Pindah ke lokasi
        if (mapController != null)
        {
            mapController.GoToLocation(proj.lat, proj.lon, proj.zoom);
        }

        if (drawTool != null) 
        { 
            // Jangan hapus, tapi sembunyikan semua dulu (Brute Force Cleanup)
            drawTool.ForceHideAllVisuals(); 
        }

        // Tampilkan polygon profil (ROI)
        if (proj.polygonCoords != null && proj.polygonCoords.Count > 0)
        {
            string roiId = proj.id + "_ROI";
            if (drawTool.HasDrawing(roiId))
            {
                drawTool.ShowDrawing(roiId, true);
            }
            else
            {
                // Load baru dengan ID spesifik agar ter-link ke project ini
                // Gunakan LayerName unik agar tidak bisa di-toggle massal oleh script lain
                drawTool.LoadPolygon(proj.polygonCoords, true, "Loaded_" + roiId, roiId);
            }
        }

        // Load project ini
        if (proj.drawings != null && drawTool != null)
        {
            foreach (var d in proj.drawings)
            {
                // Cek apakah drawing sudah ada di memori?
                if (drawTool.HasDrawing(d.id))
                {
                    // Tampilkan kembali
                    drawTool.ShowDrawing(d.id, true);
                }
                else
                {
                    // Load baru
                    drawTool.CreateObj(d.type, d.coordinates, d.layerName, d.useTexture, d.id);
                }
            }
        }

        // Matikan gambar yang layernya OFF
        Dictionary<string, bool> props = proj.GetProps();
        foreach (var kv in props)
        {
            drawTool.SetLayerVisibility(kv.Key, kv.Value);
        }

        // Notify TiffLayerManager jika ada tiffPath
        if (!string.IsNullOrEmpty(proj.tiffPath))
        {
            onTiffProjectLoaded?.Invoke(proj.tiffPath);
        }
        else
        {
            // Reset tiff jika tidak ada
            onTiffProjectLoaded?.Invoke("");
        }

        // Tampilkan property
        if (propertyPanel != null)
        {
            propertyPanel.ShowProperties(proj.GetProps());
        }
    }

    // =========================================
    // RENAME / UPDATE (Ubah Nama)
    // =========================================
    void Rename()
    {
        // Validasi
        if (current == null) return;
        if (renameProjectInput == null) return;
        if (string.IsNullOrEmpty(renameProjectInput.text)) return;

        // Cek apakah berubah
        if (current.name == renameProjectInput.text)
        {
            return;
        }

        // Update nama
        current.name = renameProjectInput.text;
        Save();

        // Refresh dropdown
        SetupDropdown();

        if (projectDropdown != null)
        {
            projectDropdown.SelectItem(current.name);
        }
    }

    // =========================================
    // DELETE (Hapus Project)
    // =========================================
    void Delete()
    {
        if (current == null) return;

        if (!string.IsNullOrEmpty(current.tiffPath) && System.IO.File.Exists(current.tiffPath))
        {
            try
            {
                System.IO.File.Delete(current.tiffPath);
                
                // Hapus juga file .meta agar Unity Editor tidak error "Asset version error"
                string metaPath = current.tiffPath + ".meta";
                if (System.IO.File.Exists(metaPath))
                {
                    System.IO.File.Delete(metaPath);
                }

                // Hapus dari cache memory
                if (tiffLayerManager != null)
                {
                    tiffLayerManager.UnloadFromCache(current.tiffPath);
                }

                Debug.Log($"[ProjectManager] Deleted associated TIFF file: {current.tiffPath}");
                
#if UNITY_EDITOR
                // Paksa Unity Editor untuk refresh database agar file hilang dari Project View
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ProjectManager] Failed to delete TIFF file: {e.Message}");
            }
        }

        // Hapus dari list
        projects.Remove(current);
        Save();

        // Reset state
        current = null;

        if (drawTool != null)
        {
            drawTool.ClearAll();
        }

        if (propertyPanel != null)
        {
            propertyPanel.ClearPanel();
        }

        // Refresh dropdown
        SetupDropdown();

        if (renameProjectInput != null)
        {
            renameProjectInput.text = "";
        }

        if (projectDropdown != null)
        {
            projectDropdown.SelectItem("Select Project");
        }
    }

    // =========================================
    // PROPERTY MANAGEMENT
    // =========================================

    // Tambah property baru
    public void AddProperty(string name, bool value = false)
    {
        if (current == null) return;

        Dictionary<string, bool> dict = current.GetProps();
        
        if (!dict.ContainsKey(name))
        {
            dict[name] = value;
            current.SetProps(dict);
            Save();

            if (propertyPanel != null)
            {
                propertyPanel.ShowProperties(dict);
            }
        }
    }

    // Hapus property
    public void RemoveProperty(string name)
    {
        if (current == null) return;

        Dictionary<string, bool> dict = current.GetProps();
        
        if (dict.ContainsKey(name))
        {
            dict.Remove(name);
            current.SetProps(dict);
            Save();

            if (propertyPanel != null)
            {
                propertyPanel.ShowProperties(dict);
            }
        }
    }

    // Callback saat property berubah dari panel
    public void OnPropertyChanged(string name, bool value)
    {
        if (current == null) return;

        Dictionary<string, bool> dict = current.GetProps();
        dict[name] = value;
        current.SetProps(dict);
        Save();

        // Update visibilitas gambar di DrawTool
        if (drawTool != null)
        {
            drawTool.SetLayerVisibility(name, value);
        }
    }

    // Callback saat objek gambar dihapus dari peta
    void OnObjectDeleted(DrawTool.DrawObject obj)
    {
        if (current == null || current.drawings == null) return;

        // Cari berdasarkan ID
        int idx = current.drawings.FindIndex(x => x.id == obj.id);
        if (idx != -1)
        {
            current.drawings.RemoveAt(idx);
            Save();
            Debug.Log($"[ProjectManager] Deleted drawing {obj.id} from project data.");
        }
    }

    // Callback saat property dihapus (Layer/Drawing layer)
    public void OnPropertyDeleted(string name)
    {
        if (current == null) return;

        // 1. Hapus dari Project Props
        Dictionary<string, bool> dict = current.GetProps();
        if (dict.ContainsKey(name))
        {
            dict.Remove(name);
            current.SetProps(dict);
            Save();
        }

        // 2. Hubungi TiffLayerManager untuk hapus layer & file
        if (tiffLayerManager != null)
        {
            tiffLayerManager.RemoveLayer(name, true);
        }

        // 3. Hubungi DrawTool jika itu adalah layer gambar
        if (drawTool != null)
        {
            drawTool.DeleteLayer(name); 
        }

        Debug.Log($"[ProjectManager] Property deleted: {name}");
    }

    // Callback saat property di-rename
    public void OnPropertyRenamed(string oldName, string newName)
    {
        if (current == null) return;
        if (oldName == newName) return;

        // 1. Update Project Props
        Dictionary<string, bool> dict = current.GetProps();
        if (dict.ContainsKey(oldName))
        {
            bool val = dict[oldName];
            dict.Remove(oldName);
            dict[newName] = val;
            current.SetProps(dict);
            Save();
        }

        // 2. Update di TiffLayerManager (Rename File + Memory)
        if (tiffLayerManager != null)
        {
            tiffLayerManager.RenameLayer(oldName, newName);
        }

        // 3. Update di DrawTool
        if (drawTool != null)
        {
            drawTool.RenameLayer(oldName, newName);
        }

        Debug.Log($"[ProjectManager] Property renamed from {oldName} to {newName}");
    }

    // Getter project saat ini
    public ProjectData GetCurrentProject()
    {
        return current;
    }

    // Set visibility polygon project
    public void SetProjectPolygonVisibility(bool visible)
    {
        if (drawTool == null || current == null) return;

        // 1. Toggle ROI Polygon (ID: projectID + "_ROI")
        string roiId = current.id + "_ROI";
        if (drawTool.HasDrawing(roiId))
        {
            drawTool.ShowDrawing(roiId, visible);
        }

        // 2. Toggle Drawings milik project
        if (current.drawings != null)
        {
            Dictionary<string, bool> props = current.GetProps();

            foreach (var d in current.drawings)
            {
                // Jika visible=TRUE (ingin menampilkan):
                // Kita harus menghormati toggle layer individu.
                // Jika layer drawing itu OFF, maka jangan di-show.
                // Jika visible=FALSE (ingin menyembunyikan):
                // Force hide tidak peduli status layernya.
                
                if (visible)
                {
                    bool isLayerActive = true;
                    if (!string.IsNullOrEmpty(d.layerName) && props.ContainsKey(d.layerName))
                    {
                        isLayerActive = props[d.layerName];
                    }

                    if (isLayerActive)
                    {
                        drawTool.ShowDrawing(d.id, true);
                    }
                }
                else
                {
                    drawTool.ShowDrawing(d.id, false);
                }
            }
        }
    }

    // =========================================
    // SAVE / LOAD
    // =========================================
    
    // Path file save
    string SavePath
    {
        get { return System.IO.Path.Combine(Application.persistentDataPath, "projects.json"); }
    }

    // Simpan ke file
    public void Save()
    {
        try
        {
            Wrapper wrapper = new Wrapper { items = projects };
            string json = JsonUtility.ToJson(wrapper, true);
            System.IO.File.WriteAllText(SavePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal menyimpan: " + e.Message);
        }
    }

    // Load dari file
    void LoadProjects()
    {
        if (!System.IO.File.Exists(SavePath))
        {
            return;
        }

        try
        {
            string json = System.IO.File.ReadAllText(SavePath);
            Wrapper wrapper = JsonUtility.FromJson<Wrapper>(json);

            if (wrapper != null && wrapper.items != null)
            {
                projects = wrapper.items;
            }
        }
        catch
        {
            // Abaikan error load
        }
    }
}