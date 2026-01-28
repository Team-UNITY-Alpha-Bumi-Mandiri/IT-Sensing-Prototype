using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Linq; // Ditambahkan untuk LINQ (FindIndex/Any)

public class DropdownFilter : MonoBehaviour
{
    // ===================================
    // Struktur Data untuk Proyek (UPDATED)
    // ===================================
    [System.Serializable]
    public struct LocationData
    {
        public string id; // BARU: ID unik
        public string name;
        public string projectType; // BARU: Type (e.g., Draw)
        public string projectOutput; // BARU: Output
        public double latitude;
        public double longitude;
        public int zoomLevel;
    }

    [Header("Dependencies")]
    public SlippyMapController_noproxy1 mapController;

    [Header("UI Components (TMP)")]
    public GameObject dropdownPanel;
    public TMP_InputField searchInput;
    public Button dropdownButton;
    public Transform contentParent;

    [Header("Data and Prefab")]
    public GameObject itemPrefab;

    public List<LocationData> locationDataList = new List<LocationData>
    { 
        new LocationData { id = "id_yogya", name = "Yogyakarta", projectType = "Draw", projectOutput = "OutputA", latitude = -7.7956, longitude = 110.3695, zoomLevel = 13 },
        new LocationData { id = "id_solo", name = "Solo/Surakarta", projectType = "Data", projectOutput = "OutputB", latitude = -7.5562, longitude = 110.8306, zoomLevel = 13 }
        // ... Tambahkan data default Anda di sini
    };
    
    // Penyimpanan ID Proyek yang sedang aktif/dipilih
    private string currentSelectedId = "";

    private List<GameObject> allItems = new List<GameObject>();

    void Start()
    {
        GenerateListItems();

        dropdownButton.onClick.AddListener(ToggleDropdown);
        
        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(FilterList);
        }

        if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(false);
        }
        
        // Pilih item pertama sebagai default saat start
        if (locationDataList.Count > 0)
        {
            OnItemSelected(locationDataList[0], dropdownButton.GetComponentInChildren<TMP_Text>());
        }
    }

    /// <summary>
    /// Membuat objek list item berdasarkan locationDataList (DIPERBARUI untuk memuat ulang)
    /// </summary>
    public void GenerateListItems()
    {
        if (itemPrefab == null || contentParent == null) return;

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
        allItems.Clear();

        TMP_Text mainButtonText = dropdownButton.GetComponentInChildren<TMP_Text>();

        foreach (LocationData data in locationDataList)
        {
            GameObject newItem = Instantiate(itemPrefab, contentParent);
            
            TMP_Text textComponent = newItem.GetComponentInChildren<TMP_Text>(); 
            if (textComponent != null)
            {
                textComponent.text = data.name;
            }

            Button btn = newItem.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnItemSelected(data, mainButtonText));
            }

            allItems.Add(newItem);
        }
        
        // Paksa rebuild layout agar ScrollRect bisa scroll dengan benar
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent as RectTransform);
        
        // Pertahankan filter jika dropdown aktif
        if (searchInput != null && dropdownPanel.activeSelf)
        {
            FilterList(searchInput.text);
        }
    }

    /// <summary>
    /// Logika ketika user mengklik item dari daftar.
    /// </summary>
    void OnItemSelected(LocationData selectedData, TMP_Text mainButtonText)
    {
        currentSelectedId = selectedData.id;

        if (mapController != null)
        {
            mapController.GoToLocation(selectedData.latitude, selectedData.longitude, selectedData.zoomLevel);
        }

        if (mainButtonText != null)
        {
            mainButtonText.text = selectedData.name;
        }
        
        ToggleDropdown();
        // TODO: Anda bisa menambahkan logika di sini untuk memuat 'projectType' dan 'projectOutput' ke panel lain
    }

    void ToggleDropdown()
    {
        bool isActive = !dropdownPanel.activeSelf;
        dropdownPanel.SetActive(isActive);
        
        if (isActive && searchInput != null)
        {
            searchInput.text = "";
            FilterList("");
        }
    }

    void FilterList(string searchString)
    {
        searchString = searchString.ToLower();
        int dataIndex = 0;

        foreach (GameObject item in allItems)
        {
            if (dataIndex >= locationDataList.Count) break;

            string itemName = locationDataList[dataIndex].name.ToLower();
            
            bool isMatch = itemName.Contains(searchString);

            item.SetActive(isMatch);
            dataIndex++; 
        }
    }
    
    // ==========================================================
    // FUNGSI MANIPULASI DATA PROYEK (CREATE, RENAME, DELETE)
    // ==========================================================

    public void CreateNewProject(string projectName, string projectType, string projectOutput)
    {
        if (string.IsNullOrEmpty(projectName) || locationDataList.Any(d => d.name == projectName))
        {
            Debug.LogWarning("Gagal membuat proyek: Nama kosong atau sudah ada.");
            return;
        }

        string newId = "proj_" + System.DateTime.Now.Ticks.ToString();
        
        LocationData newData = new LocationData
        {
            id = newId,
            name = projectName,
            projectType = projectType,
            projectOutput = projectOutput,
            latitude = mapController != null ? mapController.latitude : -7.7956,
            longitude = mapController != null ? mapController.longitude : 110.3695,
            zoomLevel = mapController != null ? mapController.zoom : 13
        };

        locationDataList.Add(newData);
        GenerateListItems();
        
        // Langsung memilih proyek baru
        OnItemSelected(newData, dropdownButton.GetComponentInChildren<TMP_Text>());
    }

    public void RenameCurrentProject(string newName)
    {
        if (string.IsNullOrEmpty(currentSelectedId) || string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("Gagal mengganti nama: ID atau nama baru kosong.");
            return;
        }
        
        // Cek apakah nama baru sudah digunakan oleh proyek lain
        if (locationDataList.Any(d => d.name == newName && d.id != currentSelectedId))
        {
            Debug.LogWarning($"Gagal mengganti nama: Nama '{newName}' sudah digunakan.");
            return;
        }

        int index = locationDataList.FindIndex(d => d.id == currentSelectedId);

        if (index != -1)
        {
            LocationData dataToUpdate = locationDataList[index];
            dataToUpdate.name = newName;
            locationDataList[index] = dataToUpdate;

            GenerateListItems();
            
            TMP_Text mainButtonText = dropdownButton.GetComponentInChildren<TMP_Text>();
            if (mainButtonText != null)
            {
                mainButtonText.text = newName;
            }
        }
    }

    public void DeleteCurrentProject()
    {
        if (string.IsNullOrEmpty(currentSelectedId))
        {
            Debug.LogWarning("Gagal menghapus proyek: Tidak ada proyek yang dipilih.");
            return;
        }
        
        int index = locationDataList.FindIndex(d => d.id == currentSelectedId);

        if (index != -1)
        {
            locationDataList.RemoveAt(index);
            GenerateListItems();
            
            // Reset status yang dipilih
            currentSelectedId = "";
            TMP_Text mainButtonText = dropdownButton.GetComponentInChildren<TMP_Text>();
            if (mainButtonText != null)
            {
                mainButtonText.text = "Select Project";
            }
            
            // Opsional: Pilih proyek pertama jika ada
            if (locationDataList.Count > 0)
            {
                 OnItemSelected(locationDataList[0], dropdownButton.GetComponentInChildren<TMP_Text>());
            }
            // Jika tidak ada, biarkan "Select Project"
        }
    }
}