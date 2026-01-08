using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class SharpeningController : MonoBehaviour
{
    [Header("UI References (Panel)")]
    public GameObject panelRoot; // Panel utama untuk Show/Hide

    [Header("UI Input References (Buttons)")]
    // Kita gunakan GameObject agar Anda bisa menarik Button langsung dari Hierarchy
    public GameObject btnMultispectral; 
    public GameObject btnPanchromatic; 
    
    [Header("UI Input References (Fields)")]
    public TMP_InputField inputOutputName; // User mengetik nama hasil di sini
    public TMP_Dropdown dropdownMethod;    // Pilihan Algoritma (Gram-Schmidt/Wavelet)

    [Header("Buttons & Status")]
    public Button btnProcess;
    public TextMeshProUGUI textStatus;

    [Header("PCA Panel (Optional)")]
    public GameObject pcaPanelRoot;
    public Button pcaSubmitButton;
    public TMP_InputField pcaOutputInput;
    public GameObject pcaRgbButton;
    [Header("Pansharpening Panel")]
    public GameObject panPanelRoot;

    [Header("Layer Manager & Project")]
    public TiffLayerManager layerManager; 
    public ProjectManager projectManager; 
    public OverlayToggleController overlayToggleController;

    // --- Variabel Penyimpanan Data ---
    // RGB disimpan dalam List karena bisa lebih dari 1 file
    private List<string> currentRgbPaths = new List<string>(); 
    // PAN hanya 1 file
    private string currentPanPath = "";
    // Lokasi output otomatis
    private string selectedOutputFolder = ""; 

    void Start()
    {
        // 1. Sembunyikan panel saat start
        if (panelRoot != null) panelRoot.SetActive(false);

        // 2. Reset Status
        textStatus.text = "Siap.";
        btnProcess.interactable = false;

        // 3. Reset Label Tombol
        SetButtonText(btnMultispectral, "Pilih File RGB (Bisa > 1)...");
        SetButtonText(btnPanchromatic, "Pilih File PAN...");

        // 4. Set Default Output Folder (StreamingAssets/Backend/Sharpened_Results)
        selectedOutputFolder = Path.Combine(Application.streamingAssetsPath, "Backend", "Sharpened_Results");
        if (!Directory.Exists(selectedOutputFolder)) Directory.CreateDirectory(selectedOutputFolder);

        // 5. Listener validasi saat user mengetik nama
        inputOutputName.onValueChanged.AddListener(delegate { CheckReadiness(); });
        if (dropdownMethod != null)
        {
            bool hasPca = dropdownMethod.options.Any(o => o.text.ToLower().Contains("pca"));
            if (!hasPca)
            {
                dropdownMethod.options.Add(new TMP_Dropdown.OptionData("PCA"));
                dropdownMethod.RefreshShownValue();
            }
            dropdownMethod.onValueChanged.AddListener(delegate { UpdateModeUI(); CheckReadiness(); });
        }
        if (pcaSubmitButton != null)
        {
            pcaSubmitButton.onClick.AddListener(OnClickProcess);
        }
        UpdateModeUI();
    }

    // Fitur Show/Hide Panel
    public void TogglePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(!panelRoot.activeSelf);
    }

    // ========================================================================
    // 1. FUNGSI SELECT RGB (MULTI-SELECT SUPPORT)
    // ========================================================================
    public void OnClickSelectRGB()
    {
        // Menggunakan OpenFiles (Jamak) - Pastikan FileBrowserHelper sudah diupdate
        string[] paths = FileBrowserHelper.OpenFiles("Pilih File RGB (Tahan CTRL untuk pilih banyak)", "TIFF Files\0*.tif;*.tiff\0All Files\0*.*\0\0");
        
        if (paths != null && paths.Length > 0)
        {
            // Simpan ke list
            currentRgbPaths = new List<string>(paths);
            
            // Update Teks Tombol
            if (currentRgbPaths.Count == 1)
            {
                SetButtonText(btnMultispectral, Path.GetFileName(currentRgbPaths[0]));
            }
            else
            {
                SetButtonText(btnMultispectral, $"{currentRgbPaths.Count} File Dipilih");
            }
            CheckReadiness();
        }
    }

    // ========================================================================
    // 2. FUNGSI SELECT PAN (SINGLE SELECT)
    // ========================================================================
    public void OnClickSelectPAN()
    {
        // Menggunakan OpenFile (Tunggal)
        string path = FileBrowserHelper.OpenFile("Pilih File Panchromatic (PAN)", "TIFF Files\0*.tif;*.tiff\0All Files\0*.*\0\0");
        
        if (!string.IsNullOrEmpty(path))
        {
            currentPanPath = path;
            SetButtonText(btnPanchromatic, Path.GetFileName(path));
            CheckReadiness();
        }
    }

    // Helper: Mengubah teks di dalam Button (Mencari TMP atau Text biasa)
    void SetButtonText(GameObject btnObj, string newText)
    {
        if (btnObj == null) return;

        // Cari TextMeshPro
        TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = newText;
            return;
        }

        // Cari Text Biasa (Legacy)
        Text txt = btnObj.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.text = newText;
        }
    }

    // ========================================================================
    // 3. VALIDASI (CHECK READINESS)
    // ========================================================================
    void CheckReadiness()
    {
        bool isPca = IsPCASelected();
        bool isRgbReady = currentRgbPaths.Count > 0;
        bool isPanReady = !string.IsNullOrEmpty(currentPanPath) && File.Exists(currentPanPath);
        string nameText = isPca && pcaOutputInput != null ? pcaOutputInput.text : inputOutputName.text;
        bool isNameReady = !string.IsNullOrEmpty(nameText);

        btnProcess.interactable = isPca ? (isRgbReady && isNameReady) : (isRgbReady && isPanReady && isNameReady);
    }

    public void OnClickProcess()
    {
        string rawName = IsPCASelected() && pcaOutputInput != null ? pcaOutputInput.text : inputOutputName.text;
        string algo = dropdownMethod.options[dropdownMethod.value].text; 

        // Append timestamp untuk memastikan nama unik
        // Format: Name_yyyyMMdd_HHmmss
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string outName = $"{rawName}_{timestamp}";
        
        // Jalankan backend dengan List File RGB
        RunBackend(algo, outName, currentRgbPaths, currentPanPath);
    }

    // ========================================================================
    // 4. LOGIKA BACKEND (ASYNC)
    // ========================================================================
    async void RunBackend(string algorithm, string outputName, List<string> rgbFiles, string panPath)
    {
        // Kunci UI
        btnProcess.interactable = false;
        textStatus.text = $"Processing {algorithm}...";
        textStatus.color = Color.yellow;

        string backendFolder = Path.Combine(Application.streamingAssetsPath, "Backend");
        string lowerAlgo = algorithm.ToLower();
        string exeName = lowerAlgo.Contains("wavelet") ? "wavelet_direct.exe" : (lowerAlgo.Contains("pca") ? "pca.exe" : "gram_direct.exe");
        string fullExePath = Path.Combine(backendFolder, exeName);

        // Sanitasi Path
        panPath = panPath.Replace("\\", "/");
        string outputDir = selectedOutputFolder.Replace("\\", "/");
        
        // Ambil folder context dari file pertama (untuk parameter -v dummy)
        string folderContext = Path.GetDirectoryName(rgbFiles[0]).Replace("\\", "/");

        // --- MENYUSUN LIST ARGUMEN RGB ---
        // Contoh hasil: "C:/Data/B4.tif" "C:/Data/B3.tif" "C:/Data/B2.tif"
        string rgbArgs = "";
        foreach(string f in rgbFiles)
        {
            string cleanPath = f.Replace("\\", "/");
            rgbArgs += $"\"{cleanPath}\" "; // Spasi penting pemisah antar file
        }

        string args;
        bool isPca = lowerAlgo.Contains("pca");
        if (isPca)
        {
            args = $"-n \"{outputName}\" --rgb {rgbArgs}";
        }
        else
        {
            args = $"-n \"{outputName}\" -v \"{folderContext}\" -o \"{outputDir}\" --rgb {rgbArgs} --pan \"{panPath}\"";
        }

        UnityEngine.Debug.Log($"Command: {exeName} {args}");

        // Jalankan Process
        if (!System.IO.File.Exists(fullExePath))
        {
            if (isPca)
            {
                string pyPath = Path.Combine(backendFolder, "pca.py");
                if (!File.Exists(pyPath))
                {
                    textStatus.text = "Gagal: backend tidak ditemukan (pca_direct.exe/pca.py)";
                    textStatus.color = Color.red;
                    btnProcess.interactable = true;
                    return;
                }
                string resultOutputPy = await Task.Run(() =>
                {
                    ProcessStartInfo start = new ProcessStartInfo();
                    start.FileName = "python";
                    start.Arguments = $"\"{pyPath}\" {args}";
                    start.UseShellExecute = false;
                    start.RedirectStandardOutput = true;
                    start.RedirectStandardError = true;
                    start.CreateNoWindow = true;
                    start.WorkingDirectory = backendFolder;
                    try
                    {
                        Process p = Process.Start(start);
                        string output = p.StandardOutput.ReadToEnd();
                        string error = p.StandardError.ReadToEnd();
                        p.WaitForExit();
                        if (!string.IsNullOrEmpty(error) && !error.Contains("UserWarning"))
                            return "PYTHON ERROR: " + error;
                        return output;
                    }
                    catch (System.Exception e)
                    {
                        return "SYSTEM ERROR: " + e.Message;
                    }
                });
                await HandleResultOutput(resultOutputPy, outputName, algorithm);
                btnProcess.interactable = true;
                return;
            }
            else
            {
                textStatus.text = "Gagal: backend tidak ditemukan (" + exeName + ")";
                textStatus.color = Color.red;
                btnProcess.interactable = true;
                return;
            }
        }
        string resultOutput = await Task.Run(() =>
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = fullExePath;
            start.Arguments = args;
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.CreateNoWindow = true;
            start.WorkingDirectory = backendFolder;

            try
            {
                Process p = Process.Start(start);
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(error) && !error.Contains("UserWarning"))
                    return "PYTHON ERROR: " + error;
                return output;
            }
            catch (System.Exception e)
            {
                return "SYSTEM ERROR: " + e.Message;
            }
        });

        await HandleResultOutput(resultOutput, outputName, algorithm);

        btnProcess.interactable = true;
    }

    // ========================================================================
    // 5. LOAD RESULT TO LAYER (Logic Polygon)
    // ========================================================================
    void LoadResultTiff(string outputFolder, string outputName, string algorithm)
    {
        if (layerManager == null) return;

        string lower = algorithm.ToLower();
        string algoShort = lower.Contains("wavelet") ? "wavelet" : (lower.Contains("pca") ? "pca" : "gramschmidt");
        
        // Pattern nama file output Python: {nama}_{algo}_direct_{timestamp}.tif
        string pattern = $"{outputName}_{algoShort}_direct_*.tif";
        string[] files = Directory.GetFiles(outputFolder, pattern);

        UnityEngine.Debug.Log($"[Sharpening] Searching for pattern: {pattern} in {outputFolder}. Found {files.Length} files.");

        if (files.Length > 0)
        {
            // Ambil file paling baru (terakhir dibuat)
            string latestFile = files[files.Length - 1];
            UnityEngine.Debug.Log($"[Sharpening] Loading latest result: {latestFile}");
            
            // Logic Geo-Location
            if (layerManager.GetTiffBounds(latestFile, out double minLat, out double maxLat, out double minLon, out double maxLon))
            {
                double centerLat = (minLat + maxLat) / 2.0;
                double centerLon = (minLon + maxLon) / 2.0;
                int zoom = layerManager.CalculateFitZoom();
                
                List<Vector2> polyCoords = new List<Vector2>
                {
                    new Vector2((float)maxLat, (float)minLon),
                    new Vector2((float)maxLat, (float)maxLon),
                    new Vector2((float)minLat, (float)maxLon),
                    new Vector2((float)minLat, (float)minLon)
                };

                string projectName = $"{outputName}_{algoShort}";
                if (projectManager != null) 
                {
                    projectManager.CreateProjectAuto(projectName, centerLat, centerLon, zoom, latestFile, polyCoords);
                    // Toggle tetap OFF, user harus nyalakan manual
                }
                else 
                {
                    layerManager.LoadTiff(latestFile);
                }
            }
            else 
            {
                layerManager.LoadTiff(latestFile);
            }
        }
    }

    bool IsPCASelected()
    {
        if (dropdownMethod == null) return false;
        string algo = dropdownMethod.options[dropdownMethod.value].text.ToLower();
        return algo.Contains("pca");
    }

    void UpdateModeUI()
    {
        bool isPca = IsPCASelected();
        SetActiveSafe(pcaPanelRoot, isPca);
        SetActiveSafe(panPanelRoot, !isPca);
        SetActiveSafe(btnPanchromatic, !isPca);
        if (btnProcess != null) SetActiveSafe(btnProcess.gameObject, !isPca);
        if (isPca)
        {
            GameObject rgbBtn = pcaRgbButton != null ? pcaRgbButton : btnMultispectral;
            SetButtonText(rgbBtn, "Pilih File RGB (1 file atau 3+ band)...");
            SetButtonText(btnPanchromatic, "Tidak diperlukan untuk PCA");
        }
        else
        {
            SetButtonText(btnMultispectral, "Pilih File RGB (Bisa > 1)...");
            SetButtonText(btnPanchromatic, "Pilih File PAN...");
        }
    }

    void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    async Task HandleResultOutput(string resultOutput, string outputName, string algorithm)
    {
        if (resultOutput.Contains("\"status\": \"success\"") || resultOutput.Contains("successfuly"))
        {
            textStatus.text = "Sukses!";
            textStatus.color = Color.green;
            if (algorithm.ToLower().Contains("pca"))
            {
                try
                {
                    int s = resultOutput.IndexOf('{');
                    int e = resultOutput.LastIndexOf('}');
                    string json = (s >= 0 && e > s) ? resultOutput.Substring(s, e - s + 1) : resultOutput;
                    PCAResponse res = JsonUtility.FromJson<PCAResponse>(json);
                    if (res != null && res.status != null && res.status.ToLower().Contains("success") && !string.IsNullOrEmpty(res.path) && File.Exists(res.path))
                    {
                        LoadResultTiffFromPath(res.path, outputName, algorithm);
                        string outDir = Path.GetDirectoryName(res.path);
                        if (!string.IsNullOrEmpty(outDir) && Directory.Exists(outDir))
                            Process.Start("explorer.exe", outDir.Replace("/", "\\"));
                        return;
                    }
                }
                catch { }
            }
            LoadResultTiff(selectedOutputFolder, outputName, algorithm);
            if(Directory.Exists(selectedOutputFolder))
                Process.Start("explorer.exe", selectedOutputFolder.Replace("/", "\\"));
        }
        else
        {
            string msg = resultOutput.Length > 50 ? resultOutput.Substring(0, 50) + "..." : resultOutput;
            textStatus.text = "Gagal: " + msg;
            textStatus.color = Color.red;
            UnityEngine.Debug.LogError("FULL ERROR: " + resultOutput);
        }
    }

    void LoadResultTiffFromPath(string tiffPath, string outputName, string algorithm)
    {
        if (layerManager == null || string.IsNullOrEmpty(tiffPath)) return;
        string lower = algorithm.ToLower();
        string algoShort = lower.Contains("pca") ? "pca" : (lower.Contains("wavelet") ? "wavelet" : "gramschmidt");
        if (File.Exists(tiffPath))
        {
            UnityEngine.Debug.Log($"[Sharpening-PCA] Loading PCA result from path: {tiffPath}");
            if (layerManager.GetTiffBounds(tiffPath, out double minLat, out double maxLat, out double minLon, out double maxLon))
            {
                double centerLat = (minLat + maxLat) / 2.0;
                double centerLon = (minLon + maxLon) / 2.0;
                int zoom = layerManager.CalculateFitZoom();
                List<Vector2> polyCoords = new List<Vector2>
                {
                    new Vector2((float)maxLat, (float)minLon),
                    new Vector2((float)maxLat, (float)maxLon),
                    new Vector2((float)minLat, (float)maxLon),
                    new Vector2((float)minLat, (float)minLon)
                };
                string projectName = $"{outputName}_{algoShort}";
                if (projectManager != null) 
                {
                    projectManager.CreateProjectAuto(projectName, centerLat, centerLon, zoom, tiffPath, polyCoords);
                    // Toggle tetap OFF, user harus nyalakan manual
                }
                else 
                {
                    layerManager.LoadTiff(tiffPath);
                }
            }
            else
            {
                layerManager.LoadTiff(tiffPath);
            }
        }
    }

    [System.Serializable] class PCAResponseBounds { public double north; public double south; public double west; public double east; }
    [System.Serializable] class PCAResponse { public string status; public string filename; public string path; public string preview_png; public PCAResponseBounds bounds; public string algo; }
}
