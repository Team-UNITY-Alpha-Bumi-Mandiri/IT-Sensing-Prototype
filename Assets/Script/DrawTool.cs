using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class DrawTool : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;
    public RectTransform container;

    [Header("Prefabs")]
    public GameObject pointMarkerPrefab;
    public GameObject lineSegmentPrefab;
    public GameObject polygonFillPrefab;
    public GameObject vertexPointPrefab;

    [Header("Settings")]
    public Color pointColor = Color.blue;
    public Color lineColor = Color.red;
    public Color polygonLineColor = Color.green;
    public Color polygonFillColor = new Color(0, 1, 0, 0.3f);
    public float doubleClickThreshold = 0.3f;

    public enum DrawMode { Point, Line, Polygon }
    public DrawMode currentMode = DrawMode.Point;

    // State
    private bool isDrawing = false;
    private float lastClickTime = 0f;
    private DrawMode activeMode = DrawMode.Point;

    // Data Structures
    [System.Serializable]
    public class DrawObject
    {
        public string id;
        public DrawMode type;
        public List<Vector2> coordinates;
        public List<GameObject> visualObjects;
        public GameObject fillObject;
        public GameObject parentObject; // Parent container untuk grouping
    }

    // Drawing Counter untuk nomor urutan
    private int drawingCounter = 0;

    private List<DrawObject> allDrawObjects = new List<DrawObject>();
    private DrawObject currentDrawObject;

    // Ghost Objects
    private GameObject ghostLine;
    private RectTransform ghostLineRect;

    // Cache Sync
    private double lastLat, lastLon;
    private int lastZoom;

    void Start()
    {
        if (container != null)
        {
            CreateGhostObjects();
        }
    }

    void Update()
    {
        if (mapController == null || container == null) return;

        // Sync
        bool mapChanged = (mapController.latitude != lastLat) ||
                          (mapController.longitude != lastLon) ||
                          (mapController.zoom != lastZoom);

        if (mapChanged)
        {
            RebuildAllVisuals();
            lastLat = mapController.latitude;
            lastLon = mapController.longitude;
            lastZoom = mapController.zoom;
        }

        HandleInput();
    }

    void HandleInput()
    {
        if (activeMode == DrawMode.Point && !isDrawing) return;
        if (Mouse.current == null) return;
        
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Ghost Preview
        if (isDrawing && currentDrawObject != null && currentDrawObject.coordinates.Count > 0)
        {
            UpdateGhostPreview(mousePos);
        }
        else
        {
            HideGhostPreview();
        }

        // LEFT CLICK
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            float timeSinceLastClick = Time.time - lastClickTime;
            lastClickTime = Time.time;

            Vector2 latLon = mapController.ScreenToLatLon(mousePos);

            // DOUBLE CLICK (Finish)
            if (timeSinceLastClick <= doubleClickThreshold && isDrawing)
            {
                FinishDrawing();
            }
            else
            {
                AddPoint(latLon);
            }
        }

        // RIGHT CLICK (Undo)
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            UndoLast();
        }

        // ESC (Cancel)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isDrawing)
            {
                CancelDrawing();
            }
        }
    }

    void AddPoint(Vector2 latLon)
    {
        switch (currentMode)
        {
            case DrawMode.Point:
                CreatePointObject(latLon);
                break;

            case DrawMode.Line:
            case DrawMode.Polygon:
                if (!isDrawing)
                {
                    StartNewDrawObject();
                }
                currentDrawObject.coordinates.Add(latLon);
                RebuildCurrentObject();
                break;
        }
    }

    void StartNewDrawObject()
    {
        isDrawing = true;
        drawingCounter++;

        // Buat parent container untuk drawing ini
        string parentName = $"Drawing_{drawingCounter}_{currentMode}";
        GameObject parentObj = new GameObject(parentName, typeof(RectTransform));
        parentObj.transform.SetParent(container, false);
        
        // Setup RectTransform parent
        RectTransform parentRT = parentObj.GetComponent<RectTransform>();
        parentRT.anchorMin = Vector2.zero;
        parentRT.anchorMax = Vector2.one;
        parentRT.offsetMin = Vector2.zero;
        parentRT.offsetMax = Vector2.zero;

        currentDrawObject = new DrawObject
        {
            id = System.Guid.NewGuid().ToString(),
            type = currentMode,
            coordinates = new List<Vector2>(),
            visualObjects = new List<GameObject>(),
            parentObject = parentObj
        };
    }

    void FinishDrawing()
    {
        if (currentDrawObject == null || currentDrawObject.coordinates.Count < 2)
        {
            CancelDrawing();
            return;
        }

        if (currentMode == DrawMode.Polygon && currentDrawObject.coordinates.Count < 3)
        {
            Debug.Log("Polygon membutuhkan minimal 3 titik!");
            return;
        }

        allDrawObjects.Add(currentDrawObject);
        HideGhostPreview();
        currentDrawObject = null;
        isDrawing = false;

        // DEACTIVATE MODE setelah finish
        activeMode = DrawMode.Point; // Reset ke default (atau bisa set ke "None")
        
        Debug.Log($"Drawing selesai! Mode deactivated. Klik button lagi untuk draw baru.");
    }

    void CancelDrawing()
    {
        if (currentDrawObject != null)
        {
            // Hapus parent (akan menghapus semua children juga)
            if (currentDrawObject.parentObject != null)
            {
                Destroy(currentDrawObject.parentObject);
            }
            else
            {
                // Fallback jika tidak ada parent
                foreach (var obj in currentDrawObject.visualObjects)
                {
                    if (obj != null) Destroy(obj);
                }
                if (currentDrawObject.fillObject != null)
                {
                    Destroy(currentDrawObject.fillObject);
                }
            }
            
            // Kurangi counter karena drawing dibatalkan
            drawingCounter--;
        }

        currentDrawObject = null;
        isDrawing = false;
        HideGhostPreview();
        
        // DEACTIVATE MODE setelah cancel
        activeMode = DrawMode.Point;
        
        Debug.Log("Drawing dibatalkan. Mode deactivated.");
    }

    void CreatePointObject(Vector2 latLon)
    {
        drawingCounter++;

        // Buat parent container untuk point ini
        string parentName = $"Drawing_{drawingCounter}_Point";
        GameObject parentObj = new GameObject(parentName, typeof(RectTransform));
        parentObj.transform.SetParent(container, false);
        
        // Setup RectTransform parent
        RectTransform parentRT = parentObj.GetComponent<RectTransform>();
        parentRT.anchorMin = Vector2.zero;
        parentRT.anchorMax = Vector2.one;
        parentRT.offsetMin = Vector2.zero;
        parentRT.offsetMax = Vector2.zero;

        DrawObject obj = new DrawObject
        {
            id = System.Guid.NewGuid().ToString(),
            type = DrawMode.Point,
            coordinates = new List<Vector2> { latLon },
            visualObjects = new List<GameObject>(),
            parentObject = parentObj
        };

        Vector2 pos = mapController.LatLonToLocalPosition(latLon.x, latLon.y);
        GameObject marker = Instantiate(pointMarkerPrefab, parentObj.transform);
        marker.GetComponent<RectTransform>().anchoredPosition = pos;
        
        Image img = marker.GetComponent<Image>();
        if (img) img.color = pointColor;

        obj.visualObjects.Add(marker);
        allDrawObjects.Add(obj);
    }

    void RebuildCurrentObject()
    {
        if (currentDrawObject == null) return;

        foreach (var obj in currentDrawObject.visualObjects)
        {
            if (obj != null) Destroy(obj);
        }
        currentDrawObject.visualObjects.Clear();

        if (currentDrawObject.fillObject != null)
        {
            Destroy(currentDrawObject.fillObject);
            currentDrawObject.fillObject = null;
        }

        var coords = currentDrawObject.coordinates;
        if (coords.Count == 0) return;

        // Spawn vertices
        Transform parentTransform = currentDrawObject.parentObject != null ? currentDrawObject.parentObject.transform : container;
        for (int i = 0; i < coords.Count; i++)
        {
            Vector2 pos = mapController.LatLonToLocalPosition(coords[i].x, coords[i].y);
            GameObject vertex = Instantiate(vertexPointPrefab, parentTransform);
            vertex.GetComponent<RectTransform>().anchoredPosition = pos;
            
            Image img = vertex.GetComponent<Image>();
            if (img)
            {
                img.color = (currentMode == DrawMode.Line) ? lineColor : polygonLineColor;
            }

            currentDrawObject.visualObjects.Add(vertex);
        }

        // Spawn lines
        for (int i = 1; i < coords.Count; i++)
        {
            GameObject line = Instantiate(lineSegmentPrefab, parentTransform);
            DrawLineUI(line.GetComponent<RectTransform>(), coords[i - 1], coords[i]);
            
            Image img = line.GetComponent<Image>();
            if (img)
            {
                img.color = (currentMode == DrawMode.Line) ? lineColor : polygonLineColor;
            }

            currentDrawObject.visualObjects.Add(line);
        }

        // Polygon: close + fill
        if (currentMode == DrawMode.Polygon && coords.Count >= 3)
        {
            GameObject closeLine = Instantiate(lineSegmentPrefab, parentTransform);
            DrawLineUI(closeLine.GetComponent<RectTransform>(), coords[coords.Count - 1], coords[0]);
            
            Image img = closeLine.GetComponent<Image>();
            if (img) img.color = polygonLineColor;
            
            currentDrawObject.visualObjects.Add(closeLine);
            CreatePolygonFill(currentDrawObject);
        }
    }

    void CreatePolygonFill(DrawObject obj)
    {
        if (obj.coordinates.Count < 3) return;

        // Tentukan parent transform
        Transform parentTransform = obj.parentObject != null ? obj.parentObject.transform : container;

        // Buat GameObject dengan UIPolygonRenderer
        GameObject fill = new GameObject("PolygonFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIPolygonRenderer));
        fill.transform.SetParent(parentTransform, false);
        fill.transform.SetAsFirstSibling();

        // Setup RectTransform
        RectTransform rt = fill.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = container.sizeDelta; // Full size container

        // Konversi koordinat lat/lon ke posisi lokal
        List<Vector2> localPositions = new List<Vector2>();
        foreach (var coord in obj.coordinates)
        {
            Vector2 pos = mapController.LatLonToLocalPosition(coord.x, coord.y);
            localPositions.Add(pos);
        }

        // Set polygon ke renderer
        UIPolygonRenderer renderer = fill.GetComponent<UIPolygonRenderer>();
        renderer.SetPolygon(localPositions, polygonFillColor);
        renderer.raycastTarget = false; // Agar tidak block click

        obj.fillObject = fill;
    }


    void CreateGhostObjects()
    {
        if (container == null) return;

        if (ghostLine == null && lineSegmentPrefab != null)
        {
            ghostLine = Instantiate(lineSegmentPrefab, container);
            ghostLineRect = ghostLine.GetComponent<RectTransform>();
            Image img = ghostLine.GetComponent<Image>();
            if (img)
            {
                img.color = new Color(1, 1, 1, 0.5f);
                img.raycastTarget = false;
            }
            ghostLine.SetActive(false);
        }
    }

    void UpdateGhostPreview(Vector2 mousePos)
    {
        if (currentDrawObject == null || currentDrawObject.coordinates.Count == 0)
        {
            HideGhostPreview();
            return;
        }

        Vector2 lastCoord = currentDrawObject.coordinates[currentDrawObject.coordinates.Count - 1];
        Vector2 mouseLatLon = mapController.ScreenToLatLon(mousePos);

        if (ghostLine != null)
        {
            DrawLineUI(ghostLineRect, lastCoord, mouseLatLon);
            
            Image img = ghostLine.GetComponent<Image>();
            if (img)
            {
                Color col = (currentMode == DrawMode.Line) ? lineColor : polygonLineColor;
                img.color = new Color(col.r, col.g, col.b, 0.5f);
            }
            
            ghostLine.SetActive(true);
            ghostLine.transform.SetAsLastSibling();
        }
    }

    void HideGhostPreview()
    {
        if (ghostLine != null) ghostLine.SetActive(false);
    }

    public void RebuildAllVisuals()
    {
        foreach (var obj in allDrawObjects)
        {
            RebuildDrawObject(obj);
        }

        if (currentDrawObject != null)
        {
            RebuildCurrentObject();
        }
    }

    void RebuildDrawObject(DrawObject obj)
    {
        foreach (var visual in obj.visualObjects)
        {
            if (visual != null) Destroy(visual);
        }
        obj.visualObjects.Clear();

        if (obj.fillObject != null)
        {
            Destroy(obj.fillObject);
            obj.fillObject = null;
        }

        switch (obj.type)
        {
            case DrawMode.Point:
                if (obj.coordinates.Count > 0)
                {
                    Transform parentTransform = obj.parentObject != null ? obj.parentObject.transform : container;
                    Vector2 pos = mapController.LatLonToLocalPosition(
                        obj.coordinates[0].x, obj.coordinates[0].y);
                    GameObject marker = Instantiate(pointMarkerPrefab, parentTransform);
                    marker.GetComponent<RectTransform>().anchoredPosition = pos;
                    
                    Image img = marker.GetComponent<Image>();
                    if (img) img.color = pointColor;
                    
                    obj.visualObjects.Add(marker);
                }
                break;

            case DrawMode.Line:
                RebuildLineObject(obj, lineColor);
                break;

            case DrawMode.Polygon:
                RebuildLineObject(obj, polygonLineColor);
                
                if (obj.coordinates.Count >= 3)
                {
                    Transform parentTransform = obj.parentObject != null ? obj.parentObject.transform : container;
                    GameObject closeLine = Instantiate(lineSegmentPrefab, parentTransform);
                    DrawLineUI(closeLine.GetComponent<RectTransform>(),
                        obj.coordinates[obj.coordinates.Count - 1], obj.coordinates[0]);
                    
                    Image img = closeLine.GetComponent<Image>();
                    if (img) img.color = polygonLineColor;
                    
                    obj.visualObjects.Add(closeLine);
                    CreatePolygonFill(obj);
                }
                break;
        }
    }

    void RebuildLineObject(DrawObject obj, Color color)
    {
        Transform parentTransform = obj.parentObject != null ? obj.parentObject.transform : container;
        
        for (int i = 0; i < obj.coordinates.Count; i++)
        {
            Vector2 pos = mapController.LatLonToLocalPosition(
                obj.coordinates[i].x, obj.coordinates[i].y);
            GameObject vertex = Instantiate(vertexPointPrefab, parentTransform);
            vertex.GetComponent<RectTransform>().anchoredPosition = pos;
            
            Image img = vertex.GetComponent<Image>();
            if (img) img.color = color;
            
            obj.visualObjects.Add(vertex);
        }

        for (int i = 1; i < obj.coordinates.Count; i++)
        {
            GameObject line = Instantiate(lineSegmentPrefab, parentTransform);
            DrawLineUI(line.GetComponent<RectTransform>(),
                obj.coordinates[i - 1], obj.coordinates[i]);
            
            Image img = line.GetComponent<Image>();
            if (img) img.color = color;
            
            obj.visualObjects.Add(line);
        }
    }

    void DrawLineUI(RectTransform lineRect, Vector2 startLatLon, Vector2 endLatLon)
    {
        if (lineRect == null) return;

        Vector2 startPos = mapController.LatLonToLocalPosition(startLatLon.x, startLatLon.y);
        Vector2 endPos = mapController.LatLonToLocalPosition(endLatLon.x, endLatLon.y);

        Vector2 dir = endPos - startPos;
        float dist = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lineRect.anchoredPosition = startPos + (dir * 0.5f);
        lineRect.sizeDelta = new Vector2(dist, lineRect.sizeDelta.y);
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    // ========================================================================
    // PUBLIC API
    // ========================================================================
    
    public void ActivateMode(DrawMode mode)
    {
        if (isDrawing && activeMode != mode)
        {
            CancelDrawing();
        }

        activeMode = mode;
        currentMode = mode;

        Debug.Log($"DrawTool Activated: {mode}");
    }

    public void DeactivateMode(DrawMode mode)
    {
        if (activeMode == mode)
        {
            if (isDrawing)
            {
                CancelDrawing();
            }

            Debug.Log($"DrawTool Deactivated: {mode}");
        }
    }

    public bool IsModeActive(DrawMode mode)
    {
        return activeMode == mode;
    }

    public bool IsCurrentlyDrawing()
    {
        return isDrawing;
    }

    public void UndoLast()
    {
        if (isDrawing && currentDrawObject != null && currentDrawObject.coordinates.Count > 0)
        {
            currentDrawObject.coordinates.RemoveAt(currentDrawObject.coordinates.Count - 1);
            
            if (currentDrawObject.coordinates.Count == 0)
            {
                CancelDrawing();
            }
            else
            {
                RebuildCurrentObject();
            }
            
            Debug.Log("Undo: Titik terakhir dihapus");
        }
        else if (allDrawObjects.Count > 0)
        {
            DrawObject lastObj = allDrawObjects[allDrawObjects.Count - 1];
            foreach (var visual in lastObj.visualObjects)
            {
                if (visual != null) Destroy(visual);
            }
            if (lastObj.fillObject != null) Destroy(lastObj.fillObject);
            
            allDrawObjects.RemoveAt(allDrawObjects.Count - 1);
            
            Debug.Log("Undo: Objek terakhir dihapus");
        }
    }

    public string ExportToJSON()
    {
        return JsonUtility.ToJson(new DrawObjectListWrapper { objects = allDrawObjects });
    }

    [System.Serializable]
    class DrawObjectListWrapper
    {
        public List<DrawObject> objects;
    }
}