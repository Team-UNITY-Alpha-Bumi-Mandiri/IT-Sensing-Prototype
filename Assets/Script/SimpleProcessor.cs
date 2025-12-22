using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Diagnostics;
using TMPro;

public class SimpleProcessor : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelPopUp;       // Panel Pop-up utama
    public TextMeshProUGUI pathDisplayText; // Teks untuk menampilkan nama file
    public TextMeshProUGUI statusText;  // Teks status (Waiting/Success)
    public Button btnSelectFile;        // Tombol Browse
    public Button btnProcess;           // Tombol Proses
    public Button btnOpenResult;        // Tombol Buka Folder Hasil

    // Variabel privat
    private string selectedFilePath = "";

    void Start()
    {
        // Setup kondisi awal UI
        panelPopUp.SetActive(false); // Sembunyikan panel saat mulai
        
        btnOpenResult.interactable = false; // Matikan tombol hasil
        btnProcess.interactable = false;    // Matikan tombol proses sebelum pilih file
        
        statusText.text = "Ready";
        pathDisplayText.text = "No file selected";
    }

    // --- 1. FUNGSI MEMBUKA POP-UP ---
    public void TogglePanel()
    {
        panelPopUp.SetActive(!panelPopUp.activeSelf);
    }

    // --- 2. FUNGSI TOMBOL 'BROWSE IMAGE' ---
    public void OnClickSelectFile()
    {
        // Membuka Windows Explorer
        string path = FileBrowserHelper.OpenFile("Select Drone Image", "Image Files\0*.jpg;*.jpeg;*.png\0\0");

        if (!string.IsNullOrEmpty(path))
        {
            selectedFilePath = path;
            
            // Tampilkan hanya nama filenya saja agar rapi
            pathDisplayText.text = Path.GetFileName(path); 
            
            statusText.text = "File Selected";
            statusText.color = Color.white;
            
            btnProcess.interactable = true; // Nyalakan tombol proses
        }
    }

    // --- 3. FUNGSI TOMBOL 'PROCESS' ---
    public void OnClickProcess()
    {
        if (string.IsNullOrEmpty(selectedFilePath) || !File.Exists(selectedFilePath))
        {
            statusText.text = "File Not Found!";
            statusText.color = Color.red;
            return;
        }

        // Kunci UI saat memproses
        statusText.text = "Processing...";
        statusText.color = Color.yellow;
        btnProcess.interactable = false;
        btnSelectFile.interactable = false;

        // Jalankan Backend
        RunBackend(selectedFilePath);
    }

    // --- 4. FUNGSI TOMBOL 'OPEN RESULT' (Ke Folder Asli) ---
    public void OnClickOpenResult()
    {
        // Cari path: Assets/StreamingAssets/Backend/result1
        string backendFolder = Path.Combine(Application.streamingAssetsPath, "Backend");
        string targetFolder = Path.Combine(backendFolder, "result_processed"); 

        // Buat folder jika belum ada (safety)
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        // Buka Explorer (Ganti / dengan \ agar Windows tidak bingung)
        targetFolder = targetFolder.Replace("/", "\\");
        Process.Start("explorer.exe", targetFolder);
    }

    // --- LOGIKA MENJALANKAN PYTHON (.EXE) ---
    void RunBackend(string imagePath)
    {
        // Cari lokasi file EXE di dalam folder game
        string backendFolder = Path.Combine(Application.streamingAssetsPath, "Backend");
        string exeName = "crop and split 2 in 1.exe"; 
        string fullExePath = Path.Combine(backendFolder, exeName);

        UnityEngine.Debug.Log("Menjalankan: " + fullExePath);

        ProcessStartInfo start = new ProcessStartInfo();
        start.FileName = fullExePath;
        
        // Kirim path gambar sebagai argumen (pakai tanda kutip biar aman dari spasi)
        start.Arguments = $"\"{imagePath}\""; 
        
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.CreateNoWindow = true;
        
        // PENTING: Set Working Directory agar Python bisa menemukan 'a.exe' dan simpan hasil di tempat yg benar
        start.WorkingDirectory = backendFolder; 

        try
        {
            Process process = Process.Start(start);
            
            // Baca output dari Python
            string output = process.StandardOutput.ReadToEnd();
            string err = process.StandardError.ReadToEnd();
            
            process.WaitForExit();

            UnityEngine.Debug.Log("Python Output: " + output);

            // Cek apakah sukses (Mencari kata kunci [SUCCESS] atau DONE_SUCCESS)
            // Sesuaikan dengan kata kunci di script Python Anda yang baru
            if (output.Contains("[SUCCESS]") || output.Contains("DONE_SUCCESS")) 
            {
                statusText.text = "Success!";
                statusText.color = Color.green;
                btnOpenResult.interactable = true; // Nyalakan tombol buka folder
            }
            else
            {
                // Jika gagal
                statusText.text = "Failed!";
                statusText.color = Color.red;
                UnityEngine.Debug.LogError("Backend Error: " + err);
            }
        }
        catch (System.Exception e)
        {
            statusText.text = "System Error";
            statusText.color = Color.red;
            UnityEngine.Debug.LogError("Exception: " + e.Message);
        }

        // Hidupkan kembali tombol setelah selesai
        btnProcess.interactable = true;
        btnSelectFile.interactable = true;
    }
}