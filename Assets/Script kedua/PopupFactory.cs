using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class PopupFactory
{
    // ==========================================================================================
    // TMP RESOURCES — versi Unity & TMP terbaru
    // (CreateDropdown hanya butuh sprite STANDARD)
    // ==========================================================================================
    private static TMP_DefaultControls.Resources GetTMPResources()
{
    TMP_DefaultControls.Resources res = new TMP_DefaultControls.Resources();

    // Biarkan null karena Unity 2023/2024 tidak menyediakan sprite default melalui TMP Settings
    res.standard = null;
    res.background = null;
    res.inputField = null;

    return res;
}


    // ==========================================================================================
    // CREATE POPUP
    // ==========================================================================================
    public static GameObject CreatePopup(Canvas canvas = null)
    {
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();  // FIX API UNITY BARU

        // ======================================================================================
        // PANEL POPUP
        // ======================================================================================
        GameObject popup = new GameObject("Popup_NewProject",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        popup.transform.SetParent(canvas.transform, false);

        RectTransform rtPopup = popup.GetComponent<RectTransform>();
        rtPopup.anchorMin = rtPopup.anchorMax = new Vector2(0.5f, 0.5f);
        rtPopup.sizeDelta = new Vector2(600, 300);

        popup.GetComponent<Image>().color = Color.white;

        // ======================================================================================
        // CONTENT WRAPPER
        // ======================================================================================
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(popup.transform, false);

        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = Vector2.zero;
        rtContent.anchorMax = Vector2.one;
        rtContent.offsetMin = new Vector2(20, 20);
        rtContent.offsetMax = new Vector2(-20, -20);

        // ======================================================================================
        // HEADER HIJAU
        // ======================================================================================
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(content.transform, false);

        RectTransform rtHeader = header.GetComponent<RectTransform>();
        rtHeader.anchorMin = new Vector2(0, 1);
        rtHeader.anchorMax = new Vector2(1, 1);
        rtHeader.sizeDelta = new Vector2(0, 55);

        header.GetComponent<Image>().color = new Color32(15, 138, 44, 255);

        // ======================================================================================
        // TEXT TITLE TMP
        // ======================================================================================
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(header.transform, false);

        RectTransform rtTitle = titleObj.GetComponent<RectTransform>();
        rtTitle.anchorMin = rtTitle.anchorMax = new Vector2(0f, 0.5f);
        rtTitle.anchoredPosition = new Vector2(20, 0);

        TextMeshProUGUI titleTMP = titleObj.GetComponent<TextMeshProUGUI>();
        titleTMP.text = "New Project";
        titleTMP.fontSize = 28;
        titleTMP.color = Color.white;

        // ======================================================================================
        // BUTTON CLOSE
        // ======================================================================================
        GameObject closeBtn = new GameObject("Button_Close",
            typeof(RectTransform), typeof(Button), typeof(Image));
        closeBtn.transform.SetParent(header.transform, false);

        RectTransform rtClose = closeBtn.GetComponent<RectTransform>();
        rtClose.anchorMin = rtClose.anchorMax = new Vector2(1, 0.5f);
        rtClose.sizeDelta = new Vector2(40, 40);
        rtClose.anchoredPosition = new Vector2(-20, 0);

        closeBtn.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        // X text
        GameObject xText = new GameObject("Text_X", typeof(TextMeshProUGUI));
        xText.transform.SetParent(closeBtn.transform, false);

        TextMeshProUGUI xTMP = xText.GetComponent<TextMeshProUGUI>();
        xTMP.text = "X";
        xTMP.fontSize = 32;
        xTMP.color = Color.white;
        xTMP.alignment = TextAlignmentOptions.Center;

        // ======================================================================================
        // LABEL TYPE
        // ======================================================================================
        GameObject labelType = new GameObject("Label_Type", typeof(TextMeshProUGUI));
        labelType.transform.SetParent(content.transform, false);

        RectTransform rtLabelType = labelType.GetComponent<RectTransform>();
        rtLabelType.anchorMin = rtLabelType.anchorMax = new Vector2(0f, 1f);
        rtLabelType.anchoredPosition = new Vector2(0, -80);

        TextMeshProUGUI typeTMP = labelType.GetComponent<TextMeshProUGUI>();
        typeTMP.text = "Type";
        typeTMP.fontSize = 22;
        typeTMP.color = Color.black;

        // ======================================================================================
        // DROPDOWN TMP (FIX RESOURCES)
        // ======================================================================================
        TMP_DefaultControls.Resources res = GetTMPResources();
        GameObject ddObj = TMP_DefaultControls.CreateDropdown(res);
        ddObj.name = "Dropdown_Type";
        ddObj.transform.SetParent(content.transform, false);

        RectTransform rtDD = ddObj.GetComponent<RectTransform>();
        rtDD.anchorMin = rtDD.anchorMax = new Vector2(0, 1);
        rtDD.anchoredPosition = new Vector2(0, -120);
        rtDD.sizeDelta = new Vector2(300, 40);

        TMP_Dropdown dd = ddObj.GetComponent<TMP_Dropdown>();
        dd.options.Clear();
        dd.options.Add(new TMP_Dropdown.OptionData("Option 1"));
        dd.options.Add(new TMP_Dropdown.OptionData("Option 2"));

        // ======================================================================================
        // LABEL OUTPUT
        // ======================================================================================
        GameObject labelOut = new GameObject("Label_Output", typeof(TextMeshProUGUI));
        labelOut.transform.SetParent(content.transform, false);

        RectTransform rtLabelOut = labelOut.GetComponent<RectTransform>();
        rtLabelOut.anchorMin = rtLabelOut.anchorMax = new Vector2(0f, 1f);
        rtLabelOut.anchoredPosition = new Vector2(0, -180);

        TextMeshProUGUI outTMP = labelOut.GetComponent<TextMeshProUGUI>();
        outTMP.text = "Output";
        outTMP.fontSize = 22;
        outTMP.color = Color.black;

        // ======================================================================================
        // INPUT FIELD TMP
        // ======================================================================================
        GameObject inputObj = TMP_DefaultControls.CreateInputField(res);
        inputObj.name = "Input_Output";
        inputObj.transform.SetParent(content.transform, false);

        RectTransform rtInput = inputObj.GetComponent<RectTransform>();
        rtInput.anchorMin = rtInput.anchorMax = new Vector2(0, 1);
        rtInput.anchoredPosition = new Vector2(0, -220);
        rtInput.sizeDelta = new Vector2(300, 40);

        TMP_InputField inputField = inputObj.GetComponent<TMP_InputField>();
        inputField.text = "";

        // ======================================================================================
        // SUBMIT BUTTON
        // ======================================================================================
        GameObject btnObj = new GameObject("Button_Submit",
            typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(content.transform, false);

        RectTransform rtBtn = btnObj.GetComponent<RectTransform>();
        rtBtn.anchorMin = rtBtn.anchorMax = new Vector2(1, 0);
        rtBtn.anchoredPosition = new Vector2(-20, 20);
        rtBtn.sizeDelta = new Vector2(120, 45);

        btnObj.GetComponent<Image>().color = new Color32(0, 123, 255, 255);

        GameObject btnText = new GameObject("Text_Submit", typeof(TextMeshProUGUI));
        btnText.transform.SetParent(btnObj.transform, false);

        TextMeshProUGUI submitTMP = btnText.GetComponent<TextMeshProUGUI>();
        submitTMP.text = "Submit";
        submitTMP.fontSize = 22;
        submitTMP.color = Color.white;
        submitTMP.alignment = TextAlignmentOptions.Center;

        return popup;
    }
}
