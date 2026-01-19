using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

// ============================================================
// GeeDownloadController - Controller download citra satelit GEE
// ============================================================
// Fungsi:
// - Search scene satelit dari Google Earth Engine
// - Download scene yang dipilih
// - Tampilkan thumbnail preview di peta
// - Tambah hasil download sebagai layer
// ============================================================
public class GeeDownloadController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;            // Root panel GEE
    public TMP_Dropdown dropdownImagery;    // Dropdown pilih jenis citra (Sentinel, Landsat)
    public TMP_Dropdown dropdownCorrection; // Dropdown pilih koreksi (SR, TOA, Raw)
    public TMP_InputField inputCloud;       // Input max cloud cover (%)
    public TMP_InputField inputStartDate;   // Input tanggal mulai
    public TMP_InputField inputEndDate;     // Input tanggal akhir
    public Button btnSearch;                // Tombol search
    public Button btnGetFile;               // Tombol download
    public TMP_Text textStatus;             // Label status

    [Header("Bands Selection")]
    public Transform bandContainer;         // Container untuk scene items
    public GameObject bandPrefab;           // Prefab GeeSceneItem
    public GameObject bandsPanelRoot;       // Panel bands (hidden jika tidak ada hasil)

    [Header("References")]
    public TiffLayerManager tiffLayerManager;  // Manager layer untuk preview dan load
    public ProjectManager projectManager;       // Manager project untuk bounds polygon
    public DrawTool drawTool;                   // (tidak digunakan langsung)

    // Path dan config
    string backendFolder, outputBaseFolder;
    string exeName = "create_view_standalone.exe";
    
    // State
    List<string> selectedBands = new();
    string selectedSceneId = "", selectedSceneDate = "";
    double curNorth, curSouth, curWest, curEast;  // Bounds dari polygon project
    List<Texture2D> generatedTextures = new();     // Textures untuk cleanup

    // Wrapper class untuk parsing JSON
    [System.Serializable]
    public class SceneData { public string id, date, thumb, thumb_filename; public float cloud; }

    [System.Serializable]
    class SceneListWrapper { public List<SceneData> scenes; }

    // ============================================================
    // LIFECYCLE
    // ============================================================

    void Start()
    {
        // Hide panel di awal
        if (panelRoot != null) panelRoot.SetActive(false);

        // Setup paths
        backendFolder = Path.Combine(Application.streamingAssetsPath, "Backend");
        outputBaseFolder = Path.Combine(backendFolder, "downloaded_bands");
        if (!Directory.Exists(outputBaseFolder)) Directory.CreateDirectory(outputBaseFolder);

        // Setup button listeners
        btnSearch?.onClick.AddListener(() => ExecuteGee(true));
        btnGetFile?.onClick.AddListener(() => ExecuteGee(false));

        // Setup input defaults
        if (inputCloud != null) inputCloud.contentType = TMP_InputField.ContentType.IntegerNumber;
        if (inputStartDate != null && string.IsNullOrEmpty(inputStartDate.text)) inputStartDate.text = "2023-01-01";
        if (inputEndDate != null && string.IsNullOrEmpty(inputEndDate.text)) inputEndDate.text = "2023-12-31";
    }

    // Toggle panel visibility
    public void TogglePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(!panelRoot.activeSelf);
    }

    // ============================================================
    // GEE EXECUTION
    // ============================================================

    // Jalankan backend GEE (search atau download)
    void ExecuteGee(bool isSearch)
    {
        var proj = projectManager?.GetCurrentProject();
        if (proj == null) return;

        // Parse dropdown values
        string imagery = dropdownImagery != null
            ? dropdownImagery.options[dropdownImagery.value].text.ToLower().Replace(" ", "").Replace("lansat", "landsat")
            : "sentinel2";

        string corrLabel = dropdownCorrection != null
            ? dropdownCorrection.options[dropdownCorrection.value].text.ToLower()
            : "sr";

        string correction = corrLabel.Contains("surface") || corrLabel == "sr" ? "sr"
            : corrLabel.Contains("atmosphere") || corrLabel == "toa" ? "toa"
            : "raw";

        if (imagery == "sentinel1") correction = "raw";

        // Get input values dengan defaults
        string cloud = !string.IsNullOrEmpty(inputCloud?.text) ? inputCloud.text : "50";
        string start = !string.IsNullOrEmpty(inputStartDate?.text) ? inputStartDate.text : "2023-01-01";
        string end = !string.IsNullOrEmpty(inputEndDate?.text) ? inputEndDate.text : "2023-12-31";

        // Get boundary dari polygon project
        string boundary = GetBoundaryString();
        if (string.IsNullOrEmpty(boundary)) return;

        CalculateCurrentBounds(proj.polygonCoords);
        RunGeeExe(proj.name, imagery, correction, cloud, start, end, boundary, isSearch);
    }

    // Hitung bounds dari polygon project
    void CalculateCurrentBounds(List<Vector2> coords)
    {
        if (coords == null || coords.Count == 0) return;
        double n = double.MinValue, s = double.MaxValue, w = double.MaxValue, e = double.MinValue;
        foreach (var c in coords)
        {
            if (c.x > n) n = c.x;
            if (c.x < s) s = c.x;
            if (c.y < w) w = c.y;
            if (c.y > e) e = c.y;
        }
        curNorth = n; curSouth = s; curWest = w; curEast = e;
    }

    // Konversi polygon ke string boundary untuk argument
    string GetBoundaryString()
    {
        var proj = projectManager?.GetCurrentProject();
        if (proj?.polygonCoords == null || proj.polygonCoords.Count < 3) return "";

        var pairs = new List<string>();
        foreach (var c in proj.polygonCoords)
            pairs.Add($"{c.y:F6},{c.x:F6}");  // lon,lat format
        return string.Join(",", pairs);
    }

    // Jalankan executable GEE backend
    async void RunGeeExe(string projName, string imagery, string correction, string cloud, string start, string end, string boundary, bool isSearch)
    {
        // Disable buttons saat proses
        if (btnSearch != null) btnSearch.interactable = false;
        if (btnGetFile != null) btnGetFile.interactable = false;

        // Build arguments
        string combinedName = string.IsNullOrEmpty(selectedSceneDate) ? projName : $"{projName}/{selectedSceneDate}";
        string args = $"-n \"{combinedName}\" -i \"{imagery}\" -c \"{correction}\" -l {cloud} -s \"{start}\" -e \"{end}\" -b \"{boundary}\"";

        if (isSearch)
            args += " --action list";
        else
        {
            args += " --action download";
            if (!string.IsNullOrEmpty(selectedSceneId)) args += $" --scene-id \"{selectedSceneId}\"";
            if (selectedBands.Count > 0) args += $" --bands \"{string.Join(",", selectedBands)}\"";
        }

        if (textStatus != null) textStatus.text = isSearch ? "Searching..." : "Downloading...";
        UnityEngine.Debug.Log($"[GEE] {exeName} {args}");

        // Run process async
        string result = await Task.Run(() =>
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(backendFolder, exeName),
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = backendFolder,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            // Fix encoding issues on Windows
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

            try
            {
                using var p = Process.Start(startInfo);
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();
                return !string.IsNullOrEmpty(error) && !error.Contains("UserWarning") ? $"ERROR: {error}\n{output}" : output;
            }
            catch (System.Exception e) { return $"SYSTEM ERROR: {e.Message}"; }
        });

        // Handle result
        if (isSearch)
            ProcessSearchOutput(result);
        else if (result.Contains("successfully") || result.Contains("Downloaded"))
        {
            AddResultAsLayer(projName);
            if (textStatus != null) textStatus.text = "Download complete!";
        }
        else
        {
            UnityEngine.Debug.LogError("[GEE] " + result);
            if (textStatus != null) textStatus.text = "Error: " + (result.Length > 50 ? result[..50] + "..." : result);
        }

        // Re-enable buttons
        if (btnSearch != null) btnSearch.interactable = true;
        if (btnGetFile != null) btnGetFile.interactable = true;
    }

    // ============================================================
    // SEARCH OUTPUT PROCESSING
    // ============================================================

    // Parse output JSON dari search
    void ProcessSearchOutput(string output)
    {
        var scenes = new List<SceneData>();
        if (string.IsNullOrEmpty(output)) { if (textStatus != null) textStatus.text = "No response."; return; }

        try
        {
            int startIdx = FindJsonArrayStart(output);
            int endIdx = output.LastIndexOf(']');
            if (startIdx >= 0 && endIdx > startIdx)
            {
                string json = "{ \"scenes\": " + output.Substring(startIdx, endIdx - startIdx + 1) + " }";
                var wrapper = JsonUtility.FromJson<SceneListWrapper>(json);
                if (wrapper?.scenes != null) scenes = wrapper.scenes;
            }
        }
        catch (System.Exception e) { UnityEngine.Debug.LogError("[GEE] JSON Parse Error: " + e.Message); }

        if (textStatus != null) textStatus.text = scenes.Count > 0 ? $"Found {scenes.Count} scenes." : "No scenes found.";
        PopulateSceneUI(scenes);
    }

    // Find start of JSON array in output (skip non-JSON text)
    int FindJsonArrayStart(string output)
    {
        int pos = 0;
        while (pos < output.Length)
        {
            int found = output.IndexOf('[', pos);
            if (found == -1) break;
            for (int i = found + 1; i < output.Length && i < found + 50; i++)
            {
                if (char.IsWhiteSpace(output[i])) continue;
                if (output[i] == '{') return found;
                break;
            }
            pos = found + 1;
        }
        return -1;
    }

    // Buat UI untuk list scene
    void PopulateSceneUI(List<SceneData> scenes)
    {
        if (bandContainer == null || bandPrefab == null) return;

        // Cleanup
        foreach (Transform child in bandContainer) Destroy(child.gameObject);
        foreach (var tex in generatedTextures) if (tex != null) Destroy(tex);
        generatedTextures.Clear();
        selectedSceneId = "";

        // Show/hide panel
        if (bandsPanelRoot != null) bandsPanelRoot.SetActive(scenes.Count > 0);

        // Create scene items
        foreach (var s in scenes)
        {
            var obj = Instantiate(bandPrefab, bandContainer);
            obj.name = "Scene_" + s.date;

            var thumbTex = LoadTexture(s.thumb);
            if (thumbTex != null) generatedTextures.Add(thumbTex);

            // Coba gunakan GeeSceneItem script
            if (obj.TryGetComponent(out GeeSceneItem item))
            {
                item.Setup(s.id, s.date, thumbTex, (id, selItem) =>
                {
                    selectedSceneId = id;
                    selectedSceneDate = s.date;

                    // Deselect semua lainnya
                    foreach (Transform child in bandContainer)
                        if (child.TryGetComponent(out GeeSceneItem sc)) sc.SetSelected(sc == selItem);

                    // Load preview thumbnail di peta
                    if (!string.IsNullOrEmpty(s.thumb))
                        tiffLayerManager?.LoadPngOverlay(s.thumb, curNorth, curSouth, curWest, curEast, true);
                });
            }
            else
            {
                // Fallback untuk prefab lama
                var txt = obj.GetComponentInChildren<TMP_Text>();
                var rawImg = obj.GetComponentInChildren<RawImage>();
                var toggle = obj.GetComponentInChildren<Toggle>();

                if (txt != null) txt.text = s.date;
                if (rawImg != null && thumbTex != null) rawImg.texture = thumbTex;

                toggle?.onValueChanged.AddListener(val =>
                {
                    if (!val) return;
                    selectedSceneId = s.id;
                    selectedSceneDate = s.date;

                    // Deselect semua lainnya
                    foreach (Transform child in bandContainer)
                    {
                        var t = child.GetComponentInChildren<Toggle>();
                        if (t != null && t != toggle) t.SetIsOnWithoutNotify(false);
                    }

                    if (!string.IsNullOrEmpty(s.thumb))
                        tiffLayerManager?.LoadPngOverlay(s.thumb, curNorth, curSouth, curWest, curEast, true);
                });
            }
        }
    }

    // Load texture dari file path
    Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
        }
        catch { }
        return null;
    }

    // ============================================================
    // RESULTS
    // ============================================================

    // Tambah hasil download sebagai layer
    void AddResultAsLayer(string projName)
    {
        string targetDir = Path.Combine(outputBaseFolder, projName);
        if (!Directory.Exists(targetDir)) return;

        var files = Directory.GetFiles(targetDir, "*.png", SearchOption.AllDirectories);
        foreach (string pngPath in files)
        {
            if (pngPath.Contains("temp")) continue;

            // Default layer name dari nama file
            string layerName = Path.GetFileNameWithoutExtension(pngPath);
            
            // Cek apakah ada di subfolder (misal folder tanggal)
            // Jika ya, gunakan nama subfolder sebagai nama layer (User Request: 1 folder = 1 layer)
            string parentDir = Path.GetFileName(Path.GetDirectoryName(pngPath));
             if (!string.Equals(Path.GetFullPath(Path.GetDirectoryName(pngPath)).TrimEnd('\\', '/'), 
                               Path.GetFullPath(targetDir).TrimEnd('\\', '/'), 
                               System.StringComparison.OrdinalIgnoreCase))
            {
                layerName = parentDir;
            }

            // Load PNG dengan nama layer custom
            tiffLayerManager?.LoadPngOverlay(pngPath, curNorth, curSouth, curWest, curEast, false, false, layerName);

            projectManager?.AddProperty(layerName, true);
            projectManager?.Save();
        }

        // Cleanup preview
        tiffLayerManager?.RemoveLayer("PREVIEW_SATELIT");
        ResetBandsUI();
    }

    // Reset bands UI
    void ResetBandsUI()
    {
        if (bandsPanelRoot != null) bandsPanelRoot.SetActive(false);
        if (bandContainer != null) foreach (Transform child in bandContainer) Destroy(child.gameObject);
        foreach (var tex in generatedTextures) if (tex != null) Destroy(tex);
        generatedTextures.Clear();
        selectedSceneId = selectedSceneDate = "";
        selectedBands.Clear();
    }

    // Dapatkan daftar bands untuk imagery/correction tertentu (untuk reference)
    List<string> GetBandsFor(string imagery, string correction)
    {
        if (imagery == "sentinel1") return new List<string> { "VV", "VH" };
        if (imagery.StartsWith("sentinel")) return new List<string> { "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "B8A", "B9", "B11", "B12" };
        return correction == "sr"
            ? new List<string> { "SR_B1", "SR_B2", "SR_B3", "SR_B4", "SR_B5", "SR_B6", "SR_B7" }
            : new List<string> { "B1", "B2", "B3", "B4", "B5", "B6", "B7", correction == "raw" ? "B8" : "" }.FindAll(x => !string.IsNullOrEmpty(x));
    }
}
