using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class GNSSPopupFactory
{
    private static TMP_DefaultControls.Resources GetTMPResources()
    {
        TMP_DefaultControls.Resources res = new TMP_DefaultControls.Resources();
        res.standard = null;
        res.background = null;
        res.inputField = null;
        return res;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/GNSS/Create GNSS Data Viewer Prefab")]
    public static void CreateGNSSDataReaderPrefab()
    {
        GameObject popup = CreateGNSSDataReader(null, true);
        string folder = "Assets/Resources";
        string path = folder + "/GNSS_DataReader_Popup.prefab";
        
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        UnityEditor.PrefabUtility.SaveAsPrefabAsset(popup, path);
        Object.DestroyImmediate(popup);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("GNSS Data Viewer Prefab created at: " + path);
    }

    [UnityEditor.MenuItem("Tools/IMU/Create IMU Bin To Log Prefab")]
    public static void CreateIMUBinToLogPrefab()
    {
        GameObject popup = CreateIMUBinToLog(null, true);
        string folder = "Assets/Resources";
        string path = folder + "/IMU_BinToLog_Popup.prefab";
        
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        UnityEditor.PrefabUtility.SaveAsPrefabAsset(popup, path);
        Object.DestroyImmediate(popup);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("IMU Bin To Log Prefab created at: " + path);
    }
#endif

    public static GameObject CreateIMUBinToLog(Canvas canvas = null, bool forceNew = false)
    {
        if (!forceNew) {
            GameObject prefab = Resources.Load<GameObject>("IMU_BinToLog_Popup");
            if (prefab != null) {
                if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
                GameObject instance = Object.Instantiate(prefab, canvas.transform);
                instance.name = "IMU_BinToLog_Root";
                AttachPopupLogic(instance, "File Bin Converter");
                return instance;
            }
        }
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        // 1. Root Overlay
        GameObject root = new GameObject("IMU_BinToLog_Root", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rtRoot = root.GetComponent<RectTransform>();
        rtRoot.anchorMin = Vector2.zero; rtRoot.anchorMax = Vector2.one; rtRoot.sizeDelta = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);

        // 2. Main Window
        GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(root.transform, false);
        RectTransform rtWin = window.GetComponent<RectTransform>();
        rtWin.anchorMin = rtWin.anchorMax = new Vector2(0.5f, 0.5f);
        rtWin.sizeDelta = new Vector2(650, 400);
        window.GetComponent<Image>().color = new Color32(235, 235, 235, 255);

        // Header
        GameObject header = CreateUIElement("Header", window.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30));
        header.GetComponent<Image>().color = new Color32(245, 245, 245, 255);
        CreateText(header.transform, "File Bin Converter", 14, Color.black, TextAlignmentOptions.Left, new Vector2(10, 0));

        // Content
        float currentY = -40;
        
        // Input Binary File Log (.bin)
        CreateInputWithBrowse(window.transform, "Input Binary File Log (.bin)", "", ref currentY, 30);

        currentY -= 20;

        // Select Output Directory
        CreateInputWithBrowse(window.transform, "Select Output Directory", "", ref currentY, 30);

        // Footer Buttons
        GameObject footer = CreateUIElement("Footer", window.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40));
        GameObject execBtn = CreateSmallButton(footer.transform, "EXECUTE", new Vector2(180, 0), new Vector2(150, 40));
        GameObject cancelBtn = CreateSmallButton(footer.transform, "CANCEL", new Vector2(470, 0), new Vector2(150, 40));

        // Logic
        AttachPopupLogic(root, "File Bin Converter");
        
        return root;
    }

    public static GameObject CreateGNSSDataReader(Canvas canvas = null, bool forceNew = false)
    {
        if (!forceNew) {
            GameObject prefab = Resources.Load<GameObject>("GNSS_DataReader_Popup");
            if (prefab != null) {
                if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
                GameObject instance = Object.Instantiate(prefab, canvas.transform);
                instance.name = "GNSS_DataReader_Root";
                AttachPopupLogic(instance, "GNSS Data Viewer");
                return instance;
            }
        }
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        // 1. Root Panel (Overlay/Background)
        GameObject root = new GameObject("GNSS_DataReader_Root", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rtRoot = root.GetComponent<RectTransform>();
        rtRoot.anchorMin = Vector2.zero;
        rtRoot.anchorMax = Vector2.one;
        rtRoot.sizeDelta = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f); // Dim background

        // 2. Main Window
        GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(root.transform, false);
        RectTransform rtWindow = window.GetComponent<RectTransform>();
        rtWindow.anchorMin = rtWindow.anchorMax = new Vector2(0.5f, 0.5f);
        rtWindow.sizeDelta = new Vector2(650, 350);
        window.GetComponent<Image>().color = new Color32(230, 230, 230, 255); // Light grey window

        // 3. Header
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(window.transform, false);
        RectTransform rtHeader = header.GetComponent<RectTransform>();
        rtHeader.anchorMin = new Vector2(0, 1);
        rtHeader.anchorMax = new Vector2(1, 1);
        rtHeader.pivot = new Vector2(0.5f, 1);
        rtHeader.anchoredPosition = Vector2.zero;
        rtHeader.sizeDelta = new Vector2(0, 30);
        header.GetComponent<Image>().color = new Color32(245, 245, 245, 255); // Very light grey header

        // 4. Header Icon & Title
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(header.transform, false);
        RectTransform rtTitle = titleObj.GetComponent<RectTransform>();
        rtTitle.anchorMin = new Vector2(0, 0.5f);
        rtTitle.anchorMax = new Vector2(1, 0.5f);
        rtTitle.anchoredPosition = new Vector2(10, 0);
        rtTitle.sizeDelta = new Vector2(-20, 20);
        TextMeshProUGUI titleTMP = titleObj.GetComponent<TextMeshProUGUI>();
        titleTMP.text = "Raw Data GNSS Reader";
        titleTMP.fontSize = 14;
        titleTMP.color = new Color32(100, 100, 100, 255);
        titleTMP.alignment = TextAlignmentOptions.Left;

        // 5. Border Frame (The inside grey frame)
        GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(window.transform, false);
        RectTransform rtFrame = frame.GetComponent<RectTransform>();
        rtFrame.anchorMin = Vector2.zero;
        rtFrame.anchorMax = Vector2.one;
        rtFrame.offsetMin = new Vector2(10, 10);
        rtFrame.offsetMax = new Vector2(-10, -40);
        frame.GetComponent<Image>().color = new Color32(210, 210, 210, 255);
        // Add Outline effect if possible, or just use slightly different color

        // 6. Label Input
        GameObject labelInput = new GameObject("Label_Input", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelInput.transform.SetParent(frame.transform, false);
        RectTransform rtLabelInput = labelInput.GetComponent<RectTransform>();
        rtLabelInput.anchorMin = new Vector2(0, 1);
        rtLabelInput.anchorMax = new Vector2(1, 1);
        rtLabelInput.anchoredPosition = new Vector2(15, -20);
        rtLabelInput.sizeDelta = new Vector2(-30, 20);
        TextMeshProUGUI labelInputTMP = labelInput.GetComponent<TextMeshProUGUI>();
        labelInputTMP.text = "Input Raw Data GNSS (.ubx)";
        labelInputTMP.fontSize = 14;
        labelInputTMP.color = new Color32(50, 50, 50, 255);

        // 7. Input Field Row (Input + Button)
        TMP_DefaultControls.Resources res = GetTMPResources();
        GameObject inputObj = TMP_DefaultControls.CreateInputField(res);
        inputObj.name = "InputField_Path";
        inputObj.transform.SetParent(frame.transform, false);
        RectTransform rtInput = inputObj.GetComponent<RectTransform>();
        rtInput.anchorMin = new Vector2(0, 1);
        rtInput.anchorMax = new Vector2(1, 1);
        rtInput.anchoredPosition = new Vector2(15, -45);
        rtInput.sizeDelta = new Vector2(-60, 25);
        
        // Browse Button (...)
        GameObject browseBtn = new GameObject("Button_Browse", typeof(RectTransform), typeof(Image), typeof(Button));
        browseBtn.transform.SetParent(frame.transform, false);
        RectTransform rtBrowse = browseBtn.GetComponent<RectTransform>();
        rtBrowse.anchorMin = new Vector2(1, 1);
        rtBrowse.anchorMax = new Vector2(1, 1);
        rtBrowse.anchoredPosition = new Vector2(-25, -45);
        rtBrowse.sizeDelta = new Vector2(35, 25);
        browseBtn.GetComponent<Image>().color = new Color32(230, 230, 230, 255);
        
        GameObject browseText = new GameObject("Text", typeof(TextMeshProUGUI));
        browseText.transform.SetParent(browseBtn.transform, false);
        TextMeshProUGUI bTMP = browseText.GetComponent<TextMeshProUGUI>();
        bTMP.text = "...";
        bTMP.color = Color.black;
        bTMP.fontSize = 14;
        bTMP.alignment = TextAlignmentOptions.Center;

        // 8. Footer Buttons (Execute & Cancel)
        // Execute Button
        GameObject executeBtn = new GameObject("Button_Execute", typeof(RectTransform), typeof(Image), typeof(Button));
        executeBtn.transform.SetParent(window.transform, false);
        RectTransform rtExec = executeBtn.GetComponent<RectTransform>();
        rtExec.anchorMin = new Vector2(0.3f, 0);
        rtExec.anchorMax = new Vector2(0.3f, 0);
        rtExec.anchoredPosition = new Vector2(0, 40);
        rtExec.sizeDelta = new Vector2(150, 35);
        executeBtn.GetComponent<Image>().color = new Color32(230, 230, 230, 255);

        GameObject execText = new GameObject("Text", typeof(TextMeshProUGUI));
        execText.transform.SetParent(executeBtn.transform, false);
        TextMeshProUGUI eTMP = execText.GetComponent<TextMeshProUGUI>();
        eTMP.text = "EXECUTE";
        eTMP.color = Color.black;
        eTMP.fontSize = 16;
        eTMP.alignment = TextAlignmentOptions.Center;

        // Cancel Button
        GameObject cancelBtn = new GameObject("Button_Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelBtn.transform.SetParent(window.transform, false);
        RectTransform rtCancel = cancelBtn.GetComponent<RectTransform>();
        rtCancel.anchorMin = new Vector2(0.7f, 0);
        rtCancel.anchorMax = new Vector2(0.7f, 0);
        rtCancel.anchoredPosition = new Vector2(0, 40);
        rtCancel.sizeDelta = new Vector2(150, 35);
        cancelBtn.GetComponent<Image>().color = new Color32(230, 230, 230, 255);

        GameObject cancelText = new GameObject("Text", typeof(TextMeshProUGUI));
        cancelText.transform.SetParent(cancelBtn.transform, false);
        TextMeshProUGUI cTMP = cancelText.GetComponent<TextMeshProUGUI>();
        cTMP.text = "CANCEL";
        cTMP.color = Color.black;
        cTMP.fontSize = 16;
        cTMP.alignment = TextAlignmentOptions.Center;

        // Logic
        AttachPopupLogic(root, "GNSS Data Viewer");
        root.AddComponent<GNSSDataReaderUI>().Setup(inputObj.GetComponent<TMP_InputField>(), browseBtn.GetComponent<Button>(), executeBtn.GetComponent<Button>());

        return root;
    }

    // --- Static Processing Popup Implementation ---

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/GNSS/Create Static Processing Prefab")]
    public static void CreateStaticProcessingPrefab()
    {
        GameObject popup = CreateStaticProcessing(null, true);
        string folder = "Assets/Resources";
        string path = folder + "/GNSS_StaticProcessing_Popup.prefab";
        
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        UnityEditor.PrefabUtility.SaveAsPrefabAsset(popup, path);
        Object.DestroyImmediate(popup);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("Static Processing Prefab created at: " + path);
    }
#endif

    public static GameObject CreateStaticProcessing(Canvas canvas = null, bool forceNew = false)
    {
        if (!forceNew) {
            GameObject prefab = Resources.Load<GameObject>("GNSS_StaticProcessing_Popup");
            if (prefab != null) {
                if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
                GameObject instance = Object.Instantiate(prefab, canvas.transform);
                instance.name = "StaticProcessing_Root";
                AttachPopupLogic(instance, "Static Processing");
                return instance;
            }
        }
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        // 1. Root Overlay
        GameObject root = new GameObject("StaticProcessing_Root", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rtRoot = root.GetComponent<RectTransform>();
        rtRoot.anchorMin = Vector2.zero; rtRoot.anchorMax = Vector2.one; rtRoot.sizeDelta = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);

        // 2. Main Window
        GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(root.transform, false);
        RectTransform rtWin = window.GetComponent<RectTransform>();
        rtWin.anchorMin = rtWin.anchorMax = new Vector2(0.5f, 0.5f);
        rtWin.sizeDelta = new Vector2(700, 900);
        window.GetComponent<Image>().color = new Color32(235, 235, 235, 255);

        // Header
        GameObject header = CreateUIElement("Header", window.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30));
        header.GetComponent<Image>().color = new Color32(245, 245, 245, 255);
        CreateText(header.transform, "TGS Post Processing 1.15.13", 14, Color.black, TextAlignmentOptions.Left, new Vector2(10, 0));

        // Scroll View Content
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(window.transform, false);
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = Vector2.zero; rtContent.anchorMax = Vector2.one;
        rtContent.offsetMin = new Vector2(10, 80); rtContent.offsetMax = new Vector2(-10, -40);

        float currentY = -20;

        // Frequencies
        CreateLabel(content.transform, "Frequencies", ref currentY);
        GameObject freqDD = TMP_DefaultControls.CreateDropdown(GetTMPResources());
        freqDD.transform.SetParent(content.transform, false);
        RectTransform rtFreq = freqDD.GetComponent<RectTransform>();
        rtFreq.anchorMin = new Vector2(0, 1); rtFreq.anchorMax = new Vector2(0.3f, 1);
        rtFreq.anchoredPosition = new Vector2(110, currentY + 12);
        rtFreq.sizeDelta = new Vector2(0, 25);
        TMP_Dropdown ddFreq = freqDD.GetComponent<TMP_Dropdown>();
        ddFreq.options.Clear(); ddFreq.options.Add(new TMP_Dropdown.OptionData("Dual Frequency"));
        if (ddFreq.captionText != null) { ddFreq.captionText.color = Color.black; ddFreq.captionText.fontSize = 11; }
        currentY -= 35;

        // Input GNSS BASE
        currentY -= 10;
        CreateInputWithBrowse(content.transform, "Input GNSS BASE :", "Rinex", ref currentY, 30, true);

        // Input Coordinate Base
        currentY -= 10;
        CreateLabel(content.transform, "Input Coordinate Base :", ref currentY);
        GameObject radioRow = CreateUIElement("RadioRow", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 5));
        CreateRadioButton(radioRow.transform, "Average of Single Position", new Vector2(20, 0), true);
        CreateRadioButton(radioRow.transform, "Lat/Lon/Height", new Vector2(250, 0), false);
        currentY -= 35;

        // Lat/Lon/Height Inputs
        GameObject tripleInput = CreateUIElement("TripleInput", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 15));
        CreateLabelledInput(tripleInput.transform, "Latitude(deg)", 20, 200, true);
        CreateLabelledInput(tripleInput.transform, "Longitude(deg)", 240, 200, true);
        CreateLabelledInput(tripleInput.transform, "Height(m)", 460, 200, true);
        currentY -= 60;

        // Antenna Base Height
        CreateLabel(content.transform, "Antenna Base Height :", ref currentY);
        GameObject antHeightRow = CreateUIElement("AntHeightRow", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 10));
        
        GameObject antInput = TMP_DefaultControls.CreateInputField(GetTMPResources());
        antInput.transform.SetParent(antHeightRow.transform, false);
        RectTransform rtAnt = antInput.GetComponent<RectTransform>();
        rtAnt.anchorMin = new Vector2(0, 0.5f); rtAnt.anchorMax = new Vector2(0, 0.5f);
        rtAnt.anchoredPosition = new Vector2(110, 0);
        rtAnt.sizeDelta = new Vector2(200, 25);
        antInput.GetComponent<Image>().color = Color.white;
        TMP_InputField tmpAnt = antInput.GetComponent<TMP_InputField>();
        tmpAnt.text = "0.000";
        if (tmpAnt.textComponent != null) { tmpAnt.textComponent.color = Color.black; tmpAnt.textComponent.fontSize = 11; }

        CreateText(antHeightRow.transform, "m", 13, Color.black, TextAlignmentOptions.Left, new Vector2(215, 0));
        currentY -= 35;

        // Input GNSS ROVER
        currentY -= 15;
        CreateLabel(content.transform, "Input GNSS ROVER", ref currentY);
        GameObject tableHeader = CreateUIElement("TableHeader", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 10));
        tableHeader.GetComponent<Image>().color = new Color32(220, 220, 220, 255);
        CreateText(tableHeader.transform, "No", 11, Color.black, TextAlignmentOptions.Center, new Vector2(-300, 0));
        CreateText(tableHeader.transform, "Rover File List", 11, Color.black, TextAlignmentOptions.Center, new Vector2(0, 0));
        CreateText(tableHeader.transform, "Antenna Height", 11, Color.black, TextAlignmentOptions.Center, new Vector2(280, 0));
        currentY -= 25;
        
        CreatePlaceholderBox(content.transform, "", ref currentY, 150);

        // Add/Clear Buttons
        GameObject btnRow = CreateUIElement("BtnRow", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 10));
        CreateSmallButton(btnRow.transform, "Add Rover", new Vector2(20, 0), new Vector2(120, 30));
        CreateSmallButton(btnRow.transform, "Clear", new Vector2(550, 0), new Vector2(120, 30));
        currentY -= 45;

        // Choose GNSS Satellite
        CreateLabel(content.transform, "Choose GNSS Satellite :", ref currentY);
        GameObject satRow = CreateUIElement("SatRow", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 5));
        CreateCheckbox(satRow.transform, "GPS", new Vector2(20, 0), true);
        CreateCheckbox(satRow.transform, "GLO", new Vector2(120, 0), true);
        CreateCheckbox(satRow.transform, "Galileo", new Vector2(220, 0), false);
        CreateCheckbox(satRow.transform, "QZSS", new Vector2(320, 0), false);
        CreateCheckbox(satRow.transform, "SBAS", new Vector2(420, 0), false);
        CreateCheckbox(satRow.transform, "BeiDou", new Vector2(520, 0), false);
        currentY -= 35;

        // Output Directory
        currentY -= 10;
        CreateInputWithBrowse(content.transform, "Output Directory :", "", ref currentY, 30);

        // Footer Buttons
        GameObject footer = CreateUIElement("Footer", window.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40));
        GameObject execBtn = CreateSmallButton(footer.transform, "EXECUTE", new Vector2(180, 0), new Vector2(150, 40));
        GameObject cancelBtn = CreateSmallButton(footer.transform, "CANCEL", new Vector2(470, 0), new Vector2(150, 40));

        // Logic
        AttachPopupLogic(root, "Static Processing");
        
        return root;
    }

    // --- Geotagging Popup Implementation ---

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/GNSS/Create Geotagging Prefab")]
    public static void CreateGeotaggingPrefab()
    {
        GameObject popup = CreateGeotagging(null, true); // force new
        string folder = "Assets/Resources";
        string path = folder + "/GNSS_Geotagging_Popup.prefab";
        
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        UnityEditor.PrefabUtility.SaveAsPrefabAsset(popup, path);
        Object.DestroyImmediate(popup);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("Geotagging Prefab created at: " + path);
    }
#endif

    public static GameObject CreateGeotagging(Canvas canvas = null, bool forceNew = false)
    {
        if (!forceNew) {
            GameObject prefab = Resources.Load<GameObject>("GNSS_Geotagging_Popup");
            if (prefab != null) {
                if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
                GameObject instance = Object.Instantiate(prefab, canvas.transform);
                instance.name = "Geotagging_Root";
                AttachPopupLogic(instance, "Geotagging");
                return instance;
            }
        }
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        // 1. Root Overlay
        GameObject root = new GameObject("Geotagging_Root", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rtRoot = root.GetComponent<RectTransform>();
        rtRoot.anchorMin = Vector2.zero; rtRoot.anchorMax = Vector2.one; rtRoot.sizeDelta = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);

        // 2. Main Window
        GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(root.transform, false);
        RectTransform rtWin = window.GetComponent<RectTransform>();
        rtWin.anchorMin = rtWin.anchorMax = new Vector2(0.5f, 0.5f);
        rtWin.sizeDelta = new Vector2(650, 450);
        window.GetComponent<Image>().color = new Color32(235, 235, 235, 255);

        // Header
        GameObject header = CreateUIElement("Header", window.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30));
        header.GetComponent<Image>().color = new Color32(245, 245, 245, 255);
        CreateText(header.transform, "GNSS Geotagging", 14, Color.black, TextAlignmentOptions.Left, new Vector2(10, 0));

        // Content Container
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(window.transform, false);
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = Vector2.zero; rtContent.anchorMax = Vector2.one;
        rtContent.offsetMin = new Vector2(10, 80); rtContent.offsetMax = new Vector2(-10, -40);

        float currentY = -20;

        // Input Flight
        CreateInputWithBrowse(content.transform, "Input Flight :", "POS", ref currentY, 30);

        // Input Directory Photo
        currentY -= 10;
        CreateInputWithBrowse(content.transform, "Input Directory Photo :", "", ref currentY, 30);

        // Output Directory
        currentY -= 10;
        CreateInputWithBrowse(content.transform, "Output Directory :", "", ref currentY, 30);

        // Footer Buttons
        GameObject footer = CreateUIElement("Footer", window.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40));
        GameObject execBtn = CreateSmallButton(footer.transform, "EXECUTE", new Vector2(180, 0), new Vector2(150, 40));
        GameObject cancelBtn = CreateSmallButton(footer.transform, "CANCEL", new Vector2(470, 0), new Vector2(150, 40));

        // Logic
        AttachPopupLogic(root, "Geotagging");
        
        return root;
    }

    // Helper Methods for Layout
    // --- PPK + Geotagging Popup Implementation ---

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/GNSS/Create PPK Geotagging Prefab")]
    public static void CreatePPKGeotaggingPrefab()
    {
        GameObject popup = CreatePPKGeotagging(null, true);
        string folder = "Assets/Resources";
        string path = folder + "/GNSS_PPKGeotagging_Popup.prefab";
        
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        UnityEditor.PrefabUtility.SaveAsPrefabAsset(popup, path);
        Object.DestroyImmediate(popup);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("PPK Geotagging Prefab created at: " + path);
    }
#endif

    public static GameObject CreatePPKGeotagging(Canvas canvas = null, bool forceNew = false)
    {
        if (!forceNew) {
            GameObject prefab = Resources.Load<GameObject>("GNSS_PPKGeotagging_Popup");
            if (prefab != null) {
                if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
                GameObject instance = Object.Instantiate(prefab, canvas.transform);
                instance.name = "PPKGeotagging_Root";
                AttachPopupLogic(instance, "PPK + Geotagging");
                return instance;
            }
        }
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        // 1. Root Overlay
        GameObject root = new GameObject("PPKGeotagging_Root", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rtRoot = root.GetComponent<RectTransform>();
        rtRoot.anchorMin = Vector2.zero; rtRoot.anchorMax = Vector2.one; rtRoot.sizeDelta = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);

        // 2. Main Window
        GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(root.transform, false);
        RectTransform rtWin = window.GetComponent<RectTransform>();
        rtWin.anchorMin = rtWin.anchorMax = new Vector2(0.5f, 0.5f);
        rtWin.sizeDelta = new Vector2(700, 850);
        window.GetComponent<Image>().color = new Color32(235, 235, 235, 255);

        // Header
        GameObject header = CreateUIElement("Header", window.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30));
        header.GetComponent<Image>().color = new Color32(245, 245, 245, 255);
        CreateText(header.transform, "TGS Post Processing 1.15.13", 14, Color.black, TextAlignmentOptions.Left, new Vector2(10, 0));

        // Scroll View Content
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(window.transform, false);
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = Vector2.zero; rtContent.anchorMax = Vector2.one;
        rtContent.offsetMin = new Vector2(10, 80); rtContent.offsetMax = new Vector2(-10, -40);

        float currentY = -20;

        // Frequencies
        CreateLabel(content.transform, "Frequencies", ref currentY);
        GameObject freqDD = TMP_DefaultControls.CreateDropdown(GetTMPResources());
        freqDD.transform.SetParent(content.transform, false);
        RectTransform rtFreq = freqDD.GetComponent<RectTransform>();
        rtFreq.anchorMin = new Vector2(0, 1); rtFreq.anchorMax = new Vector2(0.3f, 1);
        rtFreq.anchoredPosition = new Vector2(110, currentY + 12);
        rtFreq.sizeDelta = new Vector2(0, 25);
        TMP_Dropdown ddFreq = freqDD.GetComponent<TMP_Dropdown>();
        ddFreq.options.Clear(); 
        ddFreq.options.Add(new TMP_Dropdown.OptionData("Single Frequency"));
        ddFreq.options.Add(new TMP_Dropdown.OptionData("Dual Frequency"));
        ddFreq.value = 1; // Default to Dual Frequency as in image
        if (ddFreq.captionText != null) { ddFreq.captionText.color = Color.black; ddFreq.captionText.fontSize = 11; }
        currentY -= 35;

        // Input GNSS BASE
        currentY -= 10;
        CreateInputWithBrowse(content.transform, "Input GNSS BASE :", "Rinex", ref currentY, 30, true);

        // Input Coordinate Base
        currentY -= 10;
        CreateLabel(content.transform, "Input Coordinate Base :", ref currentY);
        GameObject radioRow = CreateUIElement("RadioRow", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 5));
        CreateRadioButton(radioRow.transform, "Average of Single Position", new Vector2(20, 0), true);
        CreateRadioButton(radioRow.transform, "Lat/Lon/Height", new Vector2(250, 0), false);
        currentY -= 35;

        // Lat/Lon/Height Inputs
        GameObject tripleInput = CreateUIElement("TripleInput", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 15));
        CreateLabelledInput(tripleInput.transform, "Latitude(deg)", 20, 200, true);
        CreateLabelledInput(tripleInput.transform, "Longitude(deg)", 240, 200, true);
        CreateLabelledInput(tripleInput.transform, "Height(m)", 460, 200, true);
        currentY -= 60;

        // Antenna Base Height
        CreateLabel(content.transform, "Antenna Base Height :", ref currentY);
        GameObject antHeightRow = CreateUIElement("AntHeightRow", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 10));
        
        GameObject antInput = TMP_DefaultControls.CreateInputField(GetTMPResources());
        antInput.transform.SetParent(antHeightRow.transform, false);
        RectTransform rtAnt = antInput.GetComponent<RectTransform>();
        rtAnt.anchorMin = new Vector2(0, 0.5f); rtAnt.anchorMax = new Vector2(0, 0.5f);
        rtAnt.anchoredPosition = new Vector2(110, 0);
        rtAnt.sizeDelta = new Vector2(200, 25);
        antInput.GetComponent<Image>().color = Color.white;
        TMP_InputField tmpAnt = antInput.GetComponent<TMP_InputField>();
        tmpAnt.text = "0.000";
        if (tmpAnt.textComponent != null) { tmpAnt.textComponent.color = Color.black; tmpAnt.textComponent.fontSize = 11; }
        CreateText(antHeightRow.transform, "Base Height(m)", 11, Color.black, TextAlignmentOptions.Center, new Vector2(110, -20));
        currentY -= 45;

        // Antenna Rover Offset
        CreateLabel(content.transform, "Antenna Rover Offset :", ref currentY);
        GameObject roverOffsetRow = CreateUIElement("RoverOffsetRow", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 15));
        CreateLabelledInput(roverOffsetRow.transform, "X Offset(m)", 20, 200, true);
        CreateLabelledInput(roverOffsetRow.transform, "Y Offset(m)", 240, 200, true);
        CreateLabelledInput(roverOffsetRow.transform, "Z Offset(m)", 460, 200, true);
        currentY -= 60;

        // Input Flight Log (Optional)
        currentY -= 10;
        CreateInputWithBrowse(content.transform, "Input Flight Log (Optional)", "Pixhawk", ref currentY, 30, true);

        // Choose GNSS Satellite
        CreateLabel(content.transform, "Choose GNSS Satellite :", ref currentY);
        GameObject satRow = CreateUIElement("SatRow", content.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, currentY - 5));
        CreateCheckbox(satRow.transform, "GPS", new Vector2(20, 0), true);
        CreateCheckbox(satRow.transform, "GLO", new Vector2(120, 0), true);
        CreateCheckbox(satRow.transform, "Galileo", new Vector2(220, 0), false);
        CreateCheckbox(satRow.transform, "QZSS", new Vector2(320, 0), false);
        CreateCheckbox(satRow.transform, "SBAS", new Vector2(420, 0), false);
        CreateCheckbox(satRow.transform, "BeiDou", new Vector2(520, 0), false);
        currentY -= 35;

        // Input Directory Photo
        currentY -= 10;
        CreateInputWithBrowse(content.transform, "Input Directory Photo :", "", ref currentY, 30, true);

        // Output Directory
        currentY -= 10;
        CreateInputWithBrowse(content.transform, "Output Directory :", "", ref currentY, 30, true);

        // Footer Buttons
        GameObject footer = CreateUIElement("Footer", window.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40));
        GameObject execBtn = CreateSmallButton(footer.transform, "EXECUTE", new Vector2(180, 0), new Vector2(150, 40));
        GameObject cancelBtn = CreateSmallButton(footer.transform, "CANCEL", new Vector2(470, 0), new Vector2(150, 40));

        // Logic
        AttachPopupLogic(root, "PPK + Geotagging");
        
        return root;
    }

    private static void AttachPopupLogic(GameObject root, string title)
    {
        GNSSPopupController controller = root.GetComponent<GNSSPopupController>();
        if (controller == null) controller = root.AddComponent<GNSSPopupController>();

        Button cancelBtn = null;
        Button executeBtn = null;

        // Find buttons by name
        Button[] allButtons = root.GetComponentsInChildren<Button>(true);
        foreach (var btn in allButtons)
        {
            if (btn.name.ToUpper().Contains("CANCEL")) cancelBtn = btn;
            if (btn.name.ToUpper().Contains("EXECUTE")) executeBtn = btn;
        }

        controller.Setup(cancelBtn, executeBtn, title);
    }

    private static GameObject CreateUIElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
    {        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(0, 30);
        obj.GetComponent<Image>().color = Color.clear;
        return obj;
    }

    private static void CreateLabel(Transform parent, string text, ref float y, float size = 12)
    {
        GameObject obj = new GameObject("Label_" + text, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(25, y);
        rt.sizeDelta = new Vector2(-50, 20);
        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = Color.black;
        y -= 25;
    }

    private static void CreateCheckbox(Transform parent, string text, Vector2 pos, bool isChecked = false)
    {
        GameObject row = new GameObject("Checkbox_" + text, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rtRow = row.GetComponent<RectTransform>();
        rtRow.anchorMin = new Vector2(0, 0.5f); rtRow.anchorMax = new Vector2(0, 0.5f);
        rtRow.anchoredPosition = pos;
        rtRow.sizeDelta = new Vector2(100, 25);

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Toggle));
        box.transform.SetParent(row.transform, false);
        RectTransform rtBox = box.GetComponent<RectTransform>();
        rtBox.anchorMin = new Vector2(0, 0.5f); rtBox.anchorMax = new Vector2(0, 0.5f);
        rtBox.anchoredPosition = new Vector2(10, 0);
        rtBox.sizeDelta = new Vector2(16, 16);
        box.GetComponent<Image>().color = Color.white;
        
        Toggle toggle = box.GetComponent<Toggle>();
        toggle.isOn = isChecked;

        GameObject checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkmark.transform.SetParent(box.transform, false);
        RectTransform rtCheck = checkmark.GetComponent<RectTransform>();
        rtCheck.anchorMin = Vector2.zero; rtCheck.anchorMax = Vector2.one;
        rtCheck.sizeDelta = new Vector2(-4, -4);
        checkmark.GetComponent<Image>().color = new Color32(50, 50, 50, 255);
        toggle.graphic = checkmark.GetComponent<Image>();

        CreateText(row.transform, text, 11, Color.black, TextAlignmentOptions.Left, new Vector2(25, 0));
    }

    private static void CreateRadioButton(Transform parent, string text, Vector2 pos, bool isChecked = false)
    {
        GameObject row = new GameObject("Radio_" + text, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rtRow = row.GetComponent<RectTransform>();
        rtRow.anchorMin = new Vector2(0, 0.5f); rtRow.anchorMax = new Vector2(0, 0.5f);
        rtRow.anchoredPosition = pos;
        rtRow.sizeDelta = new Vector2(200, 25);

        GameObject circle = new GameObject("Circle", typeof(RectTransform), typeof(Image));
        circle.transform.SetParent(row.transform, false);
        RectTransform rtCircle = circle.GetComponent<RectTransform>();
        rtCircle.anchorMin = new Vector2(0, 0.5f); rtCircle.anchorMax = new Vector2(0, 0.5f);
        rtCircle.anchoredPosition = new Vector2(10, 0);
        rtCircle.sizeDelta = new Vector2(14, 14);
        circle.GetComponent<Image>().color = Color.white;
        // Ideally use a circle sprite here, but Color.white is fine for now

        if (isChecked) {
            GameObject dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(circle.transform, false);
            RectTransform rtDot = dot.GetComponent<RectTransform>();
            rtDot.anchorMin = Vector2.zero; rtDot.anchorMax = Vector2.one;
            rtDot.offsetMin = new Vector2(4, 4); rtDot.offsetMax = new Vector2(-4, -4);
            dot.GetComponent<Image>().color = Color.black;
        }

        CreateText(row.transform, text, 11, Color.black, TextAlignmentOptions.Left, new Vector2(25, 0));
    }

    private static void CreateText(Transform parent, string text, float size, Color col, TextAlignmentOptions align, Vector2 pos)
    {        GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(1, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(0, size + 5);
        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = col; tmp.alignment = align;
    }

    private static void CreatePlaceholderBox(Transform parent, string text, ref float y, float height, float width = -1, bool relative = false)
    {
        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Button));
        box.transform.SetParent(parent, false);
        RectTransform rt = box.GetComponent<RectTransform>();
        if (!relative) {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(10, y - (height/2));
            rt.sizeDelta = new Vector2(-20, height);
        } else {
            rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(width/2, 0);
            rt.sizeDelta = new Vector2(width, height);
        }
        box.GetComponent<Image>().color = Color.white;
        
        // Add Button feedback
        Button btn = box.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        btn.colors = cb;
        btn.onClick.AddListener(() => Debug.Log("Box clicked: " + text));

        if(text != "") CreateText(box.transform, text, 12, Color.black, TextAlignmentOptions.Left, new Vector2(10, 0));
        if(!relative) y -= (height + 10);
    }

    private static void CreateInputWithBrowse(Transform parent, string label, string rightText, ref float y, float height, bool labelAbove = true)
    {
        GameObject row = new GameObject("Row_" + label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rtRow = row.GetComponent<RectTransform>();
        rtRow.anchorMin = new Vector2(0, 1); rtRow.anchorMax = new Vector2(1, 1);
        rtRow.anchoredPosition = new Vector2(0, y);
        
        float totalHeight = labelAbove ? height + 25 : height;
        rtRow.sizeDelta = new Vector2(0, totalHeight);
        y -= (totalHeight + 10);

        float inputY = labelAbove ? -25 : 0;
        float labelY = labelAbove ? 0 : 0;
        float labelX = labelAbove ? 25 : 25;
        
        // Label
        CreateText(row.transform, label, 12, Color.black, TextAlignmentOptions.Left, new Vector2(labelX, labelY));

        float inputMaxX = (rightText != "") ? 0.75f : 0.93f;
        float inputMinX = labelAbove ? 0.05f : 0.35f;

        // 1. Real Input Field (TMP)
        TMP_DefaultControls.Resources res = GetTMPResources();
        GameObject inputObj = TMP_DefaultControls.CreateInputField(res);
        inputObj.name = "InputField_" + label;
        inputObj.transform.SetParent(row.transform, false);
        RectTransform rtIn = inputObj.GetComponent<RectTransform>();
        rtIn.anchorMin = new Vector2(inputMinX, 0); rtIn.anchorMax = new Vector2(inputMaxX, 1);
        rtIn.offsetMin = new Vector2(5, labelAbove ? 5 : 5); 
        rtIn.offsetMax = new Vector2(-5, labelAbove ? -25 : -5);
        
        Image inputImg = inputObj.GetComponent<Image>();
        if (inputImg != null) inputImg.color = Color.white;
        TMP_InputField tmpInput = inputObj.GetComponent<TMP_InputField>();
        if (tmpInput != null && tmpInput.textComponent != null) {
            tmpInput.textComponent.color = Color.black;
            tmpInput.textComponent.fontSize = 11;
        }

        // 2. Real Dropdown if needed
        if (rightText != "")
        {
            GameObject ddObj = TMP_DefaultControls.CreateDropdown(res);
            ddObj.name = "Dropdown_" + label;
            ddObj.transform.SetParent(row.transform, false);
            RectTransform rtDD = ddObj.GetComponent<RectTransform>();
            rtDD.anchorMin = new Vector2(inputMaxX, 0); rtDD.anchorMax = new Vector2(0.93f, 1);
            rtDD.offsetMin = new Vector2(5, labelAbove ? 5 : 5); 
            rtDD.offsetMax = new Vector2(-5, labelAbove ? -25 : -5);

            TMP_Dropdown dd = ddObj.GetComponent<TMP_Dropdown>();
            dd.options.Clear();
            if (rightText == "POS") {
                dd.options.Add(new TMP_Dropdown.OptionData("POS"));
                dd.options.Add(new TMP_Dropdown.OptionData("Pixhawk"));
            } else {
                dd.options.Add(new TMP_Dropdown.OptionData(rightText));
            }
            
            if (dd.captionText != null) {
                dd.captionText.color = Color.black;
                dd.captionText.fontSize = 11;
            }
        }

        // Browse Button
        GameObject browse = new GameObject("Browse", typeof(RectTransform), typeof(Image), typeof(Button));
        browse.transform.SetParent(row.transform, false);
        RectTransform rtB = browse.GetComponent<RectTransform>();
        rtB.anchorMin = new Vector2(0.93f, 0); rtB.anchorMax = new Vector2(1, 1);
        rtB.offsetMin = new Vector2(2, labelAbove ? 5 : 5); 
        rtB.offsetMax = new Vector2(-2, labelAbove ? -25 : -5);
        browse.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);
        
        GameObject browseText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        browseText.transform.SetParent(browse.transform, false);
        RectTransform rtBt = browseText.GetComponent<RectTransform>();
        rtBt.anchorMin = Vector2.zero; rtBt.anchorMax = Vector2.one;
        TextMeshProUGUI bTmp = browseText.GetComponent<TextMeshProUGUI>();
        bTmp.text = "..."; bTmp.fontSize = 14; bTmp.color = Color.black; bTmp.alignment = TextAlignmentOptions.Center;
    }

    private static void CreateLabelledInput(Transform parent, string label, float x, float width, bool labelBelow = false)
    {
        GameObject obj = new GameObject(label, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x + (width / 2), 0);
        rt.sizeDelta = new Vector2(width, 50);

        // 1. Input Field (TMP)
        TMP_DefaultControls.Resources res = GetTMPResources();
        GameObject inputObj = TMP_DefaultControls.CreateInputField(res);
        inputObj.name = "InputField_" + label;
        inputObj.transform.SetParent(obj.transform, false);
        RectTransform rtIn = inputObj.GetComponent<RectTransform>();
        rtIn.anchorMin = new Vector2(0, 0.5f); rtIn.anchorMax = new Vector2(1, 0.5f);
        rtIn.anchoredPosition = Vector2.zero; rtIn.sizeDelta = new Vector2(0, 25);

        Image inputImg = inputObj.GetComponent<Image>();
        if (inputImg != null) inputImg.color = Color.white;
        TMP_InputField tmpInput = inputObj.GetComponent<TMP_InputField>();
        if (tmpInput != null && tmpInput.textComponent != null) {
            tmpInput.textComponent.color = Color.black;
            tmpInput.textComponent.fontSize = 11;
            tmpInput.textComponent.alignment = TextAlignmentOptions.Center;
        }

        // Label Text
        Vector2 labelPos = labelBelow ? new Vector2(0, -20) : new Vector2(0, 20);
        CreateText(obj.transform, label, 11, Color.black, TextAlignmentOptions.Center, labelPos);
    }

    private static GameObject CreateSmallButton(Transform parent, string text, Vector2 pos, Vector2 size = default)
    {        if(size == default) size = new Vector2(100, 30);
        GameObject btn = new GameObject("Button_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
        btn.transform.SetParent(parent, false);
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(0, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        btn.GetComponent<Image>().color = new Color32(230, 230, 230, 255);
        CreateText(btn.transform, text, 12, Color.black, TextAlignmentOptions.Center, Vector2.zero);
        return btn;
    }
}

public class GNSSDataReaderUI : MonoBehaviour
{
    private TMP_InputField pathInput;
    private Button browseBtn;
    private Button executeBtn;

    public void Setup(TMP_InputField input, Button browse, Button execute)
    {
        pathInput = input;
        browseBtn = browse;
        executeBtn = execute;

        browseBtn.onClick.AddListener(OnBrowse);
        executeBtn.onClick.AddListener(OnExecute);
    }

    private void OnBrowse()
    {
        // Placeholder for file browser
        Debug.Log("Browse for .ubx file");
    }

    private void OnExecute()
    {
        Debug.Log("Executing GNSS Data Reader for: " + pathInput.text);
    }
}
