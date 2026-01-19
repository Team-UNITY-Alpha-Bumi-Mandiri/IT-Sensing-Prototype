using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

public class CompositeManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public Button btnSelectInput;
    public TextMeshProUGUI textInputFilename; // Tampilkan nama file yang dipilih
    
    [Header("Band Selection")]
    public TMP_Dropdown dropdownRed;
    public TMP_Dropdown dropdownGreen;
    public TMP_Dropdown dropdownBlue;
    
    [Header("Output")]
    public TMP_InputField inputOutputName;
    public Button btnProcess;
    public TextMeshProUGUI textStatus;

    [Header("Settings")]
    public string pythonScriptName = "composite2_standalone.py";
    public string exeName = "composite2_standalone.exe"; // Jika nanti di-build jadi exe
    public bool useExeInEditor = false; // Debugging flag

    private string currentInputPath = "";
    private string backendFolder;
    private string outputFolder;

    // List opsi band (Statis 1-12 untuk Landsat/Sentinel)
    private readonly List<string> bandOptions = new List<string> 
    { 
        "Band 1", "Band 2", "Band 3", "Band 4", "Band 5", "Band 6", 
        "Band 7", "Band 8", "Band 9", "Band 10", "Band 11", "Band 12" 
    };

    void Start()
    {
        // 1. Setup Folder
        string streamingAssets = Application.streamingAssetsPath.Replace("/", "\\");
        backendFolder = Path.Combine(streamingAssets, "Backend");
        outputFolder = Path.Combine(backendFolder, "Composite_Results");
        
        // [HARDCODED TARGET] Langsung set exe name untuk memastikan
        exeName = "composite2_standalone.exe";

        if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

        // Debug check
        string targetExe = Path.Combine(backendFolder, exeName);
        UnityEngine.Debug.Log($"[Composite] Target EXE: {targetExe} (Exists: {File.Exists(targetExe)})");

        // 2. Setup Dropdowns
        SetupDropdown(dropdownRed, 3);   // Default Band 4 (Red for L8) -> Index 3
        SetupDropdown(dropdownGreen, 2); // Default Band 3 (Green for L8) -> Index 2
        SetupDropdown(dropdownBlue, 1);  // Default Band 2 (Blue for L8) -> Index 1

        // 3. Listeners
        if (btnSelectInput) btnSelectInput.onClick.AddListener(OnClickSelectInput);
        if (btnProcess) btnProcess.onClick.AddListener(OnClickProcess);
        
        // Listener validasi
        if (inputOutputName) inputOutputName.onValueChanged.AddListener((val) => ValidateUI());
        if (dropdownRed) dropdownRed.onValueChanged.AddListener((val) => ValidateUI());
        if (dropdownGreen) dropdownGreen.onValueChanged.AddListener((val) => ValidateUI());
        if (dropdownBlue) dropdownBlue.onValueChanged.AddListener((val) => ValidateUI());

        ResetUI();
    }

    void SetupDropdown(TMP_Dropdown dropdown, int defaultIndex)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(bandOptions);
        if (defaultIndex < bandOptions.Count) dropdown.value = defaultIndex;
    }

    void ResetUI()
    {
        currentInputPath = "";
        if (textInputFilename) textInputFilename.text = "Belum ada file dipilih";
        if (inputOutputName) inputOutputName.text = "";
        if (textStatus) textStatus.text = "Siap.";
        ValidateUI();
    }

    public void TogglePanel()
    {
        if (panelRoot) panelRoot.SetActive(!panelRoot.activeSelf);
    }

    public void OnClickSelectInput()
    {
        // Gunakan FileBrowserHelper yang sama dengan RasterTransform
        string path = FileBrowserHelper.OpenFile("Pilih Input Imagery (TIFF)", "TIFF Files\0*.tif;*.tiff\0All Files\0*.*\0\0");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            currentInputPath = path;
            if (textInputFilename) textInputFilename.text = Path.GetFileName(path);
            
            // Auto-fill output name suggestion
            if (inputOutputName && string.IsNullOrEmpty(inputOutputName.text))
            {
                inputOutputName.text = "_CompositeRGB";
            }

            ValidateUI();
        }
    }

    void ValidateUI()
    {
        if (btnProcess == null) return;

        bool isFileReady = !string.IsNullOrEmpty(currentInputPath);
        bool isNameReady = inputOutputName != null && !string.IsNullOrEmpty(inputOutputName.text);
        
        // Opsional: Validasi band tidak boleh sama (walaupun script python handle errornya, tapi UI lebih baik mencegah)
        // Tapi kadang user mau R=B1, G=B1, B=B1 (Grayscale), jadi kita biarkan saja.
        
        btnProcess.interactable = isFileReady && isNameReady;
    }

    public async void OnClickProcess()
    {
        if (string.IsNullOrEmpty(currentInputPath)) return;

        btnProcess.interactable = false;
        if (textStatus)
        {
            textStatus.text = "Memproses Komposit...";
            textStatus.color = Color.yellow;
        }

        // Ambil value band (Dropdown index + 1 karena dropdown mulai dari 0 tapi band mulai dari 1)
        int r = dropdownRed.value + 1;
        int g = dropdownGreen.value + 1;
        int b = dropdownBlue.value + 1;

        string suffix = inputOutputName.text;
        string timestamp = System.DateTime.Now.ToString("HHmmss");
        string originalName = Path.GetFileNameWithoutExtension(currentInputPath);
        
        // Nama Output Final
        string outputFilename = $"{originalName}{suffix}_{timestamp}.tif";
        string outputFullPath = Path.Combine(outputFolder, outputFilename);

        // Siapkan Command
        string result = await Task.Run(() => RunPythonProcess(r, g, b, outputFullPath));

        // Handle Result
        if (result == "SUCCESS")
        {
            if (textStatus)
            {
                textStatus.text = "Selesai!";
                textStatus.color = Color.green;
            }
            UnityEngine.Debug.Log($"[Composite] Output saved to: {outputFullPath}");
            
            // [UPDATED] Tidak lagi membuka explorer
            // string argument = "/select, \"" + outputFullPath.Replace("/", "\\") + "\"";
            // Process.Start("explorer.exe", argument);

            // [NEW] Load ke Grid Project
            AddToProject(outputFullPath);
        }
        else
        {
            if (textStatus)
            {
                textStatus.text = "Gagal. Cek Console.";
                textStatus.color = Color.red;
            }
            UnityEngine.Debug.LogError($"[Composite] Error: {result}");
        }

        btnProcess.interactable = true;
    }

    private string RunPythonProcess(int r, int g, int b, string outputPath)
    {
        string fullExePath;
        string args;
        string cleanInput = currentInputPath.Replace("\\", "/");
        string cleanOutput = outputPath.Replace("\\", "/");

        // Deteksi Mode: Editor (Python Script) vs Build (Exe)
// #if UNITY_EDITOR
        // if (!useExeInEditor)
        // {
             // fullExePath = Path.Combine(backendFolder, exeName);
             // args = $"--input \"{cleanInput}\" --r {r} --g {g} --b {b} --output \"{cleanOutput}\" --stretch";
             // args: --input "..." --r 4 --g 3 --b 2 --output "..." --stretch
             // args = $"\"{scriptPath}\" --input \"{cleanInput}\" --r {r} --g {g} --b {b} --output \"{cleanOutput}\" --stretch";
        // }
        // else
        // {
        //    fullExePath = Path.Combine(backendFolder, exeName);
        //    args = $"--input \"{cleanInput}\" --r {r} --g {g} --b {b} --output \"{cleanOutput}\" --stretch";
        // }
// #else
        // BUILD MODE & Editor (Default to EXE)
        fullExePath = Path.Combine(backendFolder, exeName);
        
        // [FIX] Double check if exe exists
        if (!File.Exists(fullExePath))
        {
             // Debugging Path issues
             UnityEngine.Debug.LogError($"[Composite] Exe NOT FOUND at: {fullExePath}");
             UnityEngine.Debug.LogError($"[Composite] StreamingAssetsPath: {Application.streamingAssetsPath}");
             UnityEngine.Debug.LogError($"[Composite] Backend Folder: {backendFolder}");
             
             return $"EXECUTABLE NOT FOUND: {fullExePath}";
        }

        args = $"--input \"{cleanInput}\" --r {r} --g {g} --b {b} --output \"{cleanOutput}\" --stretch";
// #endif

        UnityEngine.Debug.Log($"[Composite] Running: {fullExePath} {args}");

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = fullExePath;
        startInfo.Arguments = args;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        startInfo.WorkingDirectory = backendFolder;

        // [Fix] Paksa Python menggunakan UTF-8 untuk menghindari error charmap di Windows
        // if (!fullExePath.EndsWith(".exe")) {
        //      startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        //      startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
        // }
        
        // [Fix] Pastikan C# membaca output stream sebagai UTF-8
        // startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
        // startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

        try
        {
            Process p = Process.Start(startInfo);
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            p.WaitForExit();

            // Log everything for debugging
            UnityEngine.Debug.Log($"[Composite] StdOut: {output}");
            if (!string.IsNullOrEmpty(error)) UnityEngine.Debug.LogError($"[Composite] StdErr: {error}");

            if (p.ExitCode == 0)
            {
                return "SUCCESS";
            }
            else
            {
                return error + "\n" + output;
            }
        }
        catch (System.Exception e)
        {
            return "SYSTEM ERROR: " + e.Message;
        }
    }

    // [NEW] Helper untuk load ke ProjectManager
    void AddToProject(string tiffPath)
    {
        // Cari ProjectManager & LayerManager
        ProjectManager pm = FindFirstObjectByType<ProjectManager>();
        TiffLayerManager lm = FindFirstObjectByType<TiffLayerManager>();

        if (lm != null && pm != null)
        {
            // [Check Preview PNG]
            // Format preview yang dihasilkan python: {OriginalName}_preview.png
            // Tapi tiffPath kita adalah outputFullPath (yang ada timestamp)
            // Di python: base, _ = os.path.splitext(output_tif_path) -> preview_path = f"{base}_preview.png"
            string previewPng = Path.ChangeExtension(tiffPath, null) + "_preview.png";
            string fileToLoad = tiffPath;
            bool usePng = false;

            if (File.Exists(previewPng))
            {
                fileToLoad = previewPng;
                usePng = true;
                UnityEngine.Debug.Log($"[Composite] Found preview PNG: {previewPng}");
            }

            // Ambil info bounds & center dari file TIFF (selalu pakai TIFF untuk georeference)
            if (lm.GetTiffBounds(tiffPath, out double minLat, out double maxLat, out double minLon, out double maxLon))
            {
                double centerLat = (minLat + maxLat) / 2.0;
                double centerLon = (minLon + maxLon) / 2.0;
                int zoom = lm.CalculateFitZoom();

                List<Vector2> polyCoords = new List<Vector2>
                {
                    new Vector2((float)maxLat, (float)minLon),
                    new Vector2((float)maxLat, (float)maxLon),
                    new Vector2((float)minLat, (float)maxLon),
                    new Vector2((float)minLat, (float)minLon)
                };

                string projectName = Path.GetFileNameWithoutExtension(tiffPath);
                
                // Tambahkan sebagai project baru di grid
                // Param ke-5 (tiffPath) diganti fileToLoad (bisa PNG atau TIFF)
                // [FIX] Jangan load overlay terpisah jika fileToLoad sudah PNG
                // Fungsi CreateProjectAuto akan mengurus load layer via ProjectItem.
                pm.CreateProjectAuto(projectName, centerLat, centerLon, zoom, fileToLoad, polyCoords);
                
                UnityEngine.Debug.Log($"[Composite] Added to project grid: {projectName} (Source: {Path.GetFileName(fileToLoad)})");
                
                // [REMOVED] Jangan panggil LoadPngOverlay lagi karena CreateProjectAuto sudah akan mentrigger load.
                // if (usePng)
                // {
                //    lm.LoadPngOverlay(previewPng, minLat, maxLat, minLon, maxLon);
                // }
            }
            else
            {
                UnityEngine.Debug.LogWarning("[Composite] Failed to get TIFF bounds. Cannot add to grid.");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("[Composite] ProjectManager or TiffLayerManager not found.");
        }
    }
}
