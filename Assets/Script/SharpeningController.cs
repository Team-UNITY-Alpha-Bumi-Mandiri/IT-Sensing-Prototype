using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

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

    [Header("Layer Manager & Project")]
    public TiffLayerManager layerManager; 
    public ProjectManager projectManager; 

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
        // RGB harus minimal 1, PAN harus ada, Nama output harus diisi
        bool isRgbReady = currentRgbPaths.Count > 0;
        bool isPanReady = !string.IsNullOrEmpty(currentPanPath) && File.Exists(currentPanPath);
        bool isNameReady = !string.IsNullOrEmpty(inputOutputName.text);

        btnProcess.interactable = isRgbReady && isPanReady && isNameReady;
    }

    public void OnClickProcess()
    {
        string outName = inputOutputName.text;
        string algo = dropdownMethod.options[dropdownMethod.value].text; 
        
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
        string exeName = algorithm.ToLower().Contains("wavelet") ? "wavelet_direct.exe" : "gram_direct.exe";
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

        // Format Argumen Lengkap
        // -n [Nama] -v [FolderCtx] -o [Output] --rgb [File1] [File2]... --pan [FilePAN]
        string args = $"-n \"{outputName}\" -v \"{folderContext}\" -o \"{outputDir}\" --rgb {rgbArgs} --pan \"{panPath}\"";

        UnityEngine.Debug.Log($"Command: {exeName} {args}");

        // Jalankan Process
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

        // Cek Hasil JSON
        if (resultOutput.Contains("\"status\": \"success\"") || resultOutput.Contains("successfuly"))
        {
            textStatus.text = "Sukses!";
            textStatus.color = Color.green;
            
            // Muat hasil ke Map
            LoadResultTiff(selectedOutputFolder, outputName, algorithm);
            
            // Buka folder
            if(Directory.Exists(selectedOutputFolder))
                Process.Start("explorer.exe", selectedOutputFolder.Replace("/", "\\"));
        }
        else
        {
            // Tampilkan error
            string msg = resultOutput.Length > 50 ? resultOutput.Substring(0, 50) + "..." : resultOutput;
            textStatus.text = "Gagal: " + msg;
            textStatus.color = Color.red;
            UnityEngine.Debug.LogError("FULL ERROR: " + resultOutput);
        }

        btnProcess.interactable = true;
    }

    // ========================================================================
    // 5. LOAD RESULT TO LAYER (Logic Polygon)
    // ========================================================================
    void LoadResultTiff(string outputFolder, string outputName, string algorithm)
    {
        if (layerManager == null) return;

        string algoShort = algorithm.ToLower().Contains("wavelet") ? "wavelet" : "gramschmidt";
        
        // Pattern nama file output Python: {nama}_{algo}_direct_{timestamp}.tif
        string pattern = $"{outputName}_{algoShort}_direct_*.tif";
        string[] files = Directory.GetFiles(outputFolder, pattern);

        if (files.Length > 0)
        {
            // Ambil file paling baru (terakhir dibuat)
            string latestFile = files[files.Length - 1];
            
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
}