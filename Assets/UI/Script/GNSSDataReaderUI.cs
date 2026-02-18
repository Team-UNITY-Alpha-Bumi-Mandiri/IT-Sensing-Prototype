using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        Debug.Log("Browse for .ubx file");
    }

    private void OnExecute()
    {
        Debug.Log("Executing GNSS Data Reader for: " + pathInput.text);
    }
}

