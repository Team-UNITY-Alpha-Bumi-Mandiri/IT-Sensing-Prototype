using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

public class RasterTransformController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public Button btnSelectInput;
    public TMP_InputField inputOutputName;
    public TMP_Dropdown dropdownAlgorithm;
    public Button btnSubmit;
    public TextMeshProUGUI textStatus;

    [Header("Dependencies")]
    public TiffLayerManager layerManager;
    public ProjectManager projectManager;

    private string currentInputPath = "";
    private string backendFolder;
    private string outputBaseFolder;
    private string exeName = "rasterTransform.exe";

    // List algoritma sesuai gambar/request
    private readonly List<string> algorithmList = new List<string>
    {
        "NDTI", "NDVI", "NDWI", "NDBI", "NGRDI", 
        "RVI", "SAVI", "EVI", "GNDVI", "ARVI", 
        "MSAVI", "TVI", "CLGREEN"
    };

    void Start()
    {
        // 1. Setup Folder
        backendFolder = Path.Combine(Application.streamingAssetsPath, "Backend");
        outputBaseFolder = Path.Combine(backendFolder, "Sharpened_Results"); // Simpan hasil di folder yang sama agar rapi
        
        if (!Directory.Exists(outputBaseFolder)) Directory.CreateDirectory(outputBaseFolder);

        // 2. Setup UI Listeners
        if (btnSelectInput != null) btnSelectInput.onClick.AddListener(OnClickSelectInput);
        if (btnSubmit != null) btnSubmit.onClick.AddListener(OnClickSubmit);
        if (inputOutputName != null) inputOutputName.onValueChanged.AddListener((val) => CheckReadiness());

        // 3. Populate Dropdown
        if (dropdownAlgorithm != null)
        {
            dropdownAlgorithm.ClearOptions();
            dropdownAlgorithm.AddOptions(algorithmList);
            dropdownAlgorithm.onValueChanged.AddListener((val) => CheckReadiness());
        }

        // 4. Initial State
        ResetUI();
    }

    private void ResetUI()
    {
        currentInputPath = "";
        if (inputOutputName) inputOutputName.text = "";
        if (textStatus) textStatus.text = "Siap.";
        if (btnSelectInput) SetButtonText(btnSelectInput.gameObject, "Pilih Input Imagery...");
        CheckReadiness();
    }

    public void TogglePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(!panelRoot.activeSelf);
    }

    public void OnClickSelectInput()
    {
        string path = FileBrowserHelper.OpenFile("Pilih Input Imagery (TIFF)", "TIFF Files\0*.tif;*.tiff\0All Files\0*.*\0\0");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            currentInputPath = path;
            SetButtonText(btnSelectInput.gameObject, Path.GetFileName(path));
            
            // Auto-fill output name suggestion
            if (inputOutputName != null && string.IsNullOrEmpty(inputOutputName.text))
            {
                string algo = dropdownAlgorithm.options[dropdownAlgorithm.value].text;
                inputOutputName.text = $"_{algo}";
            }
            
            CheckReadiness();
        }
    }

    private void CheckReadiness()
    {
        if (btnSubmit == null) return;
        
        bool isInputReady = !string.IsNullOrEmpty(currentInputPath);
        bool isNameReady = inputOutputName != null && !string.IsNullOrEmpty(inputOutputName.text);
        
        btnSubmit.interactable = isInputReady && isNameReady;
    }

    public void OnClickSubmit()
    {
        if (string.IsNullOrEmpty(currentInputPath)) return;

        string algo = dropdownAlgorithm.options[dropdownAlgorithm.value].text;
        string userSuffix = inputOutputName.text;
        
        // Buat nama unik: {OriginalName}_{Suffix}_{Timestamp}
        string originalName = Path.GetFileNameWithoutExtension(currentInputPath);
        string timestamp = System.DateTime.Now.ToString("yyMMddHHmmss");
        string outputName = $"{originalName}{userSuffix}_{timestamp}"; // suffix biasanya sdh ada underscore kalau user ngikutin pattern

        RunBackend(outputName, algo, currentInputPath);
    }

    private async void RunBackend(string outputName, string algo, string inputPath)
    {
        btnSubmit.interactable = false;
        if (textStatus)
        {
            textStatus.text = $"Processing {algo}...";
            textStatus.color = Color.yellow;
        }

        string fullExePath = Path.Combine(backendFolder, exeName);
        string cleanInput = inputPath.Replace("\\", "/");
        
        // --- LOGIC DETEKSI PYTHON ENVIRONMENT (Copied from CompositeManager) ---
        // Cek apakah ada virtual environment di project root (.venv)
        // string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        // string venvPython = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");

        // if (File.Exists(venvPython))
        // {
        //     fullExePath = venvPython;
        //     UnityEngine.Debug.Log($"[RasterTransform] Using venv python: {fullExePath}");
        // }
        // else
        // {
        //     fullExePath = "python"; // Asumsi python ada di PATH global
        // }

        // Arguments: "script_path" -n "name" --algo algo --input "input"
        // args = $"\"{scriptPath}\" -n \"{outputName}\" --algo {algo} --input \"{cleanInput}\"";
        string args = $"-n \"{outputName}\" --algo {algo} --input \"{cleanInput}\"";

        UnityEngine.Debug.Log($"[RasterTransform] Running: {fullExePath} {args}");

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

            // Environment Variables for Python UTF-8
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

            try
            {
                Process p = Process.Start(startInfo);
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                UnityEngine.Debug.Log($"[RasterTransform] StdOut: {output}");
                if (!string.IsNullOrEmpty(error)) UnityEngine.Debug.LogError($"[RasterTransform] StdErr: {error}");

                if (!string.IsNullOrEmpty(error) && !error.Contains("UserWarning"))
                     return "ERROR: " + error + "\nOutput: " + output;
                
                return output;
            }
            catch (System.Exception e)
            {
                return "SYSTEM ERROR: " + e.Message;
            }
        });

        ProcessResult(result, outputName);
        btnSubmit.interactable = true;
    }

    private void ProcessResult(string output, string outputName)
    {
        UnityEngine.Debug.Log($"[RasterTransform] Output: {output}");

        // Parsing JSON manual karena output bisa terdiri dari beberapa baris JSON
        // Kita cari baris yang mengandung "status": "success"
        
        bool isSuccess = false;
        string resultPath = "";
        RasterResponse successResponse = null;

        using (StringReader reader = new StringReader(output))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Cek status success (case-insensitive)
                if (line.Contains("\"status\": \"success\""))
                {
                    isSuccess = true;
                    try
                    {
                        // Parsing JSON
                        RasterResponse response = JsonUtility.FromJson<RasterResponse>(line);
                        if (response != null)
                        {
                            successResponse = response;
                            if (!string.IsNullOrEmpty(response.path))
                            {
                                resultPath = response.path;
                            }
                        }
                    }
                    catch { } // Ignore json error
                }
            }
        }

        if (isSuccess)
        {
            if (textStatus)
            {
                textStatus.text = "Selesai!";
                textStatus.color = Color.green;
            }

            // Jika path dari JSON valid
            if (!string.IsNullOrEmpty(resultPath))
            {
                // Path dari backend mungkin relatif (misal "TRANSFORM\...") atau absolute
                string fullPath = resultPath;
                if (!Path.IsPathRooted(fullPath))
                {
                    fullPath = Path.Combine(backendFolder, resultPath);
                }

                if (File.Exists(fullPath))
                {
                    string preview = successResponse != null ? successResponse.preview_png : null;
                    RasterBounds bounds = successResponse != null ? successResponse.bounds : null;
                    LoadToMap(fullPath, preview, bounds);
                    return;
                }
            }

            // Fallback: Cari manual jika path dari JSON tidak ketemu
            string[] files = Directory.GetFiles(backendFolder, $"{outputName}*.tif", SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                LoadToMap(files[0]);
            }
            else
            {
                UnityEngine.Debug.LogWarning("File hasil tidak ditemukan otomatis.");
            }
        }
        else
        {
            if (textStatus)
            {
                textStatus.text = "Gagal (Cek Console)";
                textStatus.color = Color.red;
            }
            UnityEngine.Debug.LogError($"[RasterTransform] Failed: {output}");
        }
    }

    [System.Serializable]
    private class RasterResponse
    {
        public string status;
        public string messages;
        public string filename;
        public string path;
        public string preview_png;
        public RasterBounds bounds;
    }

    [System.Serializable]
    private class RasterBounds
    {
        public double north;
        public double south;
        public double west;
        public double east;
    }

    private void LoadToMap(string filePath, string pngPath = null, RasterBounds bounds = null)
    {
        if (layerManager == null) return;

        UnityEngine.Debug.Log($"[RasterTransformController] Deciding what to load...");
        UnityEngine.Debug.Log($"[RasterTransformController] Input TIFF: {filePath}");
        UnityEngine.Debug.Log($"[RasterTransformController] Input PNG: {pngPath}");
        UnityEngine.Debug.Log($"[RasterTransformController] Has Bounds: {(bounds != null)}");

        string layerName; // Declare here to avoid scope conflict

        // PRIORITAS 1: Load PNG jika ada (karena sudah diwarnai/visualized)
        if (!string.IsNullOrEmpty(pngPath) && bounds != null)
        {
            // Cek path PNG (bisa relative atau absolute)
            string fullPngPath = pngPath;
            bool pngExists = false;

            // 1. Cek Absolute Path langsung (jika JSON return full path)
            if (Path.IsPathRooted(pngPath))
            {
                fullPngPath = pngPath;
                pngExists = File.Exists(fullPngPath);
            }
            
            // 2. Cek Relative terhadap folder Backend (Standard)
            if (!pngExists)
            {
                string tryPath = Path.Combine(backendFolder, pngPath);
                if (File.Exists(tryPath))
                {
                    fullPngPath = tryPath;
                    pngExists = true;
                }
            }

            // 3. Cek Relative terhadap folder TRANSFORM (Python script structure)
            if (!pngExists)
            {
                // JSON Python mungkin return nama file saja "foo.png"
                // Tapi file sebenarnya ada di "Backend/TRANSFORM/foo/foo.png"
                // Atau "Backend/TRANSFORM/foo.png"
                
                // Coba cari di folder TRANSFORM/NamaOutput
                string outputFolderName = Path.GetFileNameWithoutExtension(pngPath).Replace("_preview", ""); 
                // Asumsi nama folder = nama file tanpa _preview (berdasarkan script python)
                // Python: self.output_folder_name = os.path.join(self.base_folder, self.prefix_name)
                // Tapi nama file PNG panjang: {prefix}_{algo}_{time}_preview.png
                // Folder output python: TRANSFORM/{prefix}
                
                // Cara paling aman: Cari recursive file dengan nama tersebut di folder Backend
                string[] foundFiles = Directory.GetFiles(backendFolder, Path.GetFileName(pngPath), SearchOption.AllDirectories);
                if (foundFiles.Length > 0)
                {
                    fullPngPath = foundFiles[0];
                    pngExists = true;
                    UnityEngine.Debug.Log($"[RasterTransformController] Found PNG via Search: {fullPngPath}");
                }
            }

            UnityEngine.Debug.Log($"[RasterTransformController] Check PNG Path: {fullPngPath} -> Exists: {pngExists}");

            if (pngExists)
            {
                UnityEngine.Debug.Log($"[RasterTransformController] DECISION: LOADING PNG PREVIEW");
                
                // Tentukan nama layer dari file asli (TIFF) agar bersih, atau fallback ke PNG jika perlu
                layerName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrEmpty(layerName)) layerName = Path.GetFileNameWithoutExtension(fullPngPath);

                // Bersihkan timestamp jika ada format _custom_YYYYMMDDHHMMSS
                if (layerName.Contains("_custom_"))
                {
                    int idx = layerName.IndexOf("_custom_");
                    if (idx > 0) layerName = layerName.Substring(0, idx);
                }
                else if (layerName.Contains("_transformed_")) // Format transform manager (NDWI dll)
                {
                     int idx = layerName.IndexOf("_transformed_");
                     if (idx > 0) layerName = layerName.Substring(0, idx);
                }

                UnityEngine.Debug.Log($"[RasterTransformController] Layer Name: '{layerName}'");

                // Gunakan overload baru LoadPngOverlay untuk menyimpan referensi ke TIFF asli (filePath)
                // PENTING: Pass layerName sebagai customLayerName agar konsisten antara LayerManager dan ProjectManager
                layerManager.LoadPngOverlay(fullPngPath, bounds.north, bounds.south, bounds.west, bounds.east, false, false, layerName, filePath);
                
                // Buka folder
                // string folder = Path.GetDirectoryName(fullPngPath);
                // Process.Start("explorer.exe", folder);
                
                // Register ke Project Manager
                if (projectManager != null)
                {
                    if (projectManager.GetCurrentProject() != null)
                    {
                        // Jika ada project aktif, tambahkan sebagai layer baru
                        projectManager.AddProperty(layerName, true, false);
                        
                        // Pastikan layer aktif (karena AddProperty skip jika sudah ada via SyncWithProject)
                        projectManager.OnPropertyChanged(layerName, true);
                        
                        UnityEngine.Debug.Log($"[RasterTransformController] Added layer '{layerName}' to active project.");
                    }
                    else
                    {
                        // Jika tidak ada project, buat baru
                        // Gunakan CreateProjectAuto agar masuk grid dengan benar
                        double centerLat = (bounds.north + bounds.south) / 2.0;
                        double centerLon = (bounds.west + bounds.east) / 2.0;
                        int zoom = layerManager.CalculateFitZoom();
                        List<Vector2> polyCoords = new List<Vector2>
                        {
                            new Vector2((float)bounds.north, (float)bounds.west),
                            new Vector2((float)bounds.north, (float)bounds.east),
                            new Vector2((float)bounds.south, (float)bounds.east),
                            new Vector2((float)bounds.south, (float)bounds.west)
                        };
                        // Gunakan layerName untuk nama project juga
                        projectManager.CreateProjectAuto(layerName, centerLat, centerLon, zoom, fullPngPath, polyCoords);
                    }
                }
                return;
            }
            else
            {
                 UnityEngine.Debug.LogWarning($"[RasterTransformController] PNG path provided but file not found! Falling back to TIFF.");
            }
        }
        else
        {
            UnityEngine.Debug.Log($"[RasterTransformController] PNG info missing (Path: {pngPath}, Bounds: {bounds}). Falling back to TIFF.");
        }

        // PRIORITAS 2: Load TIFF (Fallback)
        UnityEngine.Debug.Log($"[RasterTransformController] DECISION: LOADING RAW TIFF (FALLBACK)");
        
        bool hasActiveProject = projectManager != null && projectManager.GetCurrentProject() != null;
        
        // Tentukan nama layer dari file asli (TIFF) agar bersih
        layerName = Path.GetFileNameWithoutExtension(filePath);
        // Bersihkan timestamp jika ada format _custom_YYYYMMDDHHMMSS
        if (layerName.Contains("_custom_"))
        {
            int idx = layerName.IndexOf("_custom_");
            if (idx > 0) layerName = layerName.Substring(0, idx);
        }
        else if (layerName.Contains("_transformed_")) // Format transform manager (NDWI dll)
        {
                int idx = layerName.IndexOf("_transformed_");
                if (idx > 0) layerName = layerName.Substring(0, idx);
        }

        // Load TIFF (jangan clear existing jika kita menambahkan ke project aktif)
        layerManager.LoadTiff(filePath, !hasActiveProject, layerName);

        // Daftarkan ke Project Manager (optional, agar tersimpan di sesi)
        if (projectManager != null)
        {
            // Jika ada project aktif, tambahkan sebagai layer baru
            if (hasActiveProject)
            {
                projectManager.AddProperty(layerName, true, false);
                projectManager.OnPropertyChanged(layerName, true);
                UnityEngine.Debug.Log($"[RasterTransformController] Added layer '{layerName}' to active project.");
            }
            else
            {
                // Jika tidak ada project, buat baru
                // Gunakan CreateProjectAuto agar masuk grid dengan benar
                // Kita perlu bounds untuk ini. Jika bounds kosong (karena PNG gagal), kita perlu baca dari TIFF via TiffLayerManager
                if (layerManager.GetTiffBounds(filePath, out double minLat, out double maxLat, out double minLon, out double maxLon))
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
                    projectManager.CreateProjectAuto(layerName, centerLat, centerLon, zoom, filePath, polyCoords);
                }
            }
        }

        // Buka folder di explorer agar user tau
        // string tiffFolder = Path.GetDirectoryName(filePath);
        // Process.Start("explorer.exe", tiffFolder);
    }

    private void SetButtonText(GameObject btnObj, string newText)
    {
        if (btnObj == null) return;
        TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = newText;
        else
        {
            Text txt = btnObj.GetComponentInChildren<Text>();
            if (txt != null) txt.text = newText;
        }
    }
}
