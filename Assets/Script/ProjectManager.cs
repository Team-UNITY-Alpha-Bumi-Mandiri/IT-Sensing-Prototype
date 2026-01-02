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
        // Validasi
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

        // Tampilkan polygon
            if (proj.polygonCoords != null && proj.polygonCoords.Count > 0)
            {
                drawTool.LoadPolygon(proj.polygonCoords, true);
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
    }

    // Getter project saat ini
    public ProjectData GetCurrentProject()
    {
        return current;
    }

    // Set visibility polygon project
    public void SetProjectPolygonVisibility(bool visible)
    {
        if (drawTool != null)
        {
            drawTool.SetAllVisibility(visible);
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
    void Save()
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