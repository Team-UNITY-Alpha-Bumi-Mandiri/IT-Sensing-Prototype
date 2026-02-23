using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using static UnityEditor.Experimental.GraphView.GraphView;

public class FauxSplitView : MonoBehaviour
{
    [Header("Data")]
    public TiffLayerManager tiffManager;
    public ProjectManager projectManager;
    public TMP_Dropdown dropdownLeft, dropdownRight;//

    string layerNameLeft, layerNameRight;//
    GameObject instLeft, instRight;

    [Header("Toolbar")]
  //  public GameObject divider;
    public RectTransform layerManager, masker;
    Vector4 lastRect;

    [Header("Masking")]
 //   public Slider sliceSlider;
  //  public GameObject sliderHandle;
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
                    }
    }

    public void Splitview_Apply()
    {
        tiffManager.OnPropertyToggleExternal(layerNameLeft, true);
        tiffManager.OnPropertyToggleExternal(layerNameRight, true);
        instLeft = tiffManager.SelectLayerGameobject(layerNameLeft);
        instRight = tiffManager.SelectLayerGameobject(layerNameRight);

        instRight.transform.SetAsLastSibling();
        instLeft.transform.SetAsLastSibling();

        EnableMask(true);
    }

    public void Splitview_Cancel()
    {
        if (instLeft != null)
        {
            EnableMask(false);
            tiffManager.OnPropertyToggleExternal(layerNameLeft, false);
            tiffManager.OnPropertyToggleExternal(layerNameRight, false);
        }
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
    }