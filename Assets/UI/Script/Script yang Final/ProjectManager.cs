using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ProjectManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController; 

    [Header("UI: Dropdown References")]
    public SearchableDropdown projectDropdown; // Reference to the new component

    // Struktur Data Proyek
    [System.Serializable]
    public class ProjectData
    {
        public string id;
        public string name;
        public double lat;
        public double lon;
        public int zoom;
        public List<Vector2> polygonCoords; // Store polygon shape
    }

    [Header("Create Project")]
    public TMP_InputField newProjectNameInput;
    public Button createProjectButton;
    public Button deleteProjectButton; // New Delete Button
    public DrawTool drawTool; // Reference to DrawTool

    // List untuk menyimpan data proyek
    private List<ProjectData> projects = new List<ProjectData>();
    private ProjectData currentProject;

    void Start()
    {
        // 1. Load Saved Data
        LoadProjects();

        // 2. Setup Dropdown
        SetupDropdown();

        // 3. Setup Creation
        if (createProjectButton != null)
            createProjectButton.onClick.AddListener(StartCreatingProject);
            
        if (deleteProjectButton != null)
            deleteProjectButton.onClick.AddListener(DeleteCurrentProject);
        
        if (drawTool != null)
            drawTool.onDrawComplete.AddListener(OnNewProjectDrawn);

        // 4. Pilih project pertama secara default saat play (jika ada)
        if(projects.Count > 0) 
        {
            SelectProject(projects[0]);
            if(projectDropdown != null) 
                projectDropdown.SelectItem(projects[0].name);
        }
    }

    void SetupDropdown()
    {
        if (projectDropdown == null) return;

        List<string> names = new List<string>();
        foreach (var p in projects) names.Add(p.name);

        projectDropdown.SetOptions(names);
        
        // Listen to change event (remove previous to avoid duplicates if called multiple times)
        projectDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        projectDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    // --- SAVE / LOAD LOGIC ---
    string GetSavePath() => System.IO.Path.Combine(Application.persistentDataPath, "projects.json");

    void SaveProjects()
    {
        try
        {
            string json = JsonUtility.ToJson(new ProjectListWrapper { items = projects }, true);
            System.IO.File.WriteAllText(GetSavePath(), json);
            Debug.Log($"Projects saved to: {GetSavePath()}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save projects: {e.Message}");
        }
    }

    void LoadProjects()
    {
        string path = GetSavePath();
        if (System.IO.File.Exists(path))
        {
            try
            {
                string json = System.IO.File.ReadAllText(path);
                ProjectListWrapper wrapper = JsonUtility.FromJson<ProjectListWrapper>(json);
                if (wrapper != null && wrapper.items != null)
                {
                    projects = wrapper.items;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load projects: {e.Message}");
            }
        }
    }

    [System.Serializable]
    private class ProjectListWrapper
    {
        public List<ProjectData> items;
    }

    // --- DELETE LOGIC ---
    void DeleteCurrentProject()
    {
        if (currentProject == null) return;

        Debug.Log($"Deleting project: {currentProject.name}");
        projects.Remove(currentProject);
        SaveProjects(); // Persist

        // Reset State
        currentProject = null;
        if (drawTool) drawTool.ClearAll(); // Clear map
        
        // Refresh UI
        SetupDropdown();
        
        // Select next available or clear
        if (projects.Count > 0)
        {
            SelectProject(projects[0]);
            if(projectDropdown != null) projectDropdown.SelectItem(projects[0].name);
        }
        else
        {
            if(projectDropdown != null) projectDropdown.SelectItem("Select Project");
            // Also maybe clear map input fields?
        }
    }

    // --- CREATE PROJECT LOGIC ---
    void StartCreatingProject()
    {
        if (string.IsNullOrEmpty(newProjectNameInput.text))
        {
            Debug.LogWarning("Project name cannot be empty!");
            return;
        }

        if (drawTool == null) return;
        
        // Clear previous drawings to prepare for new one
        drawTool.ClearAll();

        // Activate Polygon Mode
        drawTool.forceTextureOnNext = true; // Enable custom texture for this specific drawing
        drawTool.ActivateMode(DrawTool.DrawMode.Polygon);
        Debug.Log("Draw mode activated. Draw a polygon to define the project area.");
    }

    void OnNewProjectDrawn(DrawTool.DrawObject obj)
    {
        // Only trigger if we were waiting for a project input
        if (string.IsNullOrEmpty(newProjectNameInput.text)) return; 
        
        if (obj.coordinates.Count > 0)
        {
            ProjectData newProj = new ProjectData
            {
                id = System.Guid.NewGuid().ToString(),
                name = newProjectNameInput.text,
                lat = obj.coordinates[0].x,
                lon = obj.coordinates[0].y,
                zoom = mapController ? mapController.zoom : 15,
                polygonCoords = new List<Vector2>(obj.coordinates)
            };

            projects.Add(newProj);
            SaveProjects(); // <--- SAVE HERE
            
            SetupDropdown(); 
            projectDropdown.SelectItem(newProj.name);
            newProjectNameInput.text = "";
            drawTool.DeactivateMode(DrawTool.DrawMode.Polygon);
        }
    }

    void OnDropdownValueChanged(string projectName)
    {
        // Cari project berdasarkan nama
        ProjectData selected = projects.Find(p => p.name == projectName);
        if (selected != null)
        {
            SelectProject(selected);
        }
    }

    // Logika saat sebuah proyek dipilih
    void SelectProject(ProjectData proj)
    {
        currentProject = proj;

        // Pindahkan Peta ke lokasi proyek
        if (mapController != null)
        {
            mapController.GoToLocation(proj.lat, proj.lon, proj.zoom);
        }

        // ISOLATE VISIBILITY: Show only this project's polygon
        if (drawTool != null)
        {
            drawTool.ClearAll(); // Hide everything else
            
            if (proj.polygonCoords != null && proj.polygonCoords.Count > 0)
            {
                drawTool.LoadPolygon(proj.polygonCoords, true); // Show this one WITH texture
            }
        }
        
        Debug.Log($"[ProjectManager] Proyek dimuat: {proj.name}");
    }
}