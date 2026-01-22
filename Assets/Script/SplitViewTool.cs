using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Experimental.GraphView.GraphView;

public class SplitViewTool : MonoBehaviour
{
    public TiffLayerManager tiffManager;
    public ProjectManager projectManager;
    public TMP_Dropdown dropdownLeft, dropdownRight;

    string layerNameLeft, layerNameRight;
    GameObject instLeft, instRight;

    public RectTransform layerManager, masker;
  public GameObject divider; //staticer can be removed, masker repurposed
    Vector4 lastRect;
    public Slider sliceSlider;

    void Start()
    {
        layerManager=layerManager.GetComponent<RectTransform>();
        masker=masker.GetComponent<RectTransform>();
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

    public void SplitviewApply()
    {
        layerNameLeft = dropdownLeft.options[dropdownLeft.value].text;
        layerNameRight = dropdownRight.options[dropdownRight.value].text;
        tiffManager.OnPropertyToggleExternal(layerNameLeft, true);
        tiffManager.OnPropertyToggleExternal(layerNameRight, true);
        instLeft = tiffManager.SelectLayerGameobject(layerNameLeft);
        instRight = tiffManager.SelectLayerGameobject(layerNameRight);

     //  instLeft.SetActive(true);
     //   instRight.SetActive(true);

        instRight.transform.SetAsLastSibling();
        instLeft.transform.SetAsLastSibling();

        EnableMask(true);
    }

    public void SplitviewCancel()
    {
        Destroy(instLeft);
        Destroy(instRight);

        EnableMask(false);
    }

    public void EnableMask(bool enabled)
    {
        //     if (!image || !image.material) return;
        RawImage layerLeftImage = instLeft.GetComponent<RawImage>();
        layerLeftImage.material.SetFloat("_UseClip", enabled ? 1f : 0f);
    }

    public void SplitviewDragging()
    {
        float maxWidth = layerManager.rect.size.x;
        float maskSize = sliceSlider.value;
        masker.sizeDelta = new Vector2(maskSize * maxWidth, layerManager.sizeDelta.y);

        RawImage layerLeftImage = instLeft.GetComponent<RawImage>();
        RectTransform maskRect = masker.GetComponent<RectTransform>();

        //   if (!maskRect || !layerLeftImage || !layerLeftImage.material) return;
        Vector3[] corners = new Vector3[4];
        maskRect.GetWorldCorners(corners);

        Vector4 clipRect = new Vector4(
            corners[0].x, // minX
            corners[0].y, // minY
            corners[2].x, // maxX
            corners[2].y  // maxY
        );

        // Avoid redundant material updates
        if (clipRect != lastRect)
        {
            layerLeftImage.material.SetVector("_ClipRect", clipRect);
            lastRect = clipRect;
        }
    }
}