using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UI;

public class PrintTool : MonoBehaviour
{
    public RectTransform mapWindow;
    public GameObject objectContainer, standardObjectWindow,compassWindow;
     int template, paperSize, layout, scale;
    bool mapDragging;
    Vector3 cursorOffset;

    //Paper
     List<Vector2> paperDimension; //in milimeters
    float dpi;
    GameObject selectedObject,addedCompass;

    [Header("Add Object")]  //standar object
    public GameObject[] standardShapes;
    public GameObject compassPref;
    public Sprite[] compassTypes;

    void Start()
    {
        dpi = Screen.dpi;
        if (dpi == 0)
            dpi = 96;
       // Debug.Log("DPI is = " + dpi);

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

    public void Print_ImageAdd(TMP_Dropdown selectOb)
    {
        switch (selectOb.value)
        {
            case 0:
                standardObjectWindow.SetActive(true);
                break;

            case 1:
                compassWindow.SetActive(true);
                break;
        }
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
//       Debug.Log(paperDimension[paperSize].ToString());
    }

    public void AddObject_StandardShapes(int shapeIndex)
    {
        selectedObject = Instantiate(standardShapes[shapeIndex], objectContainer.transform);
    }

    public void AddObject_Compass(int shapeIndex)
    {
        if (addedCompass != null)
            addedCompass = Instantiate(compassPref, objectContainer.transform);
       
            Image compassImg = addedCompass.GetComponent<Image>();
            compassImg.sprite = compassTypes[shapeIndex];
       }
}