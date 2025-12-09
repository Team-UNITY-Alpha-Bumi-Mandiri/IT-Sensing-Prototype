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
    public GameObject pointMarkerPrefab, lineSegmentPrefab, polygonFillPrefab, vertexPointPrefab;
    public GameObject tooltipPrefab; // Prefab untuk tooltip (dengan TMP_Text)

    [Header("Settings")]
    public Color pointColor = Color.blue, lineColor = Color.red;
    public Color polygonLineColor = Color.green, polygonFillColor = new Color(0, 1, 0, 0.3f);
    public float doubleClickThreshold = 0.3f;
    public float snapDistancePixels = 30f; // Jarak snap untuk finish (dalam pixel)

    public enum DrawMode { Point, Line, Polygon }
    public DrawMode currentMode = DrawMode.Point;

    // State
    bool isDrawing, isToolActive;
    float lastClickTime; 
    DrawMode activeMode = DrawMode.Point;
    int drawingCounter;
    double lastLat, lastLon;
    int lastZoom;

    // Objects
    List<DrawObject> allDrawObjects = new List<DrawObject>();
    DrawObject currentDrawObject;
    GameObject ghostLine;
    RectTransform ghostLineRect;
    GameObject tooltipObj;
    TMP_Text tooltipText;
    RectTransform tooltipRect;
  
    [System.Serializable]
    public class DrawObject
    {
        public string id;
        public DrawMode type;
        public List<Vector2> coordinates = new List<Vector2>();
        public List<GameObject> visualObjects = new List<GameObject>();
        public GameObject fillObject, parentObject;
    }

    void Start() 
    { 
        if (container) 
        {
            CreateGhostLine(); 
            CreateTooltip();
        }
    }

    void Update()
    {
        if (!mapController || !container) return;

        if (mapController.latitude != lastLat || mapController.longitude != lastLon || mapController.zoom != lastZoom)
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
        if (!isToolActive || Mouse.current == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Ghost preview
        if (isDrawing && currentDrawObject?.coordinates.Count > 0)
            UpdateGhostPreview(mousePos);
        else
            HideGhost();

        // Update tooltip (untuk Line/Polygon mode)
        UpdateTooltip(mousePos);

        // Left click
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 latLon = mapController.ScreenToLatLon(mousePos);
            AddPoint(latLon, mousePos);
        }

        // Right click = Undo
        if (Mouse.current.rightButton.wasPressedThisFrame) UndoLast();

        // ESC = Cancel
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && isDrawing) CancelDrawing();
    }

    void AddPoint(Vector2 latLon, Vector2 screenPos)
    {
        // MODE POINT: langsung buat titik
        if (currentMode == DrawMode.Point)
        {
            CreatePointObject(latLon);
            return;
        }

        // Cek snap-to-finish jika sudah ada minimal titik
        if (isDrawing && currentDrawObject != null && currentDrawObject.coordinates.Count >= 2)
        {
            // LINE MODE: Klik titik terakhir → finish
            if (currentMode == DrawMode.Line)
            {
                var lastCoord = currentDrawObject.coordinates[^1];
                var lastScreenPos = LatLonToScreenPos(lastCoord);
                if (Vector2.Distance(screenPos, lastScreenPos) < snapDistancePixels)
                {
                    FinishDrawing();
                    return;
                }
            }
            // POLYGON MODE: Klik titik pertama → close & finish (minimal 3 titik)
            else if (currentMode == DrawMode.Polygon && currentDrawObject.coordinates.Count >= 3)
            {
                var firstCoord = currentDrawObject.coordinates[0];
                var firstScreenPos = LatLonToScreenPos(firstCoord);
                if (Vector2.Distance(screenPos, firstScreenPos) < snapDistancePixels)
                {
                    FinishDrawing();
                    return;
                }
            }
        }

        // Tambah titik baru
        if (!isDrawing) StartNewDrawObject();
        currentDrawObject.coordinates.Add(latLon);
        RebuildVisuals(currentDrawObject, true);
    }

    Vector2 LatLonToScreenPos(Vector2 latLon)
    {
        var localPos = mapController.LatLonToLocalPosition(latLon.x, latLon.y);
        return RectTransformUtility.WorldToScreenPoint(null, container.TransformPoint(localPos));
    }

    void StartNewDrawObject()
    {
        isDrawing = true;
        drawingCounter++;
        currentDrawObject = new DrawObject
        {
            id = System.Guid.NewGuid().ToString(),
            type = currentMode,
            parentObject = CreateParent($"Drawing_{drawingCounter}_{currentMode}")
        };
    }

    GameObject CreateParent(string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(container, false);
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return obj;
    }

    void FinishDrawing()
    {
        if (currentDrawObject == null || currentDrawObject.coordinates.Count < 2 ||
            (currentMode == DrawMode.Polygon && currentDrawObject.coordinates.Count < 3))
        {
            CancelDrawing();
            return;
        }

        allDrawObjects.Add(currentDrawObject);
        ResetState();
        Debug.Log("Drawing selesai!");
    }

    void CancelDrawing()
    {
        if (currentDrawObject?.parentObject) Destroy(currentDrawObject.parentObject);
        else ClearVisuals(currentDrawObject);
        
        if (currentDrawObject != null) drawingCounter--;
        ResetState();
        Debug.Log("Drawing dibatalkan.");
    }

    void ResetState()
    {
        currentDrawObject = null;
        isDrawing = false;
        isToolActive = false;
        activeMode = DrawMode.Point;
        HideGhost();
        HideTooltip();
    }

    void CreatePointObject(Vector2 latLon)
    {
        drawingCounter++;
        var obj = new DrawObject
        {
            id = System.Guid.NewGuid().ToString(),
            type = DrawMode.Point,
            coordinates = new List<Vector2> { latLon },
            parentObject = CreateParent($"Drawing_{drawingCounter}_Point")
        };

        var marker = SpawnMarker(pointMarkerPrefab, latLon, obj.parentObject.transform, pointColor);
        obj.visualObjects.Add(marker);
        allDrawObjects.Add(obj);
    }

    GameObject SpawnMarker(GameObject prefab, Vector2 latLon, Transform parent, Color color)
    {
        var pos = mapController.LatLonToLocalPosition(latLon.x, latLon.y);
        var marker = Instantiate(prefab, parent);
        marker.GetComponent<RectTransform>().anchoredPosition = pos;
        var img = marker.GetComponent<Image>();
        if (img) img.color = color;
        return marker;
    }

    void RebuildVisuals(DrawObject obj, bool isCurrent = false)
    {
        ClearVisuals(obj);
        if (obj.coordinates.Count == 0) return;

        var parent = obj.parentObject ? obj.parentObject.transform : container.transform;
        var color = obj.type == DrawMode.Line ? lineColor : polygonLineColor;

        if (obj.type == DrawMode.Point)
        {
            obj.visualObjects.Add(SpawnMarker(pointMarkerPrefab, obj.coordinates[0], parent, pointColor));
            return;
        }

        // Vertices
        foreach (var coord in obj.coordinates)
            obj.visualObjects.Add(SpawnMarker(vertexPointPrefab, coord, parent, color));

        // Lines
        for (int i = 1; i < obj.coordinates.Count; i++)
            obj.visualObjects.Add(CreateLine(obj.coordinates[i - 1], obj.coordinates[i], parent, color));

        // Polygon close + fill
        if (obj.type == DrawMode.Polygon && obj.coordinates.Count >= 3)
        {
            obj.visualObjects.Add(CreateLine(obj.coordinates[^1], obj.coordinates[0], parent, polygonLineColor));
            CreatePolygonFill(obj);
        }
    }

    GameObject CreateLine(Vector2 start, Vector2 end, Transform parent, Color color)
    {
        var line = Instantiate(lineSegmentPrefab, parent);
        DrawLineUI(line.GetComponent<RectTransform>(), start, end);
        var img = line.GetComponent<Image>();
        if (img) img.color = color;
        return line;
    }

    void DrawLineUI(RectTransform rt, Vector2 startLatLon, Vector2 endLatLon)
    {
        if (!rt) return;
        var startPos = mapController.LatLonToLocalPosition(startLatLon.x, startLatLon.y);
        var endPos = mapController.LatLonToLocalPosition(endLatLon.x, endLatLon.y);
        var dir = endPos - startPos;

        rt.anchoredPosition = startPos + dir * 0.5f;
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
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = container.sizeDelta;

        var positions = new List<Vector2>();
        foreach (var c in obj.coordinates)
            positions.Add(mapController.LatLonToLocalPosition(c.x, c.y));

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

    void CreateGhostLine()
    {
        if (ghostLine || !lineSegmentPrefab) return;
        ghostLine = Instantiate(lineSegmentPrefab, container);
        ghostLineRect = ghostLine.GetComponent<RectTransform>();
        var img = ghostLine.GetComponent<Image>();
        if (img) { img.color = new Color(1, 1, 1, 0.5f); img.raycastTarget = false; }
        ghostLine.SetActive(false);
    }

    void UpdateGhostPreview(Vector2 mousePos)
    {
        if (currentDrawObject?.coordinates.Count == 0) { HideGhost(); return; }

        var lastCoord = currentDrawObject.coordinates[^1];
        var mouseLatLon = mapController.ScreenToLatLon(mousePos);

        if (ghostLine)
        {
            DrawLineUI(ghostLineRect, lastCoord, mouseLatLon);
            var img = ghostLine.GetComponent<Image>();
            var col = currentMode == DrawMode.Line ? lineColor : polygonLineColor;
            if (img) img.color = new Color(col.r, col.g, col.b, 0.5f);
            ghostLine.SetActive(true);
            ghostLine.transform.SetAsLastSibling();
        }
    }

    void HideGhost() { if (ghostLine) ghostLine.SetActive(false); }

    // ========== TOOLTIP ==========
    void CreateTooltip()
    {
        if (tooltipObj || !tooltipPrefab) return;
        tooltipObj = Instantiate(tooltipPrefab, container);
        tooltipRect = tooltipObj.GetComponent<RectTransform>();
        tooltipText = tooltipObj.GetComponentInChildren<TMP_Text>();
        
        var img = tooltipObj.GetComponent<Image>();
        if (img) img.raycastTarget = false;
        if (tooltipText) tooltipText.raycastTarget = false;
        
        tooltipObj.SetActive(false);
    }

    void UpdateTooltip(Vector2 mousePos)
    {
        // Tooltip untuk Line dan Polygon mode
        if ((currentMode != DrawMode.Line && currentMode != DrawMode.Polygon) || !tooltipObj) 
        {
            HideTooltip();
            return;
        }

        // Posisikan tooltip di kanan kursor
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mousePos, null, out Vector2 localPos))
        {
            tooltipRect.anchoredPosition = localPos + new Vector2(25, -15);
        }

        string message;
        int pointCount = currentDrawObject?.coordinates.Count ?? 0;
        var mouseLatLon = mapController.ScreenToLatLon(mousePos);

        if (currentMode == DrawMode.Line)
        {
            // === LINE MODE ===
            if (pointCount == 0)
            {
                message = "Click to start drawing line";
            }
            else if (pointCount == 1)
            {
                var lastCoord = currentDrawObject.coordinates[^1];
                float dist = CalculateDistance(lastCoord, mouseLatLon);
                message = $"{dist:F2} km\nClick to continue drawing line";
            }
            else
            {
                float totalDist = GetTotalDistance();
                var lastCoord = currentDrawObject.coordinates[^1];
                float currentDist = CalculateDistance(lastCoord, mouseLatLon);
                message = $"{totalDist + currentDist:F2} km\nClick last point to finish line";
            }
        }
        else
        {
            // === POLYGON MODE ===
            if (pointCount == 0)
            {
                message = "Click to start drawing shape";
            }
            else if (pointCount < 3)
            {
                message = "Click to continue drawing shape";
            }
            else
            {
                // Hitung area dengan titik sementara (mouse position)
                var tempCoords = new List<Vector2>(currentDrawObject.coordinates) { mouseLatLon };
                float area = CalculatePolygonArea(tempCoords);
                message = $"Area: {FormatArea(area)}\nClick first point to close this shape";
            }
        }

        if (tooltipText) tooltipText.text = message;
        tooltipObj.SetActive(true);
        tooltipObj.transform.SetAsLastSibling();
    }

    void HideTooltip() { if (tooltipObj) tooltipObj.SetActive(false); }

    float CalculateDistance(Vector2 p1, Vector2 p2)
    {
        float R = 6371f; // Radius bumi dalam km
        float dLat = (p2.x - p1.x) * Mathf.Deg2Rad;
        float dLon = (p2.y - p1.y) * Mathf.Deg2Rad;
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(p1.x * Mathf.Deg2Rad) * Mathf.Cos(p2.x * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
        return R * c;
    }

    float GetTotalDistance()
    {
        if (currentDrawObject == null || currentDrawObject.coordinates.Count < 2) return 0;
        float total = 0;
        for (int i = 1; i < currentDrawObject.coordinates.Count; i++)
            total += CalculateDistance(currentDrawObject.coordinates[i - 1], currentDrawObject.coordinates[i]);
        return total;
    }

    // Shoelace formula untuk menghitung area polygon (dalam km²)
    float CalculatePolygonArea(List<Vector2> coords)
    {
        if (coords.Count < 3) return 0;

        // Konversi lat/lon ke koordinat planar (approximate, cukup untuk area kecil-menengah)
        float avgLat = 0;
        foreach (var c in coords) avgLat += c.x;
        avgLat /= coords.Count;

        float kmPerDegreeLat = 111.32f; // km per derajat latitude
        float kmPerDegreeLon = 111.32f * Mathf.Cos(avgLat * Mathf.Deg2Rad); // km per derajat longitude

        // Konversi ke km
        List<Vector2> kmCoords = new List<Vector2>();
        foreach (var c in coords)
            kmCoords.Add(new Vector2(c.y * kmPerDegreeLon, c.x * kmPerDegreeLat)); // (x=lon, y=lat)

        // Shoelace formula
        float area = 0;
        int n = kmCoords.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += kmCoords[i].x * kmCoords[j].y;
            area -= kmCoords[j].x * kmCoords[i].y;
        }
        return Mathf.Abs(area) / 2f;
    }

    string FormatArea(float areaKm2)
    {
        if (areaKm2 < 1)
            return $"{areaKm2 * 1000000:F0} m²"; // Tampilkan dalam m² jika < 1 km²
        else
            return $"{areaKm2:F2} km²";
    }


    public void RebuildAllVisuals()
    {
        foreach (var obj in allDrawObjects) RebuildVisuals(obj);
        if (currentDrawObject != null) RebuildVisuals(currentDrawObject, true);
    }

    // PUBLIC API
    public void ActivateMode(DrawMode mode)
    {
        if (isDrawing && activeMode != mode) CancelDrawing();
        isToolActive = true;
        activeMode = currentMode = mode;
        Debug.Log($"DrawTool Activated: {mode}");
    }

    public void DeactivateMode(DrawMode mode)
    {
        if (activeMode != mode) return;
        if (isDrawing) CancelDrawing();
        isToolActive = false;
        Debug.Log($"DrawTool Deactivated: {mode}");
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
            Debug.Log("Undo: Titik dihapus");
        }
        else if (allDrawObjects.Count > 0)
        {
            var last = allDrawObjects[^1];
            if (last.parentObject) Destroy(last.parentObject);
            else ClearVisuals(last);
            allDrawObjects.RemoveAt(allDrawObjects.Count - 1);
            Debug.Log("Undo: Objek dihapus");
        }
    }

    public string ExportToJSON() => JsonUtility.ToJson(new DrawObjectListWrapper { objects = allDrawObjects });

    [System.Serializable]
    class DrawObjectListWrapper { public List<DrawObject> objects; }
}