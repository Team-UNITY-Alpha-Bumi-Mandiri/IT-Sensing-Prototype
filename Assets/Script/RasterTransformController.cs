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
        
        // Format: python rasterTransform.py -n <NamaOutput> --algo <Algoritma> --input <FileTiff>
        // Note: Script python akan otomatis menyimpan ke folder output defaultnya (biasanya relatif terhadap script)
        // Kita asumsikan script menyimpan di folder yang sama atau kita biarkan script menangani output pathnya.
        // Berdasarkan README, argumennya hanya nama output (-n), bukan path full.
        
        string args = $"-n \"{outputName}\" --algo {algo} --input \"{cleanInput}\"";

        UnityEngine.Debug.Log($"[RasterTransform] Running: {exeName} {args}");

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
                layerManager.LoadPngOverlay(fullPngPath, bounds.north, bounds.south, bounds.west, bounds.east);
                
                // Buka folder
                string folder = Path.GetDirectoryName(fullPngPath);
                Process.Start("explorer.exe", folder);
                
                // Register ke Project Manager (menggunakan nama PNG)
                if (projectManager != null)
                {
                    string layerName = Path.GetFileNameWithoutExtension(fullPngPath);
                    projectManager.AddProperty(layerName, true);
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
        layerManager.LoadTiff(filePath);

        // Daftarkan ke Project Manager (optional, agar tersimpan di sesi)
        if (projectManager != null)
        {
            string layerName = Path.GetFileNameWithoutExtension(filePath);
            projectManager.AddProperty(layerName, true);
        }

        // Buka folder di explorer agar user tau
        string tiffFolder = Path.GetDirectoryName(filePath);
        Process.Start("explorer.exe", tiffFolder);
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
