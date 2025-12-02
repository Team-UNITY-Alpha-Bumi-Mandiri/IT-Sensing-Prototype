using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class LineController : MonoBehaviour
{
    [Header("Distance Label")]
    [SerializeField] private GameObject distanceLabelPrefab;
    [SerializeField] private float labelVerticalOffset = 0.2f;

    private LineRenderer lr;
    private List<Transform> points;
    private readonly List<DistanceLabel> distanceLabels = new List<DistanceLabel>();

    private class DistanceLabel
    {
        public Transform point;
        public TMP_Text text;
    }

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 0;
        points = new List<Transform>();
    }

    public void AddDot(Transform point)
    {
        lr.positionCount++;
        points.Add(point);

        TryCreateDistanceLabel();
    }

    private void LateUpdate()
    {
        if (points.Count >= 2)
        {
            for (int i = 0; i < points.Count; i++)
            {
                lr.SetPosition(i, points[i].position);
            }
        }

        UpdateDistanceLabels();
    }

    private void TryCreateDistanceLabel()
    {
        if (distanceLabelPrefab == null || points.Count < 2)
        {
            return;
        }

        Transform currentPoint = points[points.Count - 1];

        GameObject labelObj = Instantiate(distanceLabelPrefab, currentPoint);
        TMP_Text tmpLabel = labelObj.GetComponent<TMP_Text>();
        if (tmpLabel == null)
        {
            Debug.LogWarning("Distance label prefab is missing a TMP_Text component.");
            Destroy(labelObj);
            return;
        }

        // Position label slightly above the dot
        labelObj.transform.localPosition = Vector3.up * labelVerticalOffset;

        var label = new DistanceLabel
        {
            point = currentPoint,
            text = tmpLabel
        };

        distanceLabels.Add(label);
        UpdateDistanceLabel(label);
    }

    private void UpdateDistanceLabel(DistanceLabel label)
    {
        if (label.point == null || label.text == null)
        {
            return;
        }

        if (points.Count == 0)
        {
            return;
        }

        Transform startPoint = points[0];
        float cumulativeDistance = 0f;

        for (int i = 1; i < points.Count; i++)
        {
            Transform current = points[i];
            Transform previous = points[i - 1];
            cumulativeDistance += Vector3.Distance(previous.position, current.position);

            if (current == label.point)
            {
                break;
            }
        }

        label.text.text = $"{cumulativeDistance:F2}";

        // Ensure label stays offset above the dot in case parent transform moves/scales
        label.text.transform.position = label.point.position + Vector3.up * labelVerticalOffset;
    }

    private void UpdateDistanceLabels()
    {
        foreach (var label in distanceLabels)
        {
            UpdateDistanceLabel(label);
        }
    }
}
