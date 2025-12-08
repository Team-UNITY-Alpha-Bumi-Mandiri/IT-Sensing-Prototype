using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ProjectManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController; // Hubungkan ke skrip map SimpleMapController

    [Header("UI: Dropdown References")]
    public TMP_Text selectedProjectText; // Teks yang ada di tombol utama dropdown
    public GameObject dropdownListPanel; // Panel (anak) yang berisi daftar item (awalnya hidden)
    public Transform contentParent;      // Objek "Content" di dalam ScrollView/Panel tempat item dimunculkan
    public GameObject itemPrefab;        // Prefab tombol untuk item dropdown

    // Struktur Data Proyek
    [System.Serializable]
    public class ProjectData
    {
        public string id;
        public string name;
        public double lat;
        public double lon;
        public int zoom;
    }

    // List untuk menyimpan data proyek
    private List<ProjectData> projects = new List<ProjectData>();
    private ProjectData currentProject;

    void Start()
    {
        // 1. Buat data dummy (Contoh Lokasi)
        projects.Add(new ProjectData { id="1", name="Yogyakarta", lat=-7.7956, lon=110.3695, zoom=14 });
        projects.Add(new ProjectData { id="2", name="Jakarta", lat=-6.2088, lon=106.8456, zoom=12 });
        projects.Add(new ProjectData { id="3", name="Surabaya", lat=-7.2575, lon=112.7521, zoom=13 });

        // 2. Refresh isi dropdown UI
        RefreshDropdownList();
        
        // 3. Pilih project pertama secara default saat play
        if(projects.Count > 0) 
            SelectProject(projects[0]);

        // 4. Pastikan panel dropdown tertutup saat mulai
        if(dropdownListPanel != null) 
            dropdownListPanel.SetActive(false);
    }

    // --- LOGIKA UTAMA ---

    // Dipanggil saat tombol utama dropdown diklik
    public void ToggleDropdown()
    {
        if (dropdownListPanel != null)
            dropdownListPanel.SetActive(!dropdownListPanel.activeSelf);
    }

    // Membuat ulang daftar item dropdown berdasarkan list 'projects'
    void RefreshDropdownList()
    {
        if (itemPrefab == null || contentParent == null) return;

        // Hapus item lama (bersihkan anak-anak contentParent)
        foreach (Transform child in contentParent) 
            Destroy(child.gameObject);

        // Buat item baru (Instantiate)
        foreach (var proj in projects)
        {
            GameObject obj = Instantiate(itemPrefab, contentParent);
            
            // Ganti teks pada prefab
            TMP_Text txt = obj.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = proj.name;
            
            // Tambahkan fungsi klik (Listener)
            Button btn = obj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => {
                    SelectProject(proj); // Pilih proyek ini
                    ToggleDropdown();    // Tutup dropdown setelah memilih
                });
            }
        }
    }

    // Logika saat sebuah proyek dipilih
    void SelectProject(ProjectData proj)
    {
        currentProject = proj;

        // 1. Ubah teks di tombol utama
        if (selectedProjectText != null) 
            selectedProjectText.text = proj.name;

        // 2. Pindahkan Peta ke lokasi proyek
        if (mapController != null)
        {
            mapController.GoToLocation(proj.lat, proj.lon, proj.zoom);
        }
        
        Debug.Log($"[ProjectManager] Proyek dimuat: {proj.name}");
    }
}