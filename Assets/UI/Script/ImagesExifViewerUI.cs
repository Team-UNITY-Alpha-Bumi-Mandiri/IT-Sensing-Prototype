using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ImagesExifViewerUI : MonoBehaviour
{
    private TMP_InputField pathInput;
    private Button browseButton;
    private RawImage previewImage;
    private TextMeshProUGUI infoText;

    public void Setup(TMP_InputField input, Button browse, RawImage preview, TextMeshProUGUI info)
    {
        pathInput = input;
        browseButton = browse;
        previewImage = preview;
        infoText = info;

        browseButton.onClick.AddListener(OnBrowse);
    }

    private void OnBrowse()
    {
        Debug.Log("Browse image for Exif Viewer: " + pathInput.text);
    }
}

