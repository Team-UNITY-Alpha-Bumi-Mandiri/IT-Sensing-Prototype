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

        RunGeeExe(projName, imagery, correction, cloud, start, end, boundary, isSearch);
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
        else if (selectedBands.Count > 0)
        {
            string bandsArg = string.Join(",", selectedBands);
            args += $" --action download --bands \"{bandsArg}\"";
        }
        else
        {
            args += " --action download";
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
        // Output dari Python: ['B1', 'B2', ...]
        UnityEngine.Debug.Log("Search Output: " + output);
        
        List<string> bands = new List<string>();
        
        // Parsing sederhana manual (cari bagian di dalam [ ])
        int startIdx = output.IndexOf('[');
        int endIdx = output.LastIndexOf(']');
        
        if (startIdx >= 0 && endIdx > startIdx)
        {
            string clean = output.Substring(startIdx + 1, endIdx - startIdx - 1);
            string[] items = clean.Replace("'", "").Replace("\"", "").Split(',');
            foreach (var item in items)
            {
                string b = item.Trim();
                if (!string.IsNullOrEmpty(b)) bands.Add(b);
            }
        }
        
        // Jika gagal parsing atau output kosong, pakai fallback statis seperti sebelumnya
        if (bands.Count == 0)
        {
            UnityEngine.Debug.LogWarning("Failed to parse bands from output, using fallback list.");
            bands = GetBandsFor(imagery, correction);
        }

        if (textStatus != null) textStatus.text = $"Found {bands.Count} available bands.";
        PopulateBandUI(bands);
    }

    private void PopulateBandUI(List<string> bands)
    {
        if (bandContainer == null || bandPrefab == null) return;

        // Bersihkan list lama
        foreach (Transform child in bandContainer) Destroy(child.gameObject);
        selectedBands.Clear();

        foreach (var b in bands)
        {
            GameObject obj = Instantiate(bandPrefab, bandContainer);
            obj.name = "Band_" + b;
            
            Toggle t = obj.GetComponentInChildren<Toggle>();
            TMP_Text txt = obj.GetComponentInChildren<TMP_Text>();
            
            if (txt != null) txt.text = b;
            if (t != null)
            {
                t.isOn = false;
                t.onValueChanged.AddListener((val) => {
                    if (val) { if (!selectedBands.Contains(b)) selectedBands.Add(b); }
                    else { selectedBands.Remove(b); }
                });
            }
        }
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
