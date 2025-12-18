using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Manajer utama sistem proyek: Handle Create, Read, Update, Delete (CRUD) & Visualisasi.
public class ProjectManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;  // Kontrol navigasi peta
    public SearchableDropdown projectDropdown;      // UI pilihan project
    public DrawTool drawTool;                       // Alat menggambar peta
    public PropertyPanel propertyPanel;             // Panel property toggle

    [Header("Form UI")]
    public TMP_InputField newProjectNameInput;      // Input nama baru
    public Button createProjectButton;              // Tombol buat
    public Button deleteProjectButton;              // Tombol hapus
    
    [Header("Rename UI")]
    public TMP_InputField renameProjectInput;       // Input ubah nama
    public Button renameProjectButton;              // Tombol simpan nama

    // Struktur data property (untuk serialisasi)
    [System.Serializable]
    public class PropertyEntry
    {
        public string key;
        public bool value;
        public PropertyEntry(string k, bool v) { key = k; value = v; }
    }

    // Struktur Data
    [System.Serializable]
    public class ProjectData
    {
        public string id, name;
        public double lat, lon;
        public int zoom;
        public List<Vector2> polygonCoords;
        public List<PropertyEntry> properties = new(); // Property toggle per project
        
        // Helper: Convert list ke dictionary
        public Dictionary<string, bool> GetPropertiesDict()
        {
            var dict = new Dictionary<string, bool>();
            if (properties != null)
                foreach (var p in properties) dict[p.key] = p.value;
            return dict;
        }
        
        // Helper: Set properties dari dictionary
        public void SetPropertiesFromDict(Dictionary<string, bool> dict)
        {
            properties = new List<PropertyEntry>();
            foreach (var kvp in dict) properties.Add(new PropertyEntry(kvp.Key, kvp.Value));
        }
    }

    [System.Serializable] class Wrapper { public List<ProjectData> items; } // Helper JSON

    private List<ProjectData> projects = new();
    private ProjectData currentProject;

    void Start()
    {
        LoadProjects();  // 1. Muat data saved
        SetupDropdown(); // 2. Isi UI Dropdown

        // 3. Setup Listener Tombol
        if (createProjectButton) createProjectButton.onClick.AddListener(StartCreating);
        if (deleteProjectButton) deleteProjectButton.onClick.AddListener(DeleteProject);
        if (renameProjectButton) renameProjectButton.onClick.AddListener(RenameProject);
        if (drawTool) drawTool.onDrawComplete.AddListener(OnProjectDrawn);

        currentProject = null; // Reset state awal
    }

    // --- FITUR: UI DROPDOWN ---

    // Mengisi opsi dropdown dan reset listener
    void SetupDropdown()
    {
        if (!projectDropdown) return;
        List<string> names = new();
        foreach (var p in projects) names.Add(p.name);

        projectDropdown.SetOptions(names);
        projectDropdown.onValueChanged.RemoveListener(OnSelectProjectByName);
        projectDropdown.onValueChanged.AddListener(OnSelectProjectByName);
    }

    void OnSelectProjectByName(string name)
    {
        var proj = projects.Find(p => p.name == name);
        if (proj != null) SelectProject(proj);
    }

    // --- FITUR: BUAT PROJECT (CREATE) ---

    // Mulai mode gambar untuk project baru
    void StartCreating()
    {
        if (string.IsNullOrEmpty(newProjectNameInput.text)) { Debug.LogWarning("Isi nama project!"); return; }
        if (!drawTool) return;

        drawTool.ClearAll(); // Bersihkan peta
        drawTool.forceTextureOnNext = true; // Pakai tekstur untuk area
        drawTool.ActivateMode(DrawTool.DrawMode.Polygon); // Aktifkan tool polygon
    }

    // Callback saat gambar selesai: Simpan data project baru
    void OnProjectDrawn(DrawTool.DrawObject obj)
    {
        if (string.IsNullOrEmpty(newProjectNameInput.text) || obj.coordinates.Count == 0) return;

        // Buat objek data baru
        var newProj = new ProjectData {
            id = System.Guid.NewGuid().ToString(),
            name = newProjectNameInput.text,
            lat = obj.coordinates[0].x,
            lon = obj.coordinates[0].y,
            zoom = mapController ? mapController.zoom : 15,
            polygonCoords = new List<Vector2>(obj.coordinates)
        };
        
        // Tambahkan dummy properties berbeda untuk setiap project
        AddRandomDummyProperties(newProj);

        projects.Add(newProj);
        SaveProjects();
        
        // Update UI Dropdown tanpa memicu event select ulang (cegah glitch)
        SetupDropdown(); 
        projectDropdown.onValueChanged.RemoveListener(OnSelectProjectByName);
        projectDropdown.SelectItem(newProj.name); 
        projectDropdown.onValueChanged.AddListener(OnSelectProjectByName);
        
        // Set manual state project yang baru dibuat
        currentProject = newProj;
        if (renameProjectInput) renameProjectInput.text = newProj.name;
        newProjectNameInput.text = "";
        
        // Tampilkan property panel untuk project baru
        if (propertyPanel) propertyPanel.ShowProperties(newProj.GetPropertiesDict());
        
        // Catatan: Jangan panggil DeactivateMode disini agar tidak membatalkan gambar yang baru jadi.
    }
    
    // Menambahkan dummy properties acak ke project baru
    void AddRandomDummyProperties(ProjectData proj)
    {
        // Daftar property dummy yang mungkin
        string[] allProperties = {
            "Show Label", "Enable Zoom", "Auto Refresh", "Show Grid",
            "Night Mode", "Show Coordinates", "Lock View", "Show Scale"
        };
        
        // Pilih 1-3 property secara acak
        int count = Random.Range(1, 4);
        var usedIndices = new List<int>();
        var dict = new Dictionary<string, bool>();
        
        for (int i = 0; i < count && i < allProperties.Length; i++)
        {
            int idx;
            do { idx = Random.Range(0, allProperties.Length); }
            while (usedIndices.Contains(idx));
            
            usedIndices.Add(idx);
            dict[allProperties[idx]] = Random.value > 0.5f; // Random true/false
        }
        
        proj.SetPropertiesFromDict(dict);
    }

    // --- FITUR: PILIH PROJECT (READ) ---

    // Muat visual dan kamera ke lokasi project
    void SelectProject(ProjectData proj)
    {
        currentProject = proj;
        if (renameProjectInput) renameProjectInput.text = proj.name; // Isi form rename
        if (mapController) mapController.GoToLocation(proj.lat, proj.lon, proj.zoom); // Pindah kamera

        // Tampilkan Polygon
        if (drawTool) {
            drawTool.ClearAll();
            if (proj.polygonCoords?.Count > 0) drawTool.LoadPolygon(proj.polygonCoords, true);
        }
        
        // Tampilkan PropertyPanel dengan properties project ini
        if (propertyPanel) propertyPanel.ShowProperties(proj.GetPropertiesDict());
    }

    // --- FITUR: UBAH NAMA (UPDATE) ---

    void RenameProject()
    {
        if (currentProject == null || !renameProjectInput || string.IsNullOrEmpty(renameProjectInput.text)) return;
        
        string newName = renameProjectInput.text;
        if (currentProject.name == newName) return; // Tidak berubah

        currentProject.name = newName;
        SaveProjects(); // Simpan ke file
        SetupDropdown(); // Refresh list UI
        
        // Update selection UI agar sinkron
        if (projectDropdown) projectDropdown.SelectItem(newName);
    }

    // --- FITUR: HAPUS (DELETE) ---

    void DeleteProject()
    {
        if (currentProject == null) return;
        
        projects.Remove(currentProject);
        SaveProjects();

        // Reset semua ke kondisi kosong
        currentProject = null;
        if (drawTool) drawTool.ClearAll();
        if (propertyPanel) propertyPanel.ClearPanel(); // Kosongkan property panel
        SetupDropdown();
        if (renameProjectInput) renameProjectInput.text = "";
        if (projectDropdown) projectDropdown.SelectItem("Select Project");
    }

    // --- FITUR: MANAJEMEN PROPERTY ---

    // Tambah property baru ke project yang aktif
    public void AddPropertyToCurrentProject(string propertyName, bool defaultValue = false)
    {
        if (currentProject == null) return;
        
        var dict = currentProject.GetPropertiesDict();
        if (!dict.ContainsKey(propertyName))
        {
            dict[propertyName] = defaultValue;
            currentProject.SetPropertiesFromDict(dict);
            SaveProjects();
            
            // Update UI
            if (propertyPanel) propertyPanel.ShowProperties(dict);
        }
    }

    // Hapus property dari project yang aktif
    public void RemovePropertyFromCurrentProject(string propertyName)
    {
        if (currentProject == null) return;
        
        var dict = currentProject.GetPropertiesDict();
        if (dict.ContainsKey(propertyName))
        {
            dict.Remove(propertyName);
            currentProject.SetPropertiesFromDict(dict);
            SaveProjects();
            
            // Update UI
            if (propertyPanel) propertyPanel.ShowProperties(dict);
        }
    }

    // Callback saat property toggle berubah di panel (untuk dipanggil dari PropertyPanel event)
    public void OnPropertyToggleChanged(string propertyName, bool value)
    {
        if (currentProject == null) return;
        
        var dict = currentProject.GetPropertiesDict();
        dict[propertyName] = value;
        currentProject.SetPropertiesFromDict(dict);
        SaveProjects();
    }

    // Getter project yang sedang aktif
    public ProjectData GetCurrentProject() => currentProject;

    // --- SISTEM PENYIMPANAN (JSON) ---

    string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "projects.json");

    void SaveProjects()
    {
        try {
            System.IO.File.WriteAllText(SavePath, JsonUtility.ToJson(new Wrapper { items = projects }, true));
        } catch (System.Exception e) { Debug.LogError("Gagal simpan: " + e.Message); }
    }

    void LoadProjects()
    {
        if (!System.IO.File.Exists(SavePath)) return;
        try {
            var w = JsonUtility.FromJson<Wrapper>(System.IO.File.ReadAllText(SavePath));
            if (w?.items != null) projects = w.items;
        } catch { } // Abaikan error load
    }
}