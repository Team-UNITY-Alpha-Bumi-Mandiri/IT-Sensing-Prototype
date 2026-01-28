using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using static UnityEditor.Experimental.GraphView.GraphView;

public class SplitViewTool : MonoBehaviour
{
    [Header("Data")]
    public TiffLayerManager tiffManager;
    public ProjectManager projectManager;
    public TMP_Dropdown dropdownLeft, dropdownRight;

    string layerNameLeft, layerNameRight;
    GameObject instLeft, instRight;

    [Header("Toolbar")]
    public GameObject divider;
    public RectTransform layerManager, masker;
    Vector4 lastRect;

    [Header("Masking")]
    public Slider sliceSlider;
    public GameObject sliderHandle;
    Vector3[] corners = new Vector3[4];

    static readonly int MaskRectID = Shader.PropertyToID("_MaskRect");
    bool maskingActive;

    void Start()
    {
        layerManager = layerManager.GetComponent<RectTransform>();
        masker = masker.GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        if (maskingActive)
        {
            RawImage layerLeftImage = instLeft.GetComponent<RawImage>();
            RectTransform maskRect = masker.GetComponent<RectTransform>();

            //Masking
            if (!maskRect || !layerLeftImage)
                return;

            Vector3[] corners = new Vector3[4];
            maskRect.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

            layerLeftImage.material.SetVector(
                MaskRectID,
                new Vector4(min.x, min.y, max.x, max.y)
            );
        }
    }

    public void UpdateDropdownOptions()
    {
        var currentProject = projectManager.GetCurrentProject();
        if (currentProject != null)// && currentProjectName != oldProjectName)
        {
            List<string> layerNameOptions = new List<string>();
            layerNameOptions.Add("-");
            foreach (var prop in currentProject.properties)
            {
                layerNameOptions.Add(prop.key);
            }

            dropdownLeft.ClearOptions();
            dropdownLeft.AddOptions(layerNameOptions);

            dropdownRight.ClearOptions();
            dropdownRight.AddOptions(layerNameOptions);
        }
    }

    public void Splitview_Apply()
    {
        layerNameLeft = dropdownLeft.options[dropdownLeft.value].text;
        layerNameRight = dropdownRight.options[dropdownRight.value].text;
        tiffManager.OnPropertyToggleExternal(layerNameLeft, true);
        tiffManager.OnPropertyToggleExternal(layerNameRight, true);
        instLeft = tiffManager.SelectLayerGameobject(layerNameLeft);
        instRight = tiffManager.SelectLayerGameobject(layerNameRight);

        instRight.transform.SetAsLastSibling();
        instLeft.transform.SetAsLastSibling();

        EnableMask(true);
        divider.SetActive(true);
        sliceSlider.gameObject.SetActive(true);
    }

    public void Splitview_Cancel()
    {
        if (instLeft != null)
        {
            EnableMask(false);
            tiffManager.OnPropertyToggleExternal(layerNameLeft, false);
            tiffManager.OnPropertyToggleExternal(layerNameRight, false);
        }

        divider.SetActive(false);
        sliceSlider.gameObject.SetActive(false);
    }

    public void EnableMask(bool enabled)
    {
        RawImage layerLeftImage = instLeft.GetComponent<RawImage>();
        if (enabled)
            layerLeftImage.material.EnableKeyword("UI_MASK");
        else
            layerLeftImage.material.DisableKeyword("UI_MASK");

        maskingActive = enabled;
    }

    public void Splitview_Dragging()
    {
        RawImage layerLeftImage = instLeft.GetComponent<RawImage>();
        RectTransform maskRect = masker.GetComponent<RectTransform>();

        //==> Using sliderHandle-to-sliderParent ratio to use with mask-to-layerManager ratio
        Vector2 sourcePos = sliderHandle.transform.localPosition;
        RectTransform sliderRt = sliceSlider.GetComponent<RectTransform>();
        float sliderWidth = sliderRt.rect.size.x;
        float sourceRatio = sourcePos.x / sliderWidth;
        float containerWidth = layerManager.rect.size.x;
        maskRect.sizeDelta = new Vector2(sourceRatio * containerWidth + containerWidth / 2, layerManager.rect.size.y);

        divider.transform.localPosition = new Vector2(sourcePos.x, divider.transform.localPosition.y);
    }
}