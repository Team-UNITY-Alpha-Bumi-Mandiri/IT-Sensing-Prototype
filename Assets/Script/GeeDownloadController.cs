using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class GeeDownloadController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    // inputProjectName dihapus karena menggunakan project aktif
    public TMP_Dropdown dropdownImagery;
    public TMP_Dropdown dropdownCorrection; 
    public TMP_InputField inputCloud;
    public TMP_InputField inputStartDate; 
    public TMP_InputField inputEndDate;   
    public Button btnSearch;
    public Button btnGetFile;
    public TMP_Text textStatus;

    [Header("Bands Selection (Optional)")]
    public Transform bandContainer;     // Container untuk toggle band
    public GameObject bandPrefab;      // Prefab toggle (pakai Toggle standar atau PropertyToggleItem)
    public GameObject bandsPanelRoot; // Panel pendukung (scroll)

    [Header("References")]
    public TiffLayerManager tiffLayerManager;
    public ProjectManager projectManager;
    public DrawTool drawTool;

    private string backendFolder;
    private string exeName = "create_view_standalone.exe";
    private string outputBaseFolder;
    
    // Simpan band yang dipilih
    private List<string> selectedBands = new List<string>();
    
    // Simpan scene yang dipilih
    private string selectedSceneId = "";
    
    // Bounds pencarian saat ini (untuk preview di peta)
    private double curNorth, curSouth, curWest, curEast;
    
    // Cache texture preview agar tidak leak memory
    private List<Texture2D> generatedTextures = new List<Texture2D>();

    [System.Serializable]
    public class SceneData
    {
        public string id;
        public string date;
        public float cloud;
        public string thumb;
        public string thumb_filename;
    }

    [System.Serializable]
    private class SceneListWrapper
    {
        public List<SceneData> scenes;
    }

    void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        
        backendFolder = Path.Combine(Application.streamingAssetsPath, "Backend");
        outputBaseFolder = Path.Combine(backendFolder, "downloaded_bands");
        
        if (!Directory.Exists(outputBaseFolder)) Directory.CreateDirectory(outputBaseFolder);

        if (btnSearch != null) btnSearch.onClick.AddListener(OnClickSearch);
        if (btnGetFile != null) btnGetFile.onClick.AddListener(OnClickGetFile);
        
        // Pastikan input cloud hanya angka
        if (inputCloud != null) inputCloud.contentType = TMP_InputField.ContentType.IntegerNumber;
        
        // Safety Check untuk Pre-fill dates
        if (inputStartDate != null && string.IsNullOrEmpty(inputStartDate.text))
            inputStartDate.text = "2023-01-01";
        
        if (inputEndDate != null && string.IsNullOrEmpty(inputEndDate.text))
            inputEndDate.text = "2023-12-31";
    }

    public void TogglePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(!panelRoot.activeSelf);
    }

    public void OnClickSearch()
    {
        ExecuteGee(isSearch: true);
    }

    public void OnClickGetFile()
    {
        ExecuteGee(isSearch: false);
    }

    private void ExecuteGee(bool isSearch)
    {
        if (projectManager == null) return;
        var currentProj = projectManager.GetCurrentProject();
        if (currentProj == null) return;

        string projName = currentProj.name; 
        string imagery = (dropdownImagery != null) ? dropdownImagery.options[dropdownImagery.value].text.ToLower().Replace(" ", "") : "sentinel2";
        if (imagery.Contains("lansat")) imagery = imagery.Replace("lansat", "landsat");

        string correctionLabel = (dropdownCorrection != null) ? dropdownCorrection.options[dropdownCorrection.value].text.ToLower() : "sr";
        string correction = "sr";
        if (correctionLabel.Contains("surface") || correctionLabel == "sr") correction = "sr";
        else if (correctionLabel.Contains("atmosphere") || correctionLabel == "toa") correction = "toa";
        else if (correctionLabel.Contains("raw")) correction = "raw";
        if (imagery == "sentinel1") correction = "raw";

        string cloud = (inputCloud != null && !string.IsNullOrEmpty(inputCloud.text)) ? inputCloud.text : "50";
        string start = (inputStartDate != null && !string.IsNullOrEmpty(inputStartDate.text)) ? inputStartDate.text : "2023-01-01";
        string end = (inputEndDate != null && !string.IsNullOrEmpty(inputEndDate.text)) ? inputEndDate.text : "2023-12-31";

        string boundary = GetBoundaryString();
        if (string.IsNullOrEmpty(boundary)) return;

        // Hitung Bounds untuk preview di peta
        CalculateCurrentBounds(currentProj.polygonCoords);

        RunGeeExe(projName, imagery, correction, cloud, start, end, boundary, isSearch);
    }

    private void CalculateCurrentBounds(List<Vector2> coords)
    {
        if (coords == null || coords.Count == 0) return;
        
        double n = double.MinValue, s = double.MaxValue;
        double w = double.MaxValue, e = double.MinValue;

        foreach (var c in coords)
        {
            // Berdasarkan GetBoundaryString: c.x = Lat, c.y = Lon
            if (c.x > n) n = c.x;
            if (c.x < s) s = c.x;
            if (c.y < w) w = c.y;
            if (c.y > e) e = c.y;
        }

        curNorth = n;
        curSouth = s;
        curWest = w;
        curEast = e;
        
        UnityEngine.Debug.Log($"[GEE] Current Search Bounds: N:{curNorth}, S:{curSouth}, W:{curWest}, E:{curEast}");
    }

    private string GetBoundaryString()
    {
        var currentProj = projectManager.GetCurrentProject();
        if (currentProj == null || currentProj.polygonCoords == null || currentProj.polygonCoords.Count < 3) 
            return "";

        // Menggunakan koordinat polygon dari project aktif
        List<string> pairs = new List<string>();
        foreach (var c in currentProj.polygonCoords)
        {
            pairs.Add($"{c.y:F6},{c.x:F6}"); // Lon, Lat
        }

        return string.Join(",", pairs);
    }

    private async void RunGeeExe(string projName, string imagery, string correction, string cloud, string start, string end, string boundary, bool isSearch)
    {
        if (btnSearch != null) btnSearch.interactable = false;
        if (btnGetFile != null) btnGetFile.interactable = false;

        string fullExePath = Path.Combine(backendFolder, exeName);
        
        // Arguments
        string args = $"-n \"{projName}\" -i \"{imagery}\" -c \"{correction}\" -l {cloud} -s \"{start}\" -e \"{end}\" -b \"{boundary}\"";
        
        if (isSearch)
        {
            args += " --action list";
        }
        else 
        {
            args += " --action download";
            
            // Tambahkan scene selection jika ada
            if (!string.IsNullOrEmpty(selectedSceneId))
            {
                args += $" --scene-id \"{selectedSceneId}\"";
            }

            if (selectedBands.Count > 0)
            {
                string bandsArg = string.Join(",", selectedBands);
                args += $" --bands \"{bandsArg}\"";
            }
        }
        
        if (textStatus != null) 
            textStatus.text = isSearch ? "Searching available bands..." : "Downloading satellite imagery...";

        UnityEngine.Debug.Log($"Running GEE EXE: {exeName} {args}");

        string result = await Task.Run(() =>
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = fullExePath;
            startInfo.Arguments = args;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            startInfo.WorkingDirectory = backendFolder;
            
            // Tambahkan environment variable untuk memaksa UTF-8 (mencegah error charmap pada Windows)
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

            // Pastikan aliran output terbaca sebagai UTF-8
            startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

            try
            {
                Process p = Process.Start(startInfo);
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(error) && !error.Contains("UserWarning"))
                    return "ERROR: " + error + "\nOutput: " + output;
                return output;
            }
            catch (System.Exception e)
            {
                return "SYSTEM ERROR: " + e.Message;
            }
        });

        if (isSearch)
        {
            ProcessSearchOutput(result, imagery, correction);
        }
        else if (result.Contains("successfully") || result.Contains("Downloaded"))
        {
            AddResultAsLayer(projName);
            if (textStatus != null) textStatus.text = "Download completed successfully!";
        }
        else
        {
            UnityEngine.Debug.LogError("GEE Error: " + result);
            if (textStatus != null) textStatus.text = "Error: " + (result.Length > 50 ? result.Substring(0, 50) + "..." : result);
        }

        if (btnSearch != null) btnSearch.interactable = true;
        if (btnGetFile != null) btnGetFile.interactable = true;
    }

    private void ProcessSearchOutput(string output, string imagery, string correction)
    {
        UnityEngine.Debug.Log("Search Output (Raw): " + output);
        
        List<SceneData> scenes = new List<SceneData>();

        if (string.IsNullOrEmpty(output))
        {
            UnityEngine.Debug.LogError("GEE Search returned empty output.");
            if (textStatus != null) textStatus.text = "No response from search backend.";
            return;
        }

        try 
        {
            // Mencari awal JSON Array yang sebenarnya. 
            // Kita cari '[' yang diikuti oleh '{' (biasanya ada spasi atau enter di antaranya)
            int startIdx = -1;
            int searchPos = 0;
            
            while (searchPos < output.Length)
            {
                int found = output.IndexOf('[', searchPos);
                if (found == -1) break;
                
                // Cek apakah setelah '[' ada '{' (untuk memastikan ini awal JSON)
                // Kita abaikan spasi/newline
                bool isJsonStart = false;
                for (int i = found + 1; i < output.Length && i < found + 50; i++)
                {
                    char c = output[i];
                    if (char.IsWhiteSpace(c)) continue;
                    if (c == '{') { isJsonStart = true; break; }
                    break; // Karakter lain ditemukan, berarti bukan JSON
                }

                if (isJsonStart)
                {
                    startIdx = found;
                    break;
                }
                searchPos = found + 1;
            }

            int endIdx = output.LastIndexOf(']');
            
            if (startIdx >= 0 && endIdx > startIdx)
            {
                string jsonArrayPart = output.Substring(startIdx, endIdx - startIdx + 1);
                string wrappedJson = "{ \"scenes\": " + jsonArrayPart + " }";
                
                SceneListWrapper wrapper = JsonUtility.FromJson<SceneListWrapper>(wrappedJson);
                
                if (wrapper != null && wrapper.scenes != null)
                {
                    scenes = wrapper.scenes;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("Valid JSON array not found in output.");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to parse JSON Scenes: " + e.Message + "\nOutput: " + output);
        }

        if (textStatus != null) 
            textStatus.text = (scenes.Count > 0) ? $"Found {scenes.Count} available scenes." : "No scenes found.";

        PopulateSceneUI(scenes);
    }

    private void PopulateSceneUI(List<SceneData> scenes)
    {
        if (bandContainer == null || bandPrefab == null) return;

        // Bersihkan list & texture lama
        foreach (Transform child in bandContainer) Destroy(child.gameObject);
        foreach (var tex in generatedTextures) if (tex != null) Destroy(tex);
        generatedTextures.Clear();
        
        selectedSceneId = "";

        foreach (var s in scenes)
        {
            GameObject obj = Instantiate(bandPrefab, bandContainer);
            obj.name = "Scene_" + s.date;
            
            // Coba cari script GeeSceneItem (jika sudah dipasangi)
            // Jika tidak ada, pakai fallback logic yang lama
            GeeSceneItem item = obj.GetComponent<GeeSceneItem>();
            
            // Load Texture dari path absolut yang dikirim Python
            Texture2D thumbTex = LoadTexture(s.thumb);
            if (thumbTex != null) generatedTextures.Add(thumbTex);

            if (item != null)
            {
                item.Setup(s.id, s.date, s.cloud, thumbTex, (id, selectedItem) => {
                    selectedSceneId = id;
                    // Radio button behavior
                    foreach (Transform child in bandContainer)
                    {
                        var scItem = child.GetComponent<GeeSceneItem>();
                        if (scItem != null) scItem.SetSelected(scItem == selectedItem);
                    }
                    UnityEngine.Debug.Log("Selected: " + selectedSceneId);

                    // --- TAMPILKAN PREVIEW DI PETA ---
                    if (tiffLayerManager != null && !string.IsNullOrEmpty(s.thumb))
                    {
                        tiffLayerManager.LoadPngOverlay(s.thumb, curNorth, curSouth, curWest, curEast, true);
                    }
                });
            }
            else
            {
                // FALLBACK: Jika user masih pakai prefab lama (hanya teks)
                Toggle t = obj.GetComponentInChildren<Toggle>();
                TMP_Text txt = obj.GetComponentInChildren<TMP_Text>();
                RawImage rawImg = obj.GetComponentInChildren<RawImage>();

                if (txt != null) txt.text = s.date;
                if (rawImg != null && thumbTex != null) rawImg.texture = thumbTex;

                if (t != null)
                {
                    t.onValueChanged.AddListener((val) => {
                        if (val) {
                            selectedSceneId = s.id;
                            foreach (Transform child in bandContainer) {
                                Toggle otherT = child.GetComponentInChildren<Toggle>();
                                if (otherT != null && otherT != t) otherT.SetIsOnWithoutNotify(false);
                            }

                            // --- TAMPILKAN PREVIEW DI PETA (FALLBACK) ---
                            if (tiffLayerManager != null && !string.IsNullOrEmpty(s.thumb))
                            {
                                tiffLayerManager.LoadPngOverlay(s.thumb, curNorth, curSouth, curWest, curEast, true);
                            }
                        }
                    });
                }
            }
        }
    }

    private Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

        try 
        {
            byte[] data = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(data)) return tex;
        }
        catch (System.Exception e) {
            UnityEngine.Debug.LogWarning("Failed to load thumbnail: " + e.Message);
        }
        return null;
    }

    // Fungsi tambahan untuk memunculkan pilihan band setelah scene dipilih
    private void PopulateBandSelection(List<string> bands)
    {
        // Untuk saat ini kita simpan saja di logic, 
        // jika ingin UI terpisah bisa ditambahkan container baru.
        // Di sini kita default-kan semua band terpilih atau biarkan user pilih manual.
        UnityEngine.Debug.Log("Available bands for this satellite: " + string.Join(", ", bands));
    }

    private void AddResultAsLayer(string projName)
    {
        string targetDir = Path.Combine(outputBaseFolder, projName);
        if (!Directory.Exists(targetDir)) return;

        // Ambil semua file TIF di folder tersebut
        string[] files = Directory.GetFiles(targetDir, "*.tif", SearchOption.AllDirectories);
        if (files.Length == 0) return;

        foreach (string tiffPath in files)
        {
            // Abaikan thumb atau file sementara
            if (tiffPath.Contains("thumb") || tiffPath.Contains("temp")) continue;

            // 1. Load TIFF ke Map
            if (tiffLayerManager != null)
                tiffLayerManager.LoadTiff(tiffPath);

            // 2. Daftarkan sebagai property project
            string layerName = Path.GetFileNameWithoutExtension(tiffPath);
            if (projectManager != null)
            {
                projectManager.AddProperty(layerName, true);
                projectManager.Save();
            }
            
            UnityEngine.Debug.Log($"[GEE] Added new layer: {layerName} to project {projName}");
        }
    }

    // ==========================================
    // LOGIKA BAND SELECTION
    // ==========================================

    // Metod UpdateBandList dihapus karena sekarang digantikan oleh Search
    // private void UpdateBandList() ...

    private List<string> GetBandsFor(string imagery, string correction)
    {
        if (imagery == "sentinel1") return new List<string> { "VV", "VH" };

        if (imagery.StartsWith("sentinel"))
        {
            // Sentinel 2
            return new List<string> { "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "B8A", "B9", "B11", "B12" };
        }
        else
        {
            // Landsat 7,8,9
            if (correction == "sr") return new List<string> { "SR_B1", "SR_B2", "SR_B3", "SR_B4", "SR_B5", "SR_B6", "SR_B7" };
            
            // TOA & Raw
            List<string> l = new List<string> { "B1", "B2", "B3", "B4", "B5", "B6", "B7" };
            if (correction == "raw") l.Add("B8"); // Panchromatic
            return l;
        }
    }
}
