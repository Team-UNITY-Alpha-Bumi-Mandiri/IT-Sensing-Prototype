using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    [Header("Modes")]
    [SerializeField] private bool drawPointsOnly = false;
    [SerializeField] private bool polygonMode = false;
    [Header("Blocking UI Elements")]
    [SerializeField] private List<GameObject> blockingUIElements = new List<GameObject>();
    [Tooltip("If true, any UI element will block input. If false, only specified blockingUIElements will block.")]
    [SerializeField] private bool blockOnAnyUI = false;
    private LineController currentLine;
    private List<GameObject> allDots = new List<GameObject>();
    private List<GameObject> allLines = new List<GameObject>();

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Check if click is blocked by UI elements (like popups)
            if (IsClickBlockedByUI(Input.mousePosition))
            {
                return; // Don't process click if blocked
            }

            // Check if the click is within the image area
            if (IsPointInImageArea(Input.mousePosition))
            {
                // If we are allowed to draw lines, ensure a line object exists
                if (!drawPointsOnly && currentLine == null)
                {
                    GameObject lineObj = Instantiate(linePrefab, Vector3.zero, Quaternion.identity, lineParent);
                    allLines.Add(lineObj);
                    currentLine = lineObj.GetComponent<LineController>();
                    if (currentLine != null)
                    {
                        currentLine.SetPolygonMode(polygonMode);
                    }
                }
                
                // Convert screen point to world position
                Vector3 worldPos = GetWorldPositionFromScreen(Input.mousePosition);
                GameObject dot = Instantiate(dotPrefab, worldPos, Quaternion.identity, dotParent);
                allDots.Add(dot);

                // Only connect dots with lines when not in draw-points-only mode
                if (!drawPointsOnly && currentLine != null)
                {
                    currentLine.AddDot(dot.transform);
                    currentLine.SetPolygonMode(polygonMode);
                }
            }
        }
    }

    private bool IsClickBlockedByUI(Vector2 screenPoint)
    {
        // Check if EventSystem exists
        if (EventSystem.current == null)
        {
            return false; // No EventSystem, can't check for UI blocking
        }

        // If blockOnAnyUI is true, check if pointer is over any UI element
        if (blockOnAnyUI)
        {
            return EventSystem.current.IsPointerOverGameObject();
        }

        // Otherwise, check if pointer is over any of the specified blocking UI elements
        if (blockingUIElements == null || blockingUIElements.Count == 0)
        {
            return false; // No blocking elements specified, allow input
        }

        // Use GraphicRaycaster to find UI elements under the mouse
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = screenPoint;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        // Check if any of the hit UI elements are in our blocking list
        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null) continue;

            GameObject hitObject = result.gameObject;
            
            // Check if the hit object or any of its parents is in the blocking list
            Transform current = hitObject.transform;
            while (current != null)
            {
                if (blockingUIElements.Contains(current.gameObject))
                {
                    return true; // Click is blocked
                }
                current = current.parent;
            }
        }

        return false; // Click is not blocked
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

    public void SetDrawPointsOnly(bool value)
    {
        drawPointsOnly = value;
        if (drawPointsOnly)
        {
            SetPolygonMode(false);
        }

        if (drawPointsOnly && currentLine != null)
        {
            GameObject currentLineObject = currentLine.gameObject;
            if (currentLineObject != null)
            {
                Destroy(currentLineObject);
                allLines.Remove(currentLineObject);
            }
            currentLine = null;
        }
    }

    public void SetPolygonMode(bool enabled)
    {
        polygonMode = enabled;
        if (polygonMode)
        {
            drawPointsOnly = false;
        }

        if (currentLine != null)
        {
            currentLine.SetPolygonMode(polygonMode);
        }
    }
}
