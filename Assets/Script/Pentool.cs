using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Pentool : MonoBehaviour
{
    [Header("Dots")]
    [SerializeField] private GameObject dotPrefab;
    [SerializeField] Transform dotParent;
    [Header("Lines")]
    [SerializeField] private GameObject linePrefab;
    [SerializeField] Transform lineParent;
    [Header("Input Area")]
    [SerializeField] private RectTransform inputAreaImage;
    private LineController currentLine;
    private List<GameObject> allDots = new List<GameObject>();
    private List<GameObject> allLines = new List<GameObject>();

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Check if the click is within the image area
            if (IsPointInImageArea(Input.mousePosition))
            {
                if (currentLine == null)
                {
                    GameObject lineObj = Instantiate(linePrefab, Vector3.zero, Quaternion.identity, lineParent);
                    allLines.Add(lineObj);
                    currentLine = lineObj.GetComponent<LineController>();
                }
                
                // Convert screen point to world position
                Vector3 worldPos = GetWorldPositionFromScreen(Input.mousePosition);
                GameObject dot = Instantiate(dotPrefab, worldPos, Quaternion.identity, dotParent);
                allDots.Add(dot);
                currentLine.AddDot(dot.transform);
            }
        }
    }

    private bool IsPointInImageArea(Vector2 screenPoint)
    {
        if (inputAreaImage == null)
        {
            Debug.LogWarning("Input area image is not assigned!");
            return true; // Allow input if no image is assigned
        }

        Vector2 localPoint;
        Canvas canvas = inputAreaImage.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            inputAreaImage, 
            screenPoint, 
            cam, 
            out localPoint) && 
            inputAreaImage.rect.Contains(localPoint);
    }

    private Vector3 GetWorldPositionFromScreen(Vector2 screenPoint)
    {
        if (inputAreaImage != null)
        {
            Canvas canvas = inputAreaImage.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                // For Screen Space - Camera mode
                Vector3 worldPos;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    inputAreaImage, 
                    screenPoint, 
                    canvas.worldCamera, 
                    out worldPos);
                return worldPos;
            }
            else if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                // For World Space mode
                Vector3 worldPos;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    inputAreaImage, 
                    screenPoint, 
                    canvas.worldCamera, 
                    out worldPos);
                return worldPos;
            }
        }
        
        // Default: Screen Space - Overlay or no UI element
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.nearClipPlane + 1f; // Set appropriate Z distance
        return Camera.main.ScreenToWorldPoint(mousePos);
    }

    public void ClearAllDotsAndLines()
    {
        // Destroy all dots
        foreach (GameObject dot in allDots)
        {
            if (dot != null)
            {
                Destroy(dot);
            }
        }
        allDots.Clear();

        // Destroy all lines
        foreach (GameObject line in allLines)
        {
            if (line != null)
            {
                Destroy(line);
            }
        }
        allLines.Clear();

        // Reset current line
        currentLine = null;
    }
}
