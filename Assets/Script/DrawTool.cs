using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Events;

public class DrawTool : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;
    public RectTransform container;

    [Header("Prefabs")]
    public GameObject pointMarkerPrefab, lineSegmentPrefab, polygonFillPrefab, vertexPointPrefab, tooltipPrefab;

    [Header("Settings")]
    public float snapDistancePixels = 30f;
    
    // Colors (private - set via code)
    Color pointColor = Color.blue, vertexColor = Color.white;
    Color lineColor = new Color(0f, 0.424f, 1f), polygonLineColor = new Color(0f, 0.424f, 1f);
    Color polygonFillColor = new Color(0f, 0.424f, 1f, 0.3f);

    public enum DrawMode { Point, Line, Polygon, Delete }
    public DrawMode currentMode = DrawMode.Point;
    
    [Header("Events")]
    public UnityEvent<DrawObject> onDrawComplete;

    // State
    bool isDrawing, isToolActive;
    DrawMode activeMode = DrawMode.Point;
    int drawingCounter;
    double lastLat, lastLon;
    int lastZoom;

    // Objects
    List<DrawObject> allDrawObjects = new();
    DrawObject currentDrawObject;
    GameObject ghostLine, tooltipObj;
    RectTransform ghostLineRect, tooltipRect;
    TMP_Text tooltipText;

    [System.Serializable]
    public class DrawObject
    {
        public string id;
        public DrawMode type;
        public List<Vector2> coordinates = new();
        public List<GameObject> visualObjects = new();
        public GameObject fillObject, parentObject;
    }

    void Start()
    {
        if (!container) return;
        CreateGhost();
        CreateTooltip();
    }

    void Update()
    {
        if (!mapController || !container) return;
        if (mapController.latitude != lastLat || mapController.longitude != lastLon || mapController.zoom != lastZoom)
        {
            RebuildAllVisuals();
            (lastLat, lastLon, lastZoom) = (mapController.latitude, mapController.longitude, mapController.zoom);
        }
        HandleInput();
    }

    void HandleInput()
    {
        if (!isToolActive || Mouse.current == null) return;
        var mousePos = Mouse.current.position.ReadValue();

        // Ghost & tooltip
        if (isDrawing && currentDrawObject?.coordinates.Count > 0) UpdateGhost(mousePos);
        else HideGhost();
        UpdateTooltip(mousePos);

        // Left click
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (activeMode == DrawMode.Delete) HandleDeleteClick(mousePos);
            else AddPoint(mapController.ScreenToLatLon(mousePos), mousePos);
        }
        // Right click = Undo
        if (Mouse.current.rightButton.wasPressedThisFrame) UndoLast();
        // ESC = Cancel
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && isDrawing) CancelDrawing();
    }

    void AddPoint(Vector2 latLon, Vector2 screenPos)
    {
        if (currentMode == DrawMode.Point) { CreatePointObject(latLon); return; }

        // Snap-to-finish check
        if (isDrawing && currentDrawObject != null && currentDrawObject.coordinates.Count >= 2)
        {
            Vector2 snapTarget = currentMode == DrawMode.Line ? currentDrawObject.coordinates[^1] : currentDrawObject.coordinates[0];
            bool canSnap = currentMode == DrawMode.Line || currentDrawObject.coordinates.Count >= 3;
            
            if (canSnap && Vector2.Distance(screenPos, LatLonToScreen(snapTarget)) < snapDistancePixels)
            { FinishDrawing(); return; }
        }

        if (!isDrawing) StartNewDrawObject();
        currentDrawObject.coordinates.Add(latLon);
        RebuildVisuals(currentDrawObject, true);
    }

    Vector2 LatLonToScreen(Vector2 latLon) =>
        RectTransformUtility.WorldToScreenPoint(null, container.TransformPoint(mapController.LatLonToLocalPosition(latLon.x, latLon.y)));

    void StartNewDrawObject()
    {
        isDrawing = true;
        currentDrawObject = new DrawObject
        {
            id = System.Guid.NewGuid().ToString(),
            type = currentMode,
            parentObject = CreateParent($"Drawing_{++drawingCounter}_{currentMode}")
        };
    }

    GameObject CreateParent(string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(container, false);
        var rt = obj.GetComponent<RectTransform>();
        (rt.anchorMin, rt.anchorMax, rt.offsetMin, rt.offsetMax) = (Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return obj;
    }

    void FinishDrawing()
    {
        if (currentDrawObject == null || currentDrawObject.coordinates.Count < 2 ||
            (currentMode == DrawMode.Polygon && currentDrawObject.coordinates.Count < 3))
        { CancelDrawing(); return; }

        RebuildVisuals(currentDrawObject, false);
        allDrawObjects.Add(currentDrawObject);
        // Invoke event
        onDrawComplete?.Invoke(currentDrawObject);
        ResetState();
    }

    void CancelDrawing()
    {
        if (currentDrawObject?.parentObject) Destroy(currentDrawObject.parentObject);
        else ClearVisuals(currentDrawObject);
        if (currentDrawObject != null) drawingCounter--;
        ResetState();
    }

    void ResetState()
    {
        currentDrawObject = null;
        isDrawing = isToolActive = false;
        activeMode = DrawMode.Point;
        HideGhost();
        HideTooltip();
    }

    void HandleDeleteClick(Vector2 mousePos)
    {
        DrawObject toDelete = null;
        float minDist = snapDistancePixels; 

        // Reverse iterate to check topmost objects first
        for (int i = allDrawObjects.Count - 1; i >= 0; i--)
        {
            var obj = allDrawObjects[i];
            if (obj.coordinates == null || obj.coordinates.Count == 0) continue;

            // Convert all points to Screen Space for accurate distance check
            List<Vector2> screenPoints = new();
            foreach(var c in obj.coordinates) screenPoints.Add(LatLonToScreen(c));

            bool hit = false;
            if (obj.type == DrawMode.Point)
            {
                if (Vector2.Distance(mousePos, screenPoints[0]) < minDist) hit = true;
            }
            else
            {
                // Check distance to each segment
                for (int k = 0; k < screenPoints.Count - 1; k++)
                    if (GetPointLineDistance(mousePos, screenPoints[k], screenPoints[k+1]) < minDist) { hit = true; break; }
                
                // For polygons, also check the closing segment
                if (!hit && obj.type == DrawMode.Polygon && screenPoints.Count >= 3)
                {
                    if (GetPointLineDistance(mousePos, screenPoints[^1], screenPoints[0]) < minDist) hit = true;
                    if (!hit && IsPointInPolygon(mousePos, screenPoints)) hit = true;
                }
            }

            if (hit)
            {
                toDelete = obj;
                break;
            }
        }

        if (toDelete != null)
        {
            if (toDelete.parentObject) Destroy(toDelete.parentObject);
            else ClearVisuals(toDelete);
            allDrawObjects.Remove(toDelete);
        }
    }

    bool IsPointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    float GetPointLineDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 n = b - a;
        float len2 = n.sqrMagnitude;
        if (len2 == 0) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, n) / len2);
        return Vector2.Distance(p, a + n * t);
    }

    void CreatePointObject(Vector2 latLon)
    {
        var obj = new DrawObject
        {
            id = System.Guid.NewGuid().ToString(),
            type = DrawMode.Point,
            coordinates = new List<Vector2> { latLon },
            parentObject = CreateParent($"Drawing_{++drawingCounter}_Point")
        };
        obj.visualObjects.Add(SpawnMarker(pointMarkerPrefab, latLon, obj.parentObject.transform, pointColor));
        allDrawObjects.Add(obj);
    }

    GameObject SpawnMarker(GameObject prefab, Vector2 latLon, Transform parent, Color color)
    {
        var marker = Instantiate(prefab, parent);
        marker.GetComponent<RectTransform>().anchoredPosition = mapController.LatLonToLocalPosition(latLon.x, latLon.y);
        if (marker.TryGetComponent<Image>(out var img)) img.color = color;
        return marker;
    }

    void RebuildVisuals(DrawObject obj, bool isCurrent = false)
    {
        ClearVisuals(obj);
        if (obj.coordinates.Count == 0) return;

        var parent = obj.parentObject ? obj.parentObject.transform : container.transform;
        var color = obj.type == DrawMode.Line ? lineColor : polygonLineColor;

        if (obj.type == DrawMode.Point)
        { obj.visualObjects.Add(SpawnMarker(pointMarkerPrefab, obj.coordinates[0], parent, pointColor)); return; }

        // Vertices (only during drawing)
        if (isCurrent)
            foreach (var c in obj.coordinates)
                obj.visualObjects.Add(SpawnMarker(vertexPointPrefab, c, parent, vertexColor));

        // Lines
        for (int i = 1; i < obj.coordinates.Count; i++)
            obj.visualObjects.Add(CreateLine(obj.coordinates[i - 1], obj.coordinates[i], parent, color));

        // Polygon close + fill
        if (obj.type == DrawMode.Polygon && obj.coordinates.Count >= 3)
        {
            if (!isCurrent) obj.visualObjects.Add(CreateLine(obj.coordinates[^1], obj.coordinates[0], parent, polygonLineColor));
            CreatePolygonFill(obj);
        }
    }

    GameObject CreateLine(Vector2 start, Vector2 end, Transform parent, Color color)
    {
        var line = Instantiate(lineSegmentPrefab, parent);
        SetLineTransform(line.GetComponent<RectTransform>(), start, end);
        if (line.TryGetComponent<Image>(out var img)) img.color = color;
        return line;
    }

    void SetLineTransform(RectTransform rt, Vector2 startLL, Vector2 endLL)
    {
        var s = mapController.LatLonToLocalPosition(startLL.x, startLL.y);
        var e = mapController.LatLonToLocalPosition(endLL.x, endLL.y);
        var dir = e - s;
        rt.anchoredPosition = s + dir * 0.5f;
        rt.sizeDelta = new Vector2(dir.magnitude, rt.sizeDelta.y);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void CreatePolygonFill(DrawObject obj)
    {
        if (obj.coordinates.Count < 3) return;
        var parent = obj.parentObject ? obj.parentObject.transform : container.transform;
        var fill = new GameObject("PolygonFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIPolygonRenderer));
        fill.transform.SetParent(parent, false);
        fill.transform.SetAsFirstSibling();

        var rt = fill.GetComponent<RectTransform>();
        (rt.anchorMin, rt.anchorMax) = (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = container.sizeDelta;

        var positions = new List<Vector2>();
        foreach (var c in obj.coordinates) positions.Add(mapController.LatLonToLocalPosition(c.x, c.y));

        var renderer = fill.GetComponent<UIPolygonRenderer>();
        renderer.SetPolygon(positions, polygonFillColor);
        renderer.raycastTarget = false;
        obj.fillObject = fill;
    }

    void ClearVisuals(DrawObject obj)
    {
        if (obj == null) return;
        foreach (var v in obj.visualObjects) if (v) Destroy(v);
        obj.visualObjects.Clear();
        if (obj.fillObject) { Destroy(obj.fillObject); obj.fillObject = null; }
    }

    // ========== GHOST LINE ==========
    void CreateGhost()
    {
        if (ghostLine || !lineSegmentPrefab) return;
        ghostLine = Instantiate(lineSegmentPrefab, container);
        ghostLineRect = ghostLine.GetComponent<RectTransform>();
        if (ghostLine.TryGetComponent<Image>(out var img)) { img.color = new Color(1, 1, 1, 0.5f); img.raycastTarget = false; }
        ghostLine.SetActive(false);
    }

    void UpdateGhost(Vector2 mousePos)
    {
        if (currentDrawObject?.coordinates.Count == 0 || !ghostLine) { HideGhost(); return; }
        SetLineTransform(ghostLineRect, currentDrawObject.coordinates[^1], mapController.ScreenToLatLon(mousePos));
        var col = currentMode == DrawMode.Line ? lineColor : polygonLineColor;
        if (ghostLine.TryGetComponent<Image>(out var img)) img.color = new Color(col.r, col.g, col.b, 0.5f);
        ghostLine.SetActive(true);
        ghostLine.transform.SetAsLastSibling();
    }

    void HideGhost() { if (ghostLine) ghostLine.SetActive(false); }

    // ========== TOOLTIP ==========
    void CreateTooltip()
    {
        if (tooltipObj || !tooltipPrefab) return;
        tooltipObj = Instantiate(tooltipPrefab, container);
        tooltipRect = tooltipObj.GetComponent<RectTransform>();
        tooltipText = tooltipObj.GetComponentInChildren<TMP_Text>();
        if (tooltipObj.TryGetComponent<Image>(out var img)) img.raycastTarget = false;
        if (tooltipText) tooltipText.raycastTarget = false;
        tooltipObj.SetActive(false);
    }

    void UpdateTooltip(Vector2 mousePos)
    {
        if (currentMode == DrawMode.Point || !tooltipObj) { HideTooltip(); return; }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mousePos, null, out var lp))
            tooltipRect.anchoredPosition = lp + new Vector2(25, -15);

        int pts = currentDrawObject?.coordinates.Count ?? 0;
        var mouseLL = mapController.ScreenToLatLon(mousePos);
        string msg;

        if (currentMode == DrawMode.Line)
        {
            if (pts == 0) msg = "Click to start line";
            else if (pts == 1) msg = $"{CalcDist(currentDrawObject.coordinates[^1], mouseLL):F2} km\nClick to continue";
            else msg = $"{GetTotalDist() + CalcDist(currentDrawObject.coordinates[^1], mouseLL):F2} km\nClick last point to finish";
        }
        else
        {
            if (pts == 0) msg = "Click to start shape";
            else if (pts < 3) msg = "Click to continue shape";
            else msg = "Click first point to close";
        }

        if (tooltipText) tooltipText.text = msg;
        tooltipObj.SetActive(true);
        tooltipObj.transform.SetAsLastSibling();
    }

    void HideTooltip() { if (tooltipObj) tooltipObj.SetActive(false); }

    // ========== MATH ==========
    float CalcDist(Vector2 p1, Vector2 p2)
    {
        float R = 6371f, dLat = (p2.x - p1.x) * Mathf.Deg2Rad, dLon = (p2.y - p1.y) * Mathf.Deg2Rad;
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(p1.x * Mathf.Deg2Rad) * Mathf.Cos(p2.x * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        return R * 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
    }

    float GetTotalDist()
    {
        if (currentDrawObject == null || currentDrawObject.coordinates.Count < 2) return 0;
        float t = 0;
        for (int i = 1; i < currentDrawObject.coordinates.Count; i++)
            t += CalcDist(currentDrawObject.coordinates[i - 1], currentDrawObject.coordinates[i]);
        return t;
    }

    // ========== PUBLIC API ==========
    public void RebuildAllVisuals()
    {
        foreach (var obj in allDrawObjects) RebuildVisuals(obj);
        if (currentDrawObject != null) RebuildVisuals(currentDrawObject, true);
    }

    public void ActivateMode(DrawMode mode)
    {
        if (isDrawing && activeMode != mode) CancelDrawing();
        isToolActive = true;
        activeMode = currentMode = mode;
    }

    public void DeactivateMode(DrawMode mode)
    {
        if (activeMode != mode) return;
        if (isDrawing) CancelDrawing();
        isToolActive = false;
    }

    public bool IsModeActive(DrawMode mode) => isToolActive && activeMode == mode;
    public bool IsCurrentlyDrawing() => isDrawing;

    public void UndoLast()
    {
        if (isDrawing && currentDrawObject?.coordinates.Count > 0)
        {
            currentDrawObject.coordinates.RemoveAt(currentDrawObject.coordinates.Count - 1);
            if (currentDrawObject.coordinates.Count == 0) CancelDrawing();
            else RebuildVisuals(currentDrawObject, true);
        }
        else if (allDrawObjects.Count > 0)
        {
            var last = allDrawObjects[^1];
            if (last.parentObject) Destroy(last.parentObject);
            else ClearVisuals(last);
            allDrawObjects.RemoveAt(allDrawObjects.Count - 1);
        }
    }

    public void ClearAll()
    {
        Debug.Log($"[DrawTool] ClearAll called. Objects: {allDrawObjects.Count}");
        // Iterate backwards for safety when removing/modifying
        for (int i = allDrawObjects.Count - 1; i >= 0; i--)
        {
            var obj = allDrawObjects[i];
            if (obj.parentObject) Destroy(obj.parentObject);
            else ClearVisuals(obj); // Fallback
        }
        allDrawObjects.Clear();
        drawingCounter = 0;
        ResetState();
    }

    public void LoadPolygon(List<Vector2> coords)
    {
        if (coords == null || coords.Count == 0) return;
        
        var obj = new DrawObject
        {
            id = System.Guid.NewGuid().ToString(),
            type = DrawMode.Polygon,
            coordinates = new List<Vector2>(coords),
            parentObject = CreateParent($"Loaded_Polygon")
        };
        
        RebuildVisuals(obj, false);
        allDrawObjects.Add(obj);
    }

    public string ExportToJSON() => JsonUtility.ToJson(new Wrapper { objects = allDrawObjects });
    [System.Serializable] class Wrapper { public List<DrawObject> objects; }
}