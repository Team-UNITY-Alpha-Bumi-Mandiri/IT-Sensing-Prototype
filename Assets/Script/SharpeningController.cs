using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

public class SharpeningController : MonoBehaviour
{
    [Header("UI References (Panel)")]
    public GameObject panelRoot; // Referensi ke Panel_Sharpening untuk Show/Hide

    [Header("UI Input References")]
    public TMP_InputField inputPrefixName; // Input Nama (misal: LC09...)
    public TMP_Dropdown dropdownAlgo;      // Pilihan Algoritma (Wavelet/Gram-Schmidt)

    [Header("UI Folder References")]
    public TextMeshProUGUI textInputPath;  // Teks path Input
    public TextMeshProUGUI textOutputPath; // Teks path Output

    [Header("Buttons & Status")]
    public Button btnProcess;
    public TextMeshProUGUI textStatus;

    [Header("Layer Manager")]
    public TiffLayerManager layerManager; // Referensi ke TiffLayerManager
    public ProjectManager projectManager; // Referensi ke ProjectManager

    // Variabel privat penyimpan path
    private string selectedInputFolder = "";
    private string selectedOutputFolder = "";
    private string lastOutputTiffPath = ""; // Path TIFF hasil terakhir

    void Start()
    {
        // 1. Sembunyikan panel saat aplikasi mulai
        if (panelRoot != null)
            panelRoot.SetActive(false);

        // 2. Reset Status UI
        textStatus.text = "Siap.";
        btnProcess.interactable = false;
        textInputPath.text = "Belum dipilih...";
        textOutputPath.text = "Belum dipilih...";

        // 3. Pasang pendengar jika user mengetik nama
        inputPrefixName.onValueChanged.AddListener(delegate { CheckReadiness(); });
    }

    // ========================================================================
    // 1. FITUR SHOW / HIDE PANEL
    // ========================================================================
    public void TogglePanel()
    {
        if (panelRoot != null)
        {
            bool isActive = panelRoot.activeSelf;
            panelRoot.SetActive(!isActive); // Balikkan status (Nyala <-> Mati)
        }
    }

    // ========================================================================
    // 2. PEMILIHAN FOLDER (Input & Output)
    // ========================================================================
    
    // Pilih Folder Input (Sumber RGB/PAN)
    public void OnClickBrowseInput()
    {
        // Panggil script Folder Browser yang baru
        string path = FolderBrowserHelper.GetFolder(); 
        
        if (!string.IsNullOrEmpty(path))
        {
            selectedInputFolder = path;
            textInputPath.text = LimitTextLength(selectedInputFolder);
            CheckReadiness();
        }
    }

    // Pilih Folder Output (Tujuan Simpan)
    public void OnClickBrowseOutput()
    {
        // Panggil script Folder Browser yang baru
        string path = FolderBrowserHelper.GetFolder();

        if (!string.IsNullOrEmpty(path))
        {
            selectedOutputFolder = path;
            textOutputPath.text = LimitTextLength(selectedOutputFolder);
            CheckReadiness();
        }
    }

    // ========================================================================
    // 3. VALIDASI & EKSEKUSI
    // ========================================================================

    // Cek apakah semua data wajib sudah terisi?
    void CheckReadiness()
    {
        bool isNameFilled = !string.IsNullOrEmpty(inputPrefixName.text);
        bool isInputReady = !string.IsNullOrEmpty(selectedInputFolder);
        bool isOutputReady = !string.IsNullOrEmpty(selectedOutputFolder);

        // Tombol Process hanya nyala jika semua syarat terpenuhi
        btnProcess.interactable = isNameFilled && isInputReady && isOutputReady;
    }

    // Tombol "Mulai Proses" diklik
    public void OnClickProcess()
    {
        string prefix = inputPrefixName.text;
        string algo = dropdownAlgo.options[dropdownAlgo.value].text; // Ambil teks dari dropdown

        RunBackend(algo, prefix, selectedInputFolder, selectedOutputFolder);
    }

    // ========================================================================
    // 4. LOGIKA BACKEND (ASYNC)
    // ========================================================================
    async void RunBackend(string algorithm, string prefixName, string inputFolder, string outputFolder)
    {
        btnProcess.interactable = false;
        textStatus.text = $"Memproses {algorithm}...";
        textStatus.color = Color.yellow;

        string backendFolder = Path.Combine(Application.streamingAssetsPath, "Backend");
        // Tentukan nama EXE sesuai pilihan
        string exeName = algorithm.Contains("Wavelet") ? "wavelet_direct.exe" : "gram_direct.exe";
        string fullExePath = Path.Combine(backendFolder, exeName);

        // --- [FIX UTAMA] SANITASI PATH ---
        // 1. Ganti Backslash (\) jadi Slash (/) agar aman dari error escaping
        // 2. Hapus slash di akhir string (.TrimEnd)
        inputFolder = inputFolder.Replace("\\", "/").TrimEnd('/');
        outputFolder = outputFolder.Replace("\\", "/").TrimEnd('/');
        
        // Pastikan nama file (Prefix) tidak mengandung ekstensi .tif atau .jpg
        // Python script Anda mengharapkan PREFIX saja (misal: LC09...), dia akan nambah _RGB.tif sendiri
        if (prefixName.ToLower().EndsWith(".tif"))
        {
            prefixName = Path.GetFileNameWithoutExtension(prefixName);
            // Hapus suffix _RGB atau _PAN jika user tidak sengaja memasukkannya
            prefixName = prefixName.Replace("_RGB", "").Replace("_PAN", "");
        }

        // Format Argumen sesuai dokumentasi Python Anda:
        // -n NAMA -v FOLDER_INPUT -o FOLDER_OUTPUT
        string args = $"-n \"{prefixName}\" -v \"{inputFolder}\" -o \"{outputFolder}\"";

        UnityEngine.Debug.Log($"Command: {fullExePath} {args}");

        string resultOutput = await Task.Run(() =>
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = fullExePath;
            start.Arguments = args;
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true; // Wajib baca Error
            start.CreateNoWindow = true;
            start.WorkingDirectory = backendFolder;

            try
            {
                Process p = Process.Start(start);
                
                // Baca output dan error sekaligus
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                
                p.WaitForExit();

                // Prioritaskan pesan error jika ada isinya
                if (!string.IsNullOrEmpty(error))
                {
                    return "PYTHON ERROR: " + error;
                }

                return output;
            }
            catch (System.Exception e)
            {
                return "SYSTEM ERROR: " + e.Message;
            }
        });

        UnityEngine.Debug.Log("Result: " + resultOutput);

        // Cek JSON Response dari Python
        if (resultOutput.Contains("\"status\": \"success\"") || resultOutput.Contains("successfuly"))
        {
            textStatus.text = "Sukses! Tersimpan.";
            textStatus.color = Color.green;
            
            // Cari file TIFF hasil di folder output
            LoadResultTiff(outputFolder, prefixName, algorithm);
            
            // Buka folder output (Ganti slash balik ke backslash untuk Explorer)
            if (Directory.Exists(outputFolder)) 
                Process.Start("explorer.exe", outputFolder.Replace("/", "\\"));
        }
        else
        {
            // Tampilkan error di UI (potong biar muat)
            string msg = resultOutput.Length > 50 ? resultOutput.Substring(0, 50) + "..." : resultOutput;
            textStatus.text = "Gagal: " + msg;
            textStatus.color = Color.red;
            UnityEngine.Debug.LogError("FULL ERROR: " + resultOutput);
        }

        btnProcess.interactable = true;
    }

    // Helper: Memendekkan teks path jika terlalu panjang di UI
    string LimitTextLength(string text)
    {
        if (text.Length > 35)
            return "..." + text.Substring(text.Length - 35);
        return text;
    }

    // ========================================================================
    // 5. LOAD TIFF HASIL KE LAYER MANAGER
    // ========================================================================
    void LoadResultTiff(string outputFolder, string prefixName, string algorithm)
    {
        if (layerManager == null)
        {
            UnityEngine.Debug.LogWarning("[SharpeningController] LayerManager tidak terhubung");
            return;
        }

        // Format nama file: {prefix}_{algorithm}_direct_{timestamp}.tif
        // Contoh: LC09_116061_20250112_gramschmidt_direct_251229160920.tif
        string algoShort = algorithm.ToLower().Contains("wavelet") ? "wavelet" : "gramschmidt";
        string pattern = $"{prefixName}_{algoShort}_direct_*.tif";

        // Cari file dengan pattern tersebut
        string[] files = Directory.GetFiles(outputFolder.Replace("/", "\\"), pattern);

        if (files.Length > 0)
        {
            // Ambil file terbaru (jika ada beberapa)
            string latestFile = files[files.Length - 1];
            lastOutputTiffPath = latestFile;

            UnityEngine.Debug.Log($"[SharpeningController] Finding bounds for TIFF: {latestFile}");
            
            // 1. Dapatkan info center & zoom dari TIFF tanpa load full texture dulu
            if (layerManager.GetTiffCenter(latestFile, out double lat, out double lon))
            {
                int zoom = layerManager.CalculateFitZoom();
                
                // 2. Buat Project Baru secara otomatis
                string projectName = $"{prefixName}_{algoShort}";
                if (projectManager != null)
                {
                    UnityEngine.Debug.Log($"[SharpeningController] Auto Creating Project: {projectName}");
                    projectManager.CreateProjectAuto(projectName, lat, lon, zoom, latestFile);
                }
                else
                {
                     // Fallback jika tidak ada ProjectManager
                     layerManager.LoadTiff(latestFile);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("[SharpeningController] Gagal membaca koordinat GeoTIFF. Loading manual...");
                layerManager.LoadTiff(latestFile);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[SharpeningController] Tidak menemukan file dengan pattern: {pattern}");
        }
    }
}