using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MeasureSimple : MonoBehaviour
{
    public RectTransform mapPanel;        // Panel_Map (yang punya RectMask2D)
    public RectTransform measureObjects;  // MeasureObjects (anak Panel_Map)
    public GameObject pointPrefab;
    public GameObject segmentPrefab;
    public GameObject labelPrefab;

    private List<RectTransform> points = new List<RectTransform>();
    private List<RectTransform> segments = new List<RectTransform>();
    private RectTransform previewSegment;

    public float lineThickness = 3f;
    public Vector2 labelOffset = new Vector2(25, 25);

    private bool active = false;

    void Start()
    {
        GameObject p = Instantiate(segmentPrefab, measureObjects);
        previewSegment = p.GetComponent<RectTransform>();
        previewSegment.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!active) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // hanya jika mouse di map
        if (!RectTransformUtility.RectangleContainsScreenPoint(mapPanel, mousePos))
        {
            previewSegment.gameObject.SetActive(false);
            return;
        }

        // klik kiri → tambahkan titik
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            AddPoint(mousePos);
        }

        if (points.Count > 0)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                measureObjects, mousePos, null, out Vector2 localMouse);

            RectTransform last = points[points.Count - 1];
            UpdateSegment(previewSegment, last.anchoredPosition, localMouse);
            previewSegment.gameObject.SetActive(true);
        }
    }

    void AddPoint(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            measureObjects, screenPos, null, out Vector2 posLocal);

        // titik
        GameObject p = Instantiate(pointPrefab, measureObjects);
        RectTransform pt = p.GetComponent<RectTransform>();
        pt.anchoredPosition = posLocal;
        points.Add(pt);

        // label
        GameObject l = Instantiate(labelPrefab, measureObjects);
        RectTransform lb = l.GetComponent<RectTransform>();
        lb.anchoredPosition = posLocal + labelOffset;
        lb.GetComponent<Text>().text = points.Count.ToString();
        // (nanti bisa diganti jarak, ini hanya placeholder)
        
        // segmen permanen
        if (points.Count >= 2)
        {
            RectTransform a = points[points.Count - 2];
            RectTransform b = points[points.Count - 1];

            GameObject s = Instantiate(segmentPrefab, measureObjects);
            RectTransform sr = s.GetComponent<RectTransform>();
            UpdateSegment(sr, a.anchoredPosition, b.anchoredPosition);
            segments.Add(sr);
        }
    }

    void UpdateSegment(RectTransform seg, Vector2 a, Vector2 b)
    {
        Vector2 dir = b - a;
        float length = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        seg.sizeDelta = new Vector2(length, lineThickness);
        seg.anchoredPosition = a + dir * 0.5f;
        seg.localEulerAngles = new Vector3(0, 0, angle);
    }

    public void ToggleMeasure()
    {
        if (!active)
        {
            Clear();
            active = true;
        }
        else
        {
            Clear();
            active = false;
        }
    }

    void Clear()
    {
        foreach (var p in points) Destroy(p.gameObject);
        foreach (var s in segments) Destroy(s.gameObject);

        points.Clear();
        segments.Clear();

        previewSegment.gameObject.SetActive(false);
    }
}
