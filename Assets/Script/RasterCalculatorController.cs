using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using System.Linq;
using BitMiracle.LibTiff.Classic;

// ============================================================
// RasterCalculatorController
// ============================================================
// Controller untuk Raster Calculator UI
// Mengintegrasikan Python script: raster_calculator_standalone (1).py
// Fitur:
// - Input Formula dengan sugesti Band (@ trigger)
// - Save/Load Formula (Local JSON)
// - Integrasi ke TiffLayerManager untuk menampilkan hasil
// ============================================================

public class RasterCalculatorController : MonoBehaviour
{
    [Header("UI References - Main")]
    public GameObject panelRoot;
    public TMP_InputField inputFormulaName;
    public TMP_InputField inputFormula;
    public TMP_InputField inputOutputName;
    public TMP_Dropdown dropdownLayers;
    public Button btnSave;
    public Button btnLoad;
    public Button btnProcess;
    public TextMeshProUGUI textStatus;

    // Auto-generated UI
    private GameObject suggestionContainer;

    [Header("UI References - Saved List")]
    public GameObject panelListCode;
    public Transform listContent;
    public GameObject listItemPrefab; // Prefab item list (harus punya komponen RasterCalcListItem)
    public Button btnCloseList;

    [Header("Dependencies")]
    public TiffLayerManager tiffLayerManager;
    
    // Config
    private string backendFolder;
    private string scriptName = "raster_calculator_standalone (1).py";
    private string savedFormulasPath;

    // State
    private List<SavedFormula> savedFormulas = new List<SavedFormula>();
    private TiffLayerManager.LayerData selectedLayer;
    private int currentLayerBandCount = 4;
    private float _checkTimer = 0f;
    private int _lastLayerCount = -1;

    // Data Classes
    [Serializable]
    public class SavedFormula
    {
        public string name;
        public string formula;
        public string date;
    }

    [Serializable]
    public class BackendResult
    {
        public string status;
        public string messages;
        public string path; // Output TIFF path
        public string preview_png;
        public BoundsData bounds;
    }

    [Serializable]
    public class BoundsData { public double north, south, west, east; }

    // ============================================================
    // LIFECYCLE
    // ============================================================

    void Start()
    {
        // Setup Paths
        backendFolder = Path.Combine(Application.streamingAssetsPath, "Backend"); // Asumsi script ada di sini atau Assets/Script? 
        // Note: User bilang script ada di Assets/Script, tapi biasanya backend executable/script dicopy ke StreamingAssets utk build.
        // Kita akan coba cari di Assets/Script dulu jika di editor.
        
        savedFormulasPath = Path.Combine(Application.persistentDataPath, "raster_formulas.json");

        // Setup Listeners
        if (btnProcess) btnProcess.onClick.AddListener(OnProcessClick);
        if (btnSave) btnSave.onClick.AddListener(OnSaveClick);
        if (btnLoad) btnLoad.onClick.AddListener(OnLoadClick);
        if (btnCloseList) btnCloseList.onClick.AddListener(() => panelListCode.SetActive(false));
        
        if (inputFormula) inputFormula.onValueChanged.AddListener(OnFormulaChanged);
        if (dropdownLayers) dropdownLayers.onValueChanged.AddListener(OnLayerChanged);

        // Load Data
        LoadSavedFormulas();
        RefreshLayerDropdown();

        // Init UI
        if (panelListCode) panelListCode.SetActive(false);
        if (suggestionContainer) suggestionContainer.SetActive(false);
    }

    private void OnEnable()
    {
        RefreshLayerDropdown();
    }

    void Update()
    {
        // Auto-refresh layer list check every 1 sec to detect new layers from ProjectManager
        _checkTimer += Time.deltaTime;
        if (_checkTimer > 1.0f)
        {
            _checkTimer = 0f;
            CheckLayerChanges();
        }
    }

    void CheckLayerChanges()
    {
        if (tiffLayerManager == null || tiffLayerManager.layers == null) return;
        
        // Simple check: count changes
        if (tiffLayerManager.layers.Count != _lastLayerCount)
        {
            _lastLayerCount = tiffLayerManager.layers.Count;
            RefreshLayerDropdown();
        }
    }

    // ============================================================
    // LOGIC: LAYER & SUGGESTION
    // ============================================================

    public void RefreshLayerDropdown()
    {
        if (dropdownLayers == null || tiffLayerManager == null) return;

        // Simpan selection saat ini agar tidak reset
        string currentSelection = selectedLayer != null ? selectedLayer.name : "";

        dropdownLayers.ClearOptions();
        List<string> options = new List<string>();
        
        // Ambil layer dari TiffLayerManager
        // Kita butuh akses ke layers. Asumsi tiffLayerManager.layers public.
        if (tiffLayerManager.layers != null)
        {
            foreach (var layer in tiffLayerManager.layers)
            {
                options.Add(layer.name);
            }
        }

        dropdownLayers.AddOptions(options);
        
        // Restore selection or default
        int targetIndex = 0;
        if (!string.IsNullOrEmpty(currentSelection))
        {
            int foundIndex = options.FindIndex(x => x == currentSelection);
            if (foundIndex >= 0) targetIndex = foundIndex;
        }

        if (options.Count > 0)
        {
            dropdownLayers.value = targetIndex;
            OnLayerChanged(targetIndex); // Force update status
        }
        else 
        {
            selectedLayer = null;
            SetStatus("No layers available", true);
        }
    }

    void OnLayerChanged(int index)
    {
        if (tiffLayerManager == null || tiffLayerManager.layers == null || index < 0 || index >= tiffLayerManager.layers.Count)
        {
            selectedLayer = null;
            currentLayerBandCount = 0;
            SetStatus("No layer selected", true);
            return;
        }
        selectedLayer = tiffLayerManager.layers[index];
        UpdateBandCount(selectedLayer.path);
    }

    void UpdateBandCount(string path)
    {
        currentLayerBandCount = 4; // Default safe
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                using (Tiff tiff = Tiff.Open(path, "r"))
                {
                    if (tiff != null)
                    {
                        FieldValue[] value = tiff.GetField(TiffTag.SAMPLESPERPIXEL);
                        if (value != null && value.Length > 0)
                        {
                            currentLayerBandCount = value[0].ToInt();
                            SetStatus($"Layer Ready: {selectedLayer.name} ({currentLayerBandCount} Bands)", false);
                            UnityEngine.Debug.Log($"[RasterCalc] Layer Loaded: {selectedLayer.name}, Bands: {currentLayerBandCount}");
                        }
                        else
                        {
                            SetStatus($"Layer Warning: {selectedLayer.name} (Unknown Bands, Default 4)", true);
                        }
                    }
                    else
                    {
                        SetStatus($"Layer Error: Cannot open TIFF file", true);
                    }
                }
            }
            catch (Exception e) 
            {
                SetStatus($"Layer Error: {e.Message}", true);
                UnityEngine.Debug.LogError($"[RasterCalc] Error reading TIFF: {e}");
            }
        }
        else
        {
             SetStatus($"Layer Error: File not found at {path}", true);
        }
    }

    void OnFormulaChanged(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Cek karakter terakhir apakah '@'
        if (text.EndsWith("@"))
        {
            UnityEngine.Debug.Log("[RasterCalc] Trigger '@' detected");
            
            // Auto-recovery: Jika selectedLayer null tapi ada layer tersedia, pilih yang pertama
            if (selectedLayer == null)
            {
                if (tiffLayerManager != null && tiffLayerManager.layers != null && tiffLayerManager.layers.Count > 0)
                {
                    UnityEngine.Debug.Log("[RasterCalc] Auto-selecting first available layer");
                    OnLayerChanged(0);
                    if (dropdownLayers) dropdownLayers.value = 0;
                }
            }

            if (selectedLayer == null)
            {
                int layerCount = (tiffLayerManager != null && tiffLayerManager.layers != null) ? tiffLayerManager.layers.Count : 0;
                SetStatus($"Warning: No active layer selected (Available: {layerCount}). Please check Project Manager.", true);
                return;
            }
            ShowBandSuggestions();
        }
        else
        {
            if (suggestionContainer) suggestionContainer.SetActive(false);
        }
    }

    void ShowBandSuggestions()
    {
        if (selectedLayer == null) return;

        UnityEngine.Debug.Log($"[RasterCalc] Showing suggestions for layer: {selectedLayer.name}, Bands: {currentLayerBandCount}");

        // Buat container jika belum ada
        if (suggestionContainer == null) CreateSuggestionContainer();

        // Bersihkan child lama
        foreach (Transform child in suggestionContainer.transform) Destroy(child.gameObject);

        // Gunakan band count asli dari file TIFF
        int count = currentLayerBandCount > 0 ? currentLayerBandCount : 4;
        
        for (int i = 1; i <= count; i++)
        {
            string bandName = $"b{i}";
            CreateSuggestionButton(bandName);
        }
        
        // Update posisi dropdown ke kursor
        UpdateDropdownPosition();

        suggestionContainer.SetActive(true);
    }

    private void CreateSuggestionContainer()
    {
        suggestionContainer = new GameObject("SuggestionPopup");
        suggestionContainer.transform.SetParent(inputFormula.transform.parent, false); // Parent ke area yang sama dengan input

        // Setup RectTransform
        RectTransform rect = suggestionContainer.AddComponent<RectTransform>();
        rect.pivot = new Vector2(0, 1); // Top-Left Pivot
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(100, 0); // Lebar 100, tinggi auto

        // Setup Background
        Image img = suggestionContainer.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark background

        // Setup Layout
        VerticalLayoutGroup vlg = suggestionContainer.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(5, 5, 5, 5);
        vlg.spacing = 2;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;

        // Setup Size Fitter
        ContentSizeFitter csf = suggestionContainer.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void CreateSuggestionButton(string bandName)
    {
        GameObject btnObj = new GameObject("Btn_" + bandName);
        btnObj.transform.SetParent(suggestionContainer.transform, false);

        // Setup Button Component
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.4f, 0.4f, 0.4f, 1f); // Sedikit lebih terang dari bg
        
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        
        // Add Layout Element
        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 30;

        // Setup Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(5, 0);
        textRect.offsetMax = new Vector2(-5, 0);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = bandName;
        tmp.fontSize = 14;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        
        // Copy Font asset dari inputFormula jika ada
        if (inputFormula.textComponent != null) tmp.font = inputFormula.textComponent.font;

        // Add Listener
        btn.onClick.AddListener(() => InsertBand(bandName));
    }

    private void UpdateDropdownPosition()
    {
        if (inputFormula == null || suggestionContainer == null) return;

        // Force update untuk mendapatkan posisi caret terbaru
        inputFormula.textComponent.ForceMeshUpdate();
        
        int caretPos = inputFormula.caretPosition;
        
        // Kita butuh info karakter sebelum caret (yaitu '@')
        // Jika caret di 0, tidak ada '@' sebelumnya, jadi return
        if (caretPos <= 0) return;

        var textInfo = inputFormula.textComponent.textInfo;
        
        // Pastikan index valid
        int charIndex = Mathf.Clamp(caretPos - 1, 0, textInfo.characterCount - 1);
        
        TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

        // Dapatkan posisi bottom-right dari karakter tersebut (local space text component)
        Vector3 localPos = charInfo.bottomRight;

        // Convert ke World Space
        Vector3 worldPos = inputFormula.textComponent.transform.TransformPoint(localPos);

        // Set posisi popup
        suggestionContainer.transform.position = worldPos;
        
        // Ensure it's on top
        suggestionContainer.transform.SetAsLastSibling();
    }

    void InsertBand(string selectedBand)
    {
        if (suggestionContainer) suggestionContainer.SetActive(false);
        
        // Replace "@" terakhir dengan band
        string currentText = inputFormula.text;
        if (currentText.EndsWith("@"))
        {
            inputFormula.text = currentText.Substring(0, currentText.Length - 1) + selectedBand;
            
            // Pindahkan caret ke akhir
            inputFormula.caretPosition = inputFormula.text.Length;
            
            // Fokus kembali ke input
            inputFormula.Select();
            inputFormula.ActivateInputField();
        }
    }

    void OnSuggestionSelected(int index) { } // Deprecated

    // ============================================================
    // LOGIC: SAVE / LOAD SYSTEM
    // ============================================================

    void LoadSavedFormulas()
    {
        if (File.Exists(savedFormulasPath))
        {
            try
            {
                string json = File.ReadAllText(savedFormulasPath);
                Wrapper wrapper = JsonUtility.FromJson<Wrapper>(json);
                if (wrapper != null) savedFormulas = wrapper.items;
            }
            catch (Exception e) { UnityEngine.Debug.LogError("Failed to load formulas: " + e.Message); }
        }
    }

    void SaveFormulasToDisk()
    {
        Wrapper wrapper = new Wrapper { items = savedFormulas };
        string json = JsonUtility.ToJson(wrapper);
        File.WriteAllText(savedFormulasPath, json);
    }

    void OnSaveClick()
    {
        if (string.IsNullOrEmpty(inputFormulaName.text) || string.IsNullOrEmpty(inputFormula.text)) return;

        // Cek duplicate name, replace if exists
        var existing = savedFormulas.Find(x => x.name == inputFormulaName.text);
        if (existing != null)
        {
            existing.formula = inputFormula.text;
            existing.date = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }
        else
        {
            savedFormulas.Add(new SavedFormula
            {
                name = inputFormulaName.text,
                formula = inputFormula.text,
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
        }

        SaveFormulasToDisk();
        UnityEngine.Debug.Log("Formula Saved!");
    }

    void OnLoadClick()
    {
        if (panelListCode)
        {
            panelListCode.SetActive(true);
            RefreshListUI();
        }
    }

    void RefreshListUI()
    {
        if (listContent == null || listItemPrefab == null) return;

        // Clear existing
        foreach (Transform child in listContent) Destroy(child.gameObject);

        // Populate
        foreach (var item in savedFormulas)
        {
            GameObject go = Instantiate(listItemPrefab, listContent);
            RasterCalcListItem script = go.GetComponent<RasterCalcListItem>();
            
            if (script != null)
            {
                script.Setup(
                    item.name, 
                    item.date, 
                    () => { ApplyFormula(item); panelListCode.SetActive(false); },
                    () => { DeleteFormula(item); }
                );
            }
            else
            {
                // Fallback jika lupa pasang script (Legacy Logic)
                var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 1) texts[0].text = item.name;
                if (texts.Length >= 2) texts[1].text = item.date;

                var btns = go.GetComponentsInChildren<Button>();
                if (btns.Length >= 1) btns[0].onClick.AddListener(() => { ApplyFormula(item); panelListCode.SetActive(false); });
                if (btns.Length >= 2) btns[1].onClick.AddListener(() => { DeleteFormula(item); });
            }
        }
    }

    void ApplyFormula(SavedFormula item)
    {
        if (inputFormulaName) inputFormulaName.text = item.name;
        if (inputFormula) inputFormula.text = item.formula;
    }

    void DeleteFormula(SavedFormula item)
    {
        savedFormulas.Remove(item);
        SaveFormulasToDisk();
        RefreshListUI();
    }

    [Serializable]
    class Wrapper { public List<SavedFormula> items; }

    // ============================================================
    // LOGIC: PROCESS (PYTHON INTEGRATION)
    // ============================================================

    async void OnProcessClick()
    {
        if (selectedLayer == null) { SetStatus("Pilih layer dahulu!", true); return; }
        if (string.IsNullOrEmpty(inputFormula.text)) { SetStatus("Masukkan rumus!", true); return; }
        if (string.IsNullOrEmpty(inputOutputName.text)) { SetStatus("Masukkan nama output!", true); return; }

        string formula = inputFormula.text;
        string outputName = inputOutputName.text;
        string inputPath = selectedLayer.path; // Asumsi LayerData punya field 'path' (kita cek nanti)

        // Validasi Path
        if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
        {
            SetStatus("File Tiff layer tidak ditemukan!", true);
            return;
        }

        btnProcess.interactable = false;
        SetStatus("Processing...", false);

        string resultJson = await RunPythonScript(inputPath, formula, outputName);
        
        HandleResult(resultJson);
        
        btnProcess.interactable = true;
    }

    async Task<string> RunPythonScript(string inputPath, string formula, string outputName)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Tentukan lokasi script python
                // Prioritas 1: Assets/Script (Development)
                string scriptPath = Path.Combine(Application.dataPath, "Script", scriptName);
                if (!File.Exists(scriptPath))
                {
                    // Prioritas 2: StreamingAssets/Backend (Build)
                    scriptPath = Path.Combine(backendFolder, scriptName);
                }

                if (!File.Exists(scriptPath)) return "{\"status\":\"failed\", \"messages\":\"Script not found\"}";

                // Python Executable logic (sama seperti RasterTransformController)
                string pythonExe = "python"; 
                // Bisa ditambahkan logic cek venv jika perlu

                // Arguments
                // python script.py -i "path" -f "formula" -n "name"
                string args = $"\"{scriptPath}\" -i \"{inputPath}\" -f \"{formula}\" -n \"{outputName}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = backendFolder // Run di backend folder agar output rapi
                };

                // Pastikan folder backend ada
                if (!Directory.Exists(backendFolder)) Directory.CreateDirectory(backendFolder);

                // Env vars for encoding
                startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                using (Process p = Process.Start(startInfo))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    if (!string.IsNullOrEmpty(error) && !output.Trim().StartsWith("{"))
                    {
                        UnityEngine.Debug.LogError("Python Error: " + error);
                        // Jika output kosong tapi ada error, return error
                        if (string.IsNullOrEmpty(output)) return "{\"status\":\"failed\", \"messages\":\"" + error.Replace("\"", "'").Replace("\n", " ") + "\"}";
                    }

                    // Ambil baris terakhir yang berupa JSON (jika ada print lain sebelumnya)
                    string[] lines = output.Split('\n');
                    foreach (var line in lines.Reverse())
                    {
                        if (line.Trim().StartsWith("{") && line.Trim().EndsWith("}"))
                            return line.Trim();
                    }

                    return output;
                }
            }
            catch (Exception e)
            {
                return "{\"status\":\"failed\", \"messages\":\"" + e.Message + "\"}";
            }
        });
    }

    void HandleResult(string json)
    {
        UnityEngine.Debug.Log("Python Result: " + json);
        
        try
        {
            BackendResult result = JsonUtility.FromJson<BackendResult>(json);

            if (result.status == "success")
            {
                SetStatus("Sukses!", false);
                
                string overlayName = !string.IsNullOrEmpty(inputOutputName?.text) ? inputOutputName.text : "RasterCalc_Result";
                
                // Prioritas 1: Load PNG preview dengan bounds agar tampil sebagai overlay di project aktif
                if (!string.IsNullOrEmpty(result.preview_png) && result.bounds != null)
                {
                    string pngPath = result.preview_png;
                    if (!Path.IsPathRooted(pngPath))
                        pngPath = Path.Combine(backendFolder, pngPath);
                    
                    if (File.Exists(pngPath))
                    {
                        tiffLayerManager.LoadPngOverlay(
                            pngPath, 
                            result.bounds.north, 
                            result.bounds.south, 
                            result.bounds.west, 
                            result.bounds.east, 
                            false, 
                            false, 
                            overlayName
                        );
                        tiffLayerManager.OnPropertyToggleExternal(overlayName, true);
                        return;
                    }
                }
                
                // Prioritas 2: Fallback ke TIFF jika PNG tidak tersedia
                if (!string.IsNullOrEmpty(result.path))
                {
                    string tifPath = result.path;
                    if (!Path.IsPathRooted(tifPath))
                        tifPath = Path.Combine(backendFolder, tifPath);
                    
                    if (File.Exists(tifPath))
                    {
                        tiffLayerManager.LoadTiff(tifPath, false);
                    }
                }
            }
            else
            {
                SetStatus("Gagal: " + result.messages, true);
            }
        }
        catch (Exception e)
        {
            SetStatus("Error parsing result: " + e.Message, true);
        }
    }

    void SetStatus(string msg, bool isError)
    {
        if (textStatus)
        {
            textStatus.text = msg;
            textStatus.color = isError ? Color.red : Color.white;
        }
        UnityEngine.Debug.Log("[RasterCalc] " + msg);
    }
}
