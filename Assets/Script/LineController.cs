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
    private DistanceButton cachedDistanceButton;
    private bool isPolygonMode = false;

    private class DistanceLabel
    {
        public Transform point;
        public TMP_Text text;
        public GameObject gameObject;
    }

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 0;
        points = new List<Transform>();
        // Cache DistanceButton reference
        cachedDistanceButton = FindObjectOfType<DistanceButton>();
    }

    public void AddDot(Transform point)
    {
        lr.positionCount++;
        points.Add(point);

        TryCreateDistanceLabel();
        UpdateLineLoopState();
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

        // Only create label if DistanceMode is enabled
        if (!IsDistanceModeEnabled())
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
            text = tmpLabel,
            gameObject = labelObj
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
        bool distanceModeEnabled = IsDistanceModeEnabled();
        
        // Create labels for existing points if DistanceMode is enabled and labels are missing
        if (distanceModeEnabled && points.Count >= 2)
        {
            CreateMissingLabels();
        }
        
        // Clean up null references
        distanceLabels.RemoveAll(label => label.gameObject == null || label.point == null);
        
        foreach (var label in distanceLabels)
        {
            // Show/hide label based on DistanceMode
            if (label.gameObject != null)
            {
                label.gameObject.SetActive(distanceModeEnabled);
            }
            
            // Only update label text if DistanceMode is enabled
            if (distanceModeEnabled && label.text != null)
            {
                UpdateDistanceLabel(label);
            }
        }

        UpdateLineLoopState();
    }

    private void CreateMissingLabels()
    {
        if (distanceLabelPrefab == null)
        {
            return;
        }

        // Check each point and create label if it doesn't have one
        for (int i = 1; i < points.Count; i++)
        {
            Transform point = points[i];
            
            // Check if this point already has a label
            bool hasLabel = false;
            foreach (var label in distanceLabels)
            {
                if (label.point == point)
                {
                    hasLabel = true;
                    break;
                }
            }

            // Create label if missing
            if (!hasLabel)
            {
                GameObject labelObj = Instantiate(distanceLabelPrefab, point);
                TMP_Text tmpLabel = labelObj.GetComponent<TMP_Text>();
                if (tmpLabel != null)
                {
                    labelObj.transform.localPosition = Vector3.up * labelVerticalOffset;
                    
                    var label = new DistanceLabel
                    {
                        point = point,
                        text = tmpLabel,
                        gameObject = labelObj
                    };
                    
                    distanceLabels.Add(label);
                }
            }
        }
    }

    private bool IsDistanceModeEnabled()
    {
        // Use cached reference, refresh if null
        if (cachedDistanceButton == null)
        {
            cachedDistanceButton = FindObjectOfType<DistanceButton>();
        }
        
        if (cachedDistanceButton != null)
        {
            return cachedDistanceButton.DistanceMode;
        }
        return false;
    }

    private void UpdateLineLoopState()
    {
        if (lr == null)
        {
            return;
        }

        lr.loop = isPolygonMode && points != null && points.Count >= 3;
    }

    public void SetPolygonMode(bool enabled)
    {
        isPolygonMode = enabled;
        UpdateLineLoopState();
    }
}
