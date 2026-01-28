using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Tooltip : MonoBehaviour
{
    public GameObject tooltipPref, tooltipParent;
    GameObject tooltipInst;
    public string objName;
    Vector2 mouseLastPos, tooltipOffset;
    float pause = .5f;
    float mouseLastMovement;
    bool isMoving, isTippable;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mouseLastPos = Input.mousePosition;
        tooltipOffset = new Vector2(20, -20);
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 mouseCurrentPos = Input.mousePosition;

        //mouse starts moving
        if (mouseCurrentPos != mouseLastPos)
        {
            mouseLastMovement = Time.time;
            if (!isMoving)
            {
                isMoving = true;
                isTippable = false;
                if (tooltipInst != null) Destroy(tooltipInst);
            }
        }
        else
        {
            if (isMoving && Time.time - mouseLastMovement > pause)
            {
                isMoving = false;
                OnMouseStoppedMoving(mouseCurrentPos);
            }
        }
        mouseLastPos = mouseCurrentPos;
    }

    void OnMouseStoppedMoving(Vector2 pos)
    {
        //raycasting
        var eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        //get object name
        foreach (RaycastResult r in results)
        {
            GameObject go = r.gameObject;
            if (go.layer == 5 && go.CompareTag("ToolButton"))
            {
                name = r.gameObject.name;
                isTippable = true;
            }
        }

        if (isTippable)
        {
            tooltipInst = Instantiate(tooltipPref, pos, Quaternion.identity, tooltipParent.transform);
            tooltipInst.GetComponentInChildren<TMP_Text>().text = name;

            RectTransform tipRt = tooltipInst.GetComponent<RectTransform>();
            Vector2 offset;

            if (Input.mousePosition.x < 1720)
            {
                tipRt.pivot = new Vector2(0, 1);
                offset = tooltipOffset;
            }
            else
            {
                tipRt.pivot = new Vector2(1, 1);
                offset = new Vector2(-1 * tooltipOffset.x, tooltipOffset.y);
            }

            tooltipInst.transform.position = pos + offset;
        }
    }
}