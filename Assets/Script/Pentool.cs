using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class Pentool : MonoBehaviour
{
    [Header("Dots")]
    [SerializeField] private GameObject dotPrefab; // Default dot
    [SerializeField] private GameObject distanceModeDotPrefab; // Untuk Distance Mode
    [SerializeField] private GameObject drawLineModeDotPrefab; // Untuk Draw Line Mode
    [SerializeField] private GameObject drawPointModeDotPrefab; // Untuk Draw Point Mode
    [SerializeField] private GameObject polygonModeDotPrefab; // Untuk Polygon Mode
    [SerializeField] Transform dotParent;
    
    [Header("Lines")]
    [SerializeField] private GameObject linePrefab; // Default line
    [SerializeField] private GameObject distanceModeLinePrefab; // Untuk Distance Mode
    [SerializeField] private GameObject drawLineModeLinePrefab; // Untuk Draw Line Mode
    [SerializeField] private GameObject polygonModeLinePrefab; // Untuk Polygon Mode
    [SerializeField] Transform lineParent;
    
    [Header("Preview Settings")]
    [SerializeField] private bool enablePreview = true;
    [SerializeField] private float previewDotAlpha = 0.5f; // Transparansi preview dot
    [SerializeField] private float previewLineAlpha = 0.5f; // Transparansi preview line
    
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
    
    // Preview dot
    private GameObject previewDot;
    private bool isMouseInArea = false;
    
    // Preview line (from last dot to mouse)
    private GameObject previewLineObj;
    private LineRenderer previewLineRenderer;
    private GameObject currentPreviewLinePrefab;
    
    // Preview loop line (from mouse to first dot, for polygon mode)
    private GameObject previewLoopLineObj;
    private LineRenderer previewLoopLineRenderer;

    void Start()
    {
        if (enablePreview)
        {
            CreatePreviewDot();
            CreatePreviewLine();
            CreatePreviewLoopLine();
        }
    }

    void Update()
    {
        // Update preview dot and line position
        if (enablePreview)
        {
            if (previewDot != null)
            {
                UpdatePreviewDot();
            }
            if (previewLineObj != null)
            {
                UpdatePreviewLine();
            }
            if (previewLoopLineObj != null)
            {
                UpdatePreviewLoopLine();
            }
        }

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
                    GameObject lineObj = Instantiate(GetCurrentLinePrefab(), Vector3.zero, Quaternion.identity, lineParent);
                    allLines.Add(lineObj);
                    currentLine = lineObj.GetComponent<LineController>();
                    if (currentLine != null)
                    {
                        currentLine.SetPolygonMode(polygonMode);
                    }
                }
                
                // Convert screen point to world position
                Vector3 worldPos = GetWorldPositionFromScreen(Input.mousePosition);
                GameObject dot = Instantiate(GetCurrentDotPrefab(), worldPos, Quaternion.identity, dotParent);
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

    private void CreatePreviewDot()
    {
        GameObject prefabToUse = GetCurrentDotPrefab();
        previewDot = Instantiate(prefabToUse, Vector3.zero, Quaternion.identity, dotParent);
        previewDot.name = "PreviewDot";
        
        // Set semi-transparent
        SetPreviewDotAlpha(previewDotAlpha);
        
        // Hide initially
        previewDot.SetActive(false);
    }

    private void CreatePreviewLine()
    {
        GameObject linePrefabToUse = GetCurrentLinePrefab();
        if (linePrefabToUse == null)
        {
            Debug.LogWarning("No line prefab available for preview!");
            return;
        }

        // Instantiate the line prefab
        previewLineObj = Instantiate(linePrefabToUse, Vector3.zero, Quaternion.identity, lineParent);
        previewLineObj.name = "PreviewLine";
        currentPreviewLinePrefab = linePrefabToUse;
        
        // Get LineRenderer component
        previewLineRenderer = previewLineObj.GetComponent<LineRenderer>();
        if (previewLineRenderer == null)
        {
            Debug.LogWarning("Preview line prefab doesn't have LineRenderer component!");
            Destroy(previewLineObj);
            return;
        }
        
        // Disable LineController if it exists (we don't need it for preview)
        LineController lineController = previewLineObj.GetComponent<LineController>();
        if (lineController != null)
        {
            lineController.enabled = false;
        }
        
        // Set alpha for preview
        SetPreviewLineAlpha(previewLineRenderer, previewLineAlpha);
        
        // Set position count for simple line (2 points)
        previewLineRenderer.positionCount = 2;
        previewLineRenderer.loop = false;
        
        previewLineObj.SetActive(false);
    }

    private void CreatePreviewLoopLine()
    {
        GameObject linePrefabToUse = GetCurrentLinePrefab();
        if (linePrefabToUse == null)
        {
            return;
        }

        // Instantiate the line prefab for loop preview
        previewLoopLineObj = Instantiate(linePrefabToUse, Vector3.zero, Quaternion.identity, lineParent);
        previewLoopLineObj.name = "PreviewLoopLine";
        
        // Get LineRenderer component
        previewLoopLineRenderer = previewLoopLineObj.GetComponent<LineRenderer>();
        if (previewLoopLineRenderer == null)
        {
            Debug.LogWarning("Preview loop line prefab doesn't have LineRenderer component!");
            Destroy(previewLoopLineObj);
            return;
        }
        
        // Disable LineController if it exists
        LineController lineController = previewLoopLineObj.GetComponent<LineController>();
        if (lineController != null)
        {
            lineController.enabled = false;
        }
        
        // Set alpha for preview
        SetPreviewLineAlpha(previewLoopLineRenderer, previewLineAlpha);
        
        // Set position count for loop line (2 points: mouse to first dot)
        previewLoopLineRenderer.positionCount = 2;
        previewLoopLineRenderer.loop = false;
        
        previewLoopLineObj.SetActive(false);
    }

    private void RecreatePreviewLine()
    {
        // Destroy old preview line
        if (previewLineObj != null)
        {
            Destroy(previewLineObj);
            previewLineObj = null;
            previewLineRenderer = null;
        }
        
        // Destroy old preview loop line
        if (previewLoopLineObj != null)
        {
            Destroy(previewLoopLineObj);
            previewLoopLineObj = null;
            previewLoopLineRenderer = null;
        }
        
        // Create new preview lines with current prefab
        CreatePreviewLine();
        CreatePreviewLoopLine();
    }

    private void UpdatePreviewDot()
    {
        if (previewDot == null) return;

        // Check if mouse is in valid area and not blocked by UI
        bool shouldShow = IsPointInImageArea(Input.mousePosition) && !IsClickBlockedByUI(Input.mousePosition);
        
        if (shouldShow)
        {
            // Update position
            Vector3 worldPos = GetWorldPositionFromScreen(Input.mousePosition);
            previewDot.transform.position = worldPos;
            
            // Show preview dot
            if (!previewDot.activeSelf)
            {
                previewDot.SetActive(true);
            }
        }
        else
        {
            // Hide preview dot when outside area or blocked
            if (previewDot.activeSelf)
            {
                previewDot.SetActive(false);
            }
        }
    }

    private void UpdatePreviewLine()
    {
        if (previewLineRenderer == null || previewLineObj == null) return;

        // Check if we need to recreate preview line with different prefab
        GameObject expectedPrefab = GetCurrentLinePrefab();
        if (expectedPrefab != currentPreviewLinePrefab)
        {
            RecreatePreviewLine();
            return;
        }

        // Only show preview line if:
        // 1. Not in draw points only mode
        // 2. There's at least one dot placed
        // 3. Mouse is in valid area
        bool shouldShowLine = !drawPointsOnly && 
                              allDots.Count > 0 && 
                              IsPointInImageArea(Input.mousePosition) && 
                              !IsClickBlockedByUI(Input.mousePosition);

        if (shouldShowLine)
        {
            // Get last placed dot position
            Vector3 lastDotPos = allDots[allDots.Count - 1].transform.position;
            
            // Get current mouse world position
            Vector3 mouseWorldPos = GetWorldPositionFromScreen(Input.mousePosition);
            
            // Update line positions
            previewLineRenderer.SetPosition(0, lastDotPos);
            previewLineRenderer.SetPosition(1, mouseWorldPos);
            
            // Show preview line
            if (!previewLineObj.activeSelf)
            {
                previewLineObj.SetActive(true);
            }
        }
        else
        {
            // Hide preview line
            if (previewLineObj.activeSelf)
            {
                previewLineObj.SetActive(false);
            }
        }
    }

    private void UpdatePreviewLoopLine()
    {
        if (previewLoopLineRenderer == null || previewLoopLineObj == null) return;

        // Only show preview loop line if:
        // 1. In polygon mode
        // 2. There are at least 2 dots placed (need at least 2 to form a meaningful loop preview)
        // 3. Mouse is in valid area
        bool shouldShowLoopLine = polygonMode && 
                                  !drawPointsOnly && 
                                  allDots.Count >= 2 && 
                                  IsPointInImageArea(Input.mousePosition) && 
                                  !IsClickBlockedByUI(Input.mousePosition);

        if (shouldShowLoopLine)
        {
            // Get first placed dot position
            Vector3 firstDotPos = allDots[0].transform.position;
            
            // Get current mouse world position
            Vector3 mouseWorldPos = GetWorldPositionFromScreen(Input.mousePosition);
            
            // Update loop line positions (from mouse back to first dot)
            previewLoopLineRenderer.SetPosition(0, mouseWorldPos);
            previewLoopLineRenderer.SetPosition(1, firstDotPos);
            
            // Show preview loop line
            if (!previewLoopLineObj.activeSelf)
            {
                previewLoopLineObj.SetActive(true);
            }
        }
        else
        {
            // Hide preview loop line
            if (previewLoopLineObj.activeSelf)
            {
                previewLoopLineObj.SetActive(false);
            }
        }
    }

    private void SetPreviewDotAlpha(float alpha)
    {
        if (previewDot == null) return; 

        // Try to set alpha on SpriteRenderer
        SpriteRenderer sr = previewDot.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color color = sr.color;
            color.a = alpha;
            sr.color = color;
        }

        // Try to set alpha on Image (UI)
        Image img = previewDot.GetComponent<Image>();
        if (img != null)
        {
            Color color = img.color;
            color.a = alpha;
            img.color = color;
        }

        // Try to set alpha on all child SpriteRenderers
        SpriteRenderer[] childSRs = previewDot.GetComponentsInChildren<SpriteRenderer>();
        foreach (var childSR in childSRs)
        {
            Color color = childSR.color;
            color.a = alpha;
            childSR.color = color;
        }

        // Try to set alpha on all child Images
        Image[] childImgs = previewDot.GetComponentsInChildren<Image>();
        foreach (var childImg in childImgs)
        {
            Color color = childImg.color;
            color.a = alpha;
            childImg.color = color;
        }
    }

    private void SetPreviewLineAlpha(LineRenderer lr, float alpha)
    {
        if (lr == null) return;

        Color startColor = lr.startColor;
        startColor.a = alpha;
        lr.startColor = startColor;

        Color endColor = lr.endColor;
        endColor.a = alpha;
        lr.endColor = endColor;
        
        // Also try to set alpha on material if it has color property
        if (lr.material != null)
        {
            if (lr.material.HasProperty("_Color"))
            {
                Color matColor = lr.material.color;
                matColor.a = alpha;
                lr.material.color = matColor;
            }
        }
    }

    private GameObject GetCurrentDotPrefab()
    {
        if (polygonMode && polygonModeDotPrefab != null)
            return polygonModeDotPrefab;
        
        // Check DistanceButton component untuk mode
        DistanceButton distanceBtn = FindObjectOfType<DistanceButton>();
        if (distanceBtn != null)
        {
            if (distanceBtn.DistanceMode && distanceModeDotPrefab != null)
                return distanceModeDotPrefab;
            
            if (drawPointsOnly && drawPointModeDotPrefab != null)
                return drawPointModeDotPrefab;
            
            if (!drawPointsOnly && !distanceBtn.DistanceMode && drawLineModeDotPrefab != null)
                return drawLineModeDotPrefab;
        }
        
        return dotPrefab; // Fallback to default
    }

    private GameObject GetCurrentLinePrefab()
    {
        if (polygonMode && polygonModeLinePrefab != null)
            return polygonModeLinePrefab;
        
        // Check DistanceButton component untuk mode
        DistanceButton distanceBtn = FindObjectOfType<DistanceButton>();
        if (distanceBtn != null)
        {
            if (distanceBtn.DistanceMode && distanceModeLinePrefab != null)
                return distanceModeLinePrefab;
            
            if (!drawPointsOnly && !distanceBtn.DistanceMode && drawLineModeLinePrefab != null)
                return drawLineModeLinePrefab;
        }
        
        return linePrefab; // Fallback to default
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
        
        // Hide preview lines when clearing
        if (previewLineObj != null)
        {
            previewLineObj.SetActive(false);
        }
        if (previewLoopLineObj != null)
        {
            previewLoopLineObj.SetActive(false);
        }
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
        
        // Hide preview lines in draw points only mode
        if (drawPointsOnly)
        {
            if (previewLineObj != null)
            {
                previewLineObj.SetActive(false);
            }
            if (previewLoopLineObj != null)
            {
                previewLoopLineObj.SetActive(false);
            }
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

    void OnDisable()
    {
        // Hide preview dot and lines when pentool is disabled
        if (previewDot != null)
        {
            previewDot.SetActive(false);
        }
        if (previewLineObj != null)
        {
            previewLineObj.SetActive(false);
        }
        if (previewLoopLineObj != null)
        {
            previewLoopLineObj.SetActive(false);
        }
    }

    void OnDestroy()
    {
        // Clean up preview dot and lines
        if (previewDot != null)
        {
            Destroy(previewDot);
        }
        if (previewLineObj != null)
        {
            Destroy(previewLineObj);
        }
        if (previewLoopLineObj != null)
        {
            Destroy(previewLoopLineObj);
        }
    }
}