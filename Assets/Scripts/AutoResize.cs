using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoResize : MonoBehaviour
{
    float menuSize;
    int buttonCount;
        
    // Start is called before the first frame update
    void Start()
    {
        buttonCount = transform.childCount - 1;
        menuSize = (25 * buttonCount) + (1 * (buttonCount + 1));
        RectTransform rectTransform = GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(menuSize, 40);


    }

    // Update is called once per frame
    void Update()
    {
    }
}