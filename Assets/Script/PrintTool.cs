using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Timeline;

public class PrintTool : MonoBehaviour
{
    public RectTransform mapWindow;
    public List<Vector2> paperDimension; //in milimeters
    public int template, paperSize, layout, scale;
    float dpi;
    bool mapDragging;
    Vector3 cursorOffset;

    void Start()
    {
        dpi = Screen.dpi;
        if (dpi == 0)
            dpi = 96;
        Debug.Log("DPI is = " + dpi);

        paperDimension = new List<Vector2>(5);
        paperDimension.Add(new Vector2(297, 420));
        paperDimension.Add(new Vector2(210, 297));
        paperDimension.Add(new Vector2(148, 210));
        paperDimension.Add(new Vector2(215.9f, 279.4f));
        paperDimension.Add(new Vector2(216, 356));

        paperSize = 1;
        layout = 0;

        SetMapWindow();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            cursorOffset = mapWindow.transform.position - Input.mousePosition;
            mapDragging = true;
        }

        if(Input.GetMouseButtonUp(1))
            mapDragging = false;

        if (mapDragging)
        {
            Vector3 offset3=new Vector3(cursorOffset.x,cursorOffset.y,mapWindow.transform.position.z);
            mapWindow.transform.position = Input.mousePosition + offset3;
        }
    }

    public void Print_Paper(TMP_Dropdown selectObj)
    {
        paperSize = selectObj.value;
        SetMapWindow();
    }

    public void Print_Layout(TMP_Dropdown selectOb)
    {
        layout = selectOb.value;
        SetMapWindow();
    }

    void SetMapWindow()
    {
        Vector2 paperPixelSize = new Vector2(
            (paperDimension[paperSize].x/10) * (dpi / 2.54f),
            (paperDimension[paperSize].y/10 )* (dpi / 2.54f));

        Vector2 pixelSizeWithLayout;
        if (layout == 0) //landscape
            pixelSizeWithLayout = new Vector2(paperPixelSize.y, paperPixelSize.x);
        else
            pixelSizeWithLayout = paperPixelSize;

        mapWindow.sizeDelta = pixelSizeWithLayout;
       Debug.Log(paperDimension[paperSize].ToString());
    }
}