using UnityEngine;
using UnityEngine.UI;

public class ToolbarArranger : MonoBehaviour
{
    GameObject[] buttonGroup;
    RectTransform selfRt;
    float areaWidth;
    float padding = 1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        selfRt = GetComponent<RectTransform>();
        buttonGroup = new GameObject[transform.childCount - 1];
        Arrange();
    }

    // Update is called once per frame
    void Update()
    {
    }

    void Arrange()
    {
        int childCount = transform.childCount;

        //add children Buttons into array
        for (int i = 1; i < childCount; i++)
            buttonGroup[i - 1] = transform.GetChild(i).gameObject;

        //get the width of button
        RectTransform buttonRt = buttonGroup[0].GetComponent<RectTransform>();
        float buttonWidth = buttonRt.sizeDelta.x;

        //set the header width
        areaWidth = (buttonWidth * buttonGroup.Length) + (padding * (buttonGroup.Length + 1));
        selfRt.sizeDelta = new Vector2(areaWidth, selfRt.sizeDelta.y);

        //set the button position
        float leftEdge = -(areaWidth / 2);
        for (int i = 0; i < buttonGroup.Length; i++)
            buttonGroup[i].transform.localPosition = new Vector2(leftEdge + (padding + buttonWidth / 2 + i * (padding + buttonWidth)), -28);
    }
}