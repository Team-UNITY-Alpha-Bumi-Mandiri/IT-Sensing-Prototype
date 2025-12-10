using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;

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

    public enum DrawMode { Point, Line, Polygon }
    public DrawMode currentMode = DrawMode.Point;

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

        // Left click = Add point
        if (Mouse.current.leftButton.wasPressedThisFrame)
            AddPoint(mapController.ScreenToLatLon(mousePos), mousePos);
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
            else
            {
                var temp = new List<Vector2>(currentDrawObject.coordinates) { mouseLL };
                msg = $"Area: {FormatArea(CalcArea(temp))}\nClick first point to close";
            }
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

    float CalcArea(List<Vector2> coords)
    {
        if (coords.Count < 3) return 0;
        float avgLat = 0;
        foreach (var c in coords) avgLat += c.x;
        avgLat /= coords.Count;

        float kmLat = 111.32f, kmLon = 111.32f * Mathf.Cos(avgLat * Mathf.Deg2Rad);
        var km = new List<Vector2>();
        foreach (var c in coords) km.Add(new Vector2(c.y * kmLon, c.x * kmLat));

        float area = 0;
        for (int i = 0; i < km.Count; i++)
        {
            int j = (i + 1) % km.Count;
            area += km[i].x * km[j].y - km[j].x * km[i].y;
        }
        return Mathf.Abs(area) / 2f;
    }

    string FormatArea(float km2) => km2 < 1 ? $"{km2 * 1000000:F0} m²" : $"{km2:F2} km²";

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

    public string ExportToJSON() => JsonUtility.ToJson(new Wrapper { objects = allDrawObjects });
    [System.Serializable] class Wrapper { public List<DrawObject> objects; }
}