using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Tooltip : MonoBehaviour
{
    public GameObject tooltipPref, tooltipParent, toolbarAtas;
    GameObject tooltipInst;
    string objName;
    Vector2 mouseLastPos, tooltipOffset;
    float pause = .5f;
    float mouseLastMovement;
    bool isMoving;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mouseLastPos = Input.mousePosition;
        tooltipOffset = new Vector2(30, -30);
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 mouseCurrentPos = Input.mousePosition;

        //raycasting
        var eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        //get object name
        if (results.Count!=0&& results[0].gameObject.layer == 5 && results[0].gameObject.GetComponentInParent<Button>()!=null)
        {
            objName = results[0].gameObject.transform.parent.gameObject.name;
        }

        //mouse starts moving
        if (mouseCurrentPos != mouseLastPos)
        {
            mouseLastMovement = Time.time;
            if (!isMoving)
            {
                isMoving = true;

                if (tooltipInst != null)
                    Destroy(tooltipInst);
            }
        }
        else
        {
            if (isMoving && Time.time - mouseLastMovement > pause)
            {
                isMoving = false;

                GameObject parentOfParent = results[0].gameObject.transform.parent.gameObject.transform.parent.gameObject.transform.parent.gameObject   ;
              //  Debug.Log("going? "+parentOfParent+";;; object is = " + results[0]);
                if (parentOfParent == toolbarAtas)
                {
                    Vector2 tooltipPos = mouseCurrentPos + tooltipOffset;
                    OnMouseStoppedMoving(objName, tooltipPos);
                }
            }
        }
        mouseLastPos = mouseCurrentPos;
    }

    void OnMouseStoppedMoving(string name, Vector2 offset)
    {
        tooltipInst = Instantiate(tooltipPref, offset, Quaternion.identity, tooltipParent.transform);
        tooltipInst.GetComponentInChildren<TMP_Text>().text = name;
    }
}