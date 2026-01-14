using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NUnit.Compatibility;

public class EnhancementTool : MonoBehaviour
{
    public Slider[] enhancementSliders;
    public TMP_Text layerName, conTxt, briTxt, satTxt, statusTxt;
    Material mat;
    float con, bri, sat;

    public void AssignValues(GameObject layer, string layerNm)
    {
        if (layer != null)
        {
            RawImage layerImage;
            layerImage = layer.GetComponent<RawImage>();
            mat = layerImage.material;
        }

        layerName.text = new string("Layer : " + layerNm);
        con = mat.GetFloat("_Contrast");
        bri = mat.GetFloat("_Brightness");
        sat = mat.GetFloat("_Saturation");
        statusTxt.text = "Image loaded - Ready for enhacement";
    }

    public void Change_Contrast()
    {
        con = enhancementSliders[0].value * (2 / enhancementSliders[0].maxValue);
        ApplyChange();
        conTxt.text = (enhancementSliders[0].value - 100).ToString();
    }

    public void Change_Brightness()
    {
        bri = (enhancementSliders[1].value * (2 / enhancementSliders[1].maxValue)) - 1;
        ApplyChange();
        briTxt.text = (enhancementSliders[1].value - 100).ToString();
    }

    public void Change_Saturation()
    {
        sat = enhancementSliders[2].value * (2 / enhancementSliders[2].maxValue);
        ApplyChange();
        satTxt.text = (enhancementSliders[2].value - 100).ToString();
    }

    void ApplyChange()
    {
        mat.SetFloat("_Contrast", con);
        mat.SetFloat("_Brightness", bri);
        mat.SetFloat("_Saturation", sat);

        statusTxt.text = "Live preview active";
    }

    public void Reset_Enhancement()
    {
        mat.SetFloat("_Contrast", 1);
        mat.SetFloat("_Brightness", 0);
        mat.SetFloat("_Saturation", 1);

        foreach (Slider slii in enhancementSliders)
            slii.value = 100;

        conTxt.text = "0";
        briTxt.text = "0";
        satTxt.text = "0";

        statusTxt.text = "Reset to original";
    }
}