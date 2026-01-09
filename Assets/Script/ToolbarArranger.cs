using UnityEngine;
using UnityEngine.UI;

public class ToolbarArranger : MonoBehaviour
{
    public bool isForHeader, startForHeader;
    public GameObject[] childGroup;
    RectTransform selfRt;
    float areaWidth;
    float padding = 1;

    //for header
    float accumulatedDistance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        selfRt = GetComponent<RectTransform>();
        Arrange();
    }

    // Update is called once per frame
    void Update()
    {
    }

    void Arrange()
    {
        if (!isForHeader)
        {
            int childCount = transform.childCount;
            childGroup = new GameObject[transform.childCount-1];

            // Add children Buttons into array (minus the first Child)
            for (int i = 1; i < childCount; i++)
                childGroup[i - 1] = transform.GetChild(i).gameObject;

            // Get the width of button
            RectTransform buttonRt = childGroup[0].GetComponent<RectTransform>();
            float buttonWidth = buttonRt.sizeDelta.x;

            //set the header width
            areaWidth = (buttonWidth * childGroup.Length) + (padding * (childGroup.Length + 1));
            selfRt.sizeDelta = new Vector2(areaWidth, selfRt.sizeDelta.y);

            //set the button position
            float leftEdge = -(areaWidth / 2);
            for (int i = 0; i < childGroup.Length; i++)
                childGroup[i].transform.localPosition = new Vector2(leftEdge + (padding + buttonWidth / 2 + i * (padding + buttonWidth)), -28);
        }
        else 
        {
            int childCount = 7;
            childGroup = new GameObject[childCount];

            // Add children Categories into array (minus the first Child & certain array length only)
            for (int i = 1; i <= childCount; i++)
                childGroup[i - 1] = transform.GetChild(i).gameObject;

            //set the category header position
            float rightEdge =transform.position.x+( selfRt.sizeDelta.x / 2);
            for (int i = childGroup.Length - 1; i >= 0; i--)
            {
                RectTransform categRt = childGroup[i].GetComponent<RectTransform>();
                float categoryWidth = categRt.sizeDelta.x;

                float categEndPos = categoryWidth / 2 + padding + accumulatedDistance;

                accumulatedDistance += categoryWidth + padding;

                /*  if (i < buttonGroup.Length - 2) // if its not the right-most Category
  {
      for (int j = buttonGroup.Length - 1; j > i; j--) // count the width of every Categories to the right of this Category.
      {
          //get the width of the category header
          RectTransform categRt = buttonGroup[j].GetComponent<RectTransform>();
          float categoryWidth = categRt.sizeDelta.x;
          accumulatedDistance += categoryWidth + padding;
      }
  }*/

                childGroup[i].transform.localPosition = new Vector2(rightEdge - categEndPos, 19);
            }
        }
    }
}