using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Events;
using System.IO;

// Tool utama untuk menggambar Titik, Garis, dan Polygon di peta
public class DrawTool : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;
    public RectTransform container;

    [Header("Prefabs")]
    public GameObject pointMarkerPrefab, lineSegmentPrefab, polygonFillPrefab, vertexPointPrefab, tooltipPrefab;

    [Header("Settings")]
    public float snapDistancePixels = 30f;
    Color point = Color.blue, vertex = Color.white;
    Color line = new Color(0, 0.424f, 1), polyLine = new Color(0, 0.424f, 1), polyFill = new Color(0, 0.424f, 1, 0.3f);
    public bool forceTextureOnNext = false;
    public enum DrawMode { Point, Line, Polygon, Delete }
    public DrawMode currentMode = DrawMode.Point;
    public UnityEvent<DrawObject> onDrawComplete;

    [Header("Custom Texture")]
    public string customTexturePath = "Assets/Image Map/mapa del mundo pixel art.jpg";
    private Texture2D cachedTex;

    // State
    bool isDrawing, isToolActive;
    DrawMode activeMode = DrawMode.Point;
    int drawingCounter;
    double lastLat, lastLon;
    int lastZoom;

    List<DrawObject> allObjs = new();
    DrawObject currentObj;
    GameObject ghost, tooltip;
    RectTransform ghostRT, tooltipRT;
    TMP_Text tooltipTxt;

    [System.Serializable]
    public class DrawObject {
        public string id = System.Guid.NewGuid().ToString();
        public DrawMode type;
        public bool useTexture;
        public List<Vector2> coordinates = new();
        public List<GameObject> visualObjects = new();
        public GameObject fillObject, parentObject;
    }

    void Start() {
        if (!container) return;
        CreateGhost(); CreateTooltip(); LoadTex();
    }

    // Load tekstur peta custom dari file lokal
    void LoadTex() {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, customTexturePath);
        if (!File.Exists(path)) path = customTexturePath;
        if (File.Exists(path)) {
            cachedTex = new Texture2D(2, 2);
            cachedTex.LoadImage(File.ReadAllBytes(path));
            cachedTex.wrapMode = TextureWrapMode.Clamp;
        }
    }

    void Update() {
        if (!mapController || !container) return;
        // Rebuild visual jika peta digeser/zoom agar posisi tetap akurat
        if (mapController.latitude != lastLat || mapController.longitude != lastLon || mapController.zoom != lastZoom) {
            RebuildAllVisuals();
            (lastLat, lastLon, lastZoom) = (mapController.latitude, mapController.longitude, mapController.zoom);
        }
        HandleInput();
    }

    // Handle input mouse (Klik Kiri: Gambar/Hapus, Kanan: Undo, ESC: Batal)
    void HandleInput() {
        if (!isToolActive || Mouse.current == null) return;
        Vector2 mPos = Mouse.current.position.ReadValue();
        
        if (isDrawing && currentObj?.coordinates.Count > 0) UpdateGhost(mPos); else HideGhost();
        UpdateTooltip(mPos);

        if (Mouse.current.leftButton.wasPressedThisFrame) {
            if (activeMode == DrawMode.Delete) HandleDelete(mPos);
            else AddPoint(mapController.ScreenToLatLon(mPos), mPos);
        }
        if (Mouse.current.rightButton.wasPressedThisFrame) UndoLast();
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && isDrawing) CancelDrawing();
    }

    // Tambah titik baru dengan fitur Snapping otomatis
    void AddPoint(Vector2 latLon, Vector2 screenPos) {
        if (currentMode == DrawMode.Point) { CreatePointObj(latLon); return; }
        if (isDrawing && currentObj != null && currentObj.coordinates.Count >= 2) {
            // Cek snapping ke titik awal (untuk menutup polygon) atau titik akhir (garis)
            Vector2 target = currentMode == DrawMode.Line ? currentObj.coordinates[^1] : currentObj.coordinates[0];
            if ((currentMode == DrawMode.Line || currentObj.coordinates.Count >= 3) && 
                Vector2.Distance(screenPos, LLToScreen(target)) < snapDistancePixels) { FinishDrawing(); return; }
        }
        if (!isDrawing) StartDrawObj();
        currentObj.coordinates.Add(latLon);
        RebuildVisuals(currentObj, true);
    }

    Vector2 LLToScreen(Vector2 ll) => RectTransformUtility.WorldToScreenPoint(null, container.TransformPoint(mapController.LatLonToLocalPosition(ll.x, ll.y)));

    void StartDrawObj() {
        isDrawing = true;
        currentObj = new DrawObject { type = currentMode, useTexture = forceTextureOnNext, parentObject = CreateParent($"Draw_{++drawingCounter}_{currentMode}") };
    }

    GameObject CreateParent(string name) {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(container, false);
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.offsetMin = rt.offsetMax = Vector2.zero; rt.anchorMax = Vector2.one;
        return obj;
    }

    // Selesaikan gambar saat ini dan reset state
    void FinishDrawing() {
        if (currentObj == null || currentObj.coordinates.Count < (currentMode == DrawMode.Polygon ? 3 : 2)) { CancelDrawing(); return; }
        RebuildVisuals(currentObj, false); allObjs.Add(currentObj); onDrawComplete?.Invoke(currentObj); ResetState();
    }

    void CancelDrawing() {
        if (currentObj?.parentObject) Destroy(currentObj.parentObject); else ClearVisuals(currentObj);
        if (currentObj != null) drawingCounter--;
        ResetState();
    }

    void ResetState() {
        currentObj = null; isDrawing = isToolActive = forceTextureOnNext = false;
        activeMode = DrawMode.Point; HideGhost(); HideTooltip();
    }

    // Hapus objek yang diklik (Point, Garis, atau Polygon)
    void HandleDelete(Vector2 mPos) {
        DrawObject target = null;
        for (int i = allObjs.Count - 1; i >= 0; i--) {
            var obj = allObjs[i];
            if (obj.coordinates.Count == 0) continue;
            var pts = new List<Vector2>();
            foreach(var c in obj.coordinates) pts.Add(LLToScreen(c));

            bool hit = obj.type == DrawMode.Point ? Vector2.Distance(mPos, pts[0]) < snapDistancePixels : false;
            if (!hit && obj.type != DrawMode.Point) {
                // Cek jarak ke garis atau dalam polygon
                for (int k = 0; k < pts.Count - 1; k++) if (GetLineDist(mPos, pts[k], pts[k+1]) < snapDistancePixels) { hit = true; break; }
                if (!hit && obj.type == DrawMode.Polygon && pts.Count >= 3 && (GetLineDist(mPos, pts[^1], pts[0]) < snapDistancePixels || IsInPoly(mPos, pts))) hit = true;
            }
            if (hit) { target = obj; break; }
        }
        if (target != null) {
            if (target.parentObject) Destroy(target.parentObject); else ClearVisuals(target);
            allObjs.Remove(target);
        }
    }

    bool IsInPoly(Vector2 p, List<Vector2> poly) {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) inside = !inside;
        return inside;
    }

    float GetLineDist(Vector2 p, Vector2 a, Vector2 b) {
        Vector2 n = b - a; float len2 = n.sqrMagnitude;
        if (len2 == 0) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, n) / len2);
        return Vector2.Distance(p, a + n * t);
    }

    void CreatePointObj(Vector2 ll) {
        var obj = new DrawObject { type = DrawMode.Point, coordinates = { ll }, parentObject = CreateParent($"Draw_{++drawingCounter}_Pt") };
        obj.visualObjects.Add(SpawnMarker(pointMarkerPrefab, ll, obj.parentObject.transform, point));
        allObjs.Add(obj);
    }

    GameObject SpawnMarker(GameObject prefab, Vector2 ll, Transform p, Color c) {
        var m = Instantiate(prefab, p);
        m.GetComponent<RectTransform>().anchoredPosition = mapController.LatLonToLocalPosition(ll.x, ll.y);
        if (m.TryGetComponent<Image>(out var img)) img.color = c;
        return m;
    }

    // Re-render semua elemen visual (titik, garis, fill)
    void RebuildVisuals(DrawObject obj, bool isCur = false) {
        ClearVisuals(obj);
        if (obj.coordinates.Count == 0) return;
        Transform p = obj.parentObject ? obj.parentObject.transform : container;
        Color c = obj.type == DrawMode.Line ? line : polyLine;

        if (obj.type == DrawMode.Point) { obj.visualObjects.Add(SpawnMarker(pointMarkerPrefab, obj.coordinates[0], p, point)); return; }
        if (isCur) foreach (var coord in obj.coordinates) obj.visualObjects.Add(SpawnMarker(vertexPointPrefab, coord, p, vertex));
        
        for (int i = 1; i < obj.coordinates.Count; i++) obj.visualObjects.Add(CreateLine(obj.coordinates[i - 1], obj.coordinates[i], p, c));

        if (obj.type == DrawMode.Polygon && obj.coordinates.Count >= 3) {
            if (!isCur) obj.visualObjects.Add(CreateLine(obj.coordinates[^1], obj.coordinates[0], p, polyLine));
            CreateFill(obj);
        }
    }

    GameObject CreateLine(Vector2 s, Vector2 e, Transform p, Color c) {
        var l = Instantiate(lineSegmentPrefab, p);
        SetLineVars(l.GetComponent<RectTransform>(), s, e);
        if (l.TryGetComponent<Image>(out var img)) img.color = c;
        return l;
    }

    void SetLineVars(RectTransform rt, Vector2 sLL, Vector2 eLL) {
        var s = mapController.LatLonToLocalPosition(sLL.x, sLL.y);
        var dir = mapController.LatLonToLocalPosition(eLL.x, eLL.y) - s;
        rt.anchoredPosition = s + dir * 0.5f;
        rt.sizeDelta = new Vector2(dir.magnitude, rt.sizeDelta.y);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // Buat fill polygon (warna solid atau tekstur)
    void CreateFill(DrawObject obj) {
        if (obj.coordinates.Count < 3) return;
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIPolygonRenderer));
        fill.transform.SetParent(obj.parentObject ? obj.parentObject.transform : container, false);
        fill.transform.SetAsFirstSibling();
        
        var rt = fill.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = container.sizeDelta;

        var pts = new List<Vector2>();
        foreach (var c in obj.coordinates) pts.Add(mapController.LatLonToLocalPosition(c.x, c.y));

        var r = fill.GetComponent<UIPolygonRenderer>();
        if (obj.useTexture && cachedTex) { r.SetPolygon(pts, Color.white); r.texture = cachedTex; }
        else r.SetPolygon(pts, polyFill);
        
        r.raycastTarget = false; obj.fillObject = fill;
    }

    void ClearVisuals(DrawObject obj) {
        if (obj == null) return;
        foreach (var v in obj.visualObjects) if (v) Destroy(v);
        obj.visualObjects.Clear();
        if (obj.fillObject) { Destroy(obj.fillObject); obj.fillObject = null; }
    }

    void CreateGhost() {
        if (ghost || !lineSegmentPrefab) return;
        ghost = Instantiate(lineSegmentPrefab, container);
        ghostRT = ghost.GetComponent<RectTransform>();
        if (ghost.TryGetComponent<Image>(out var img)) { img.color = new Color(1, 1, 1, 0.5f); img.raycastTarget = false; }
        ghost.SetActive(false);
    }

    void UpdateGhost(Vector2 mPos) {
        if (currentObj?.coordinates.Count == 0 || !ghost) { HideGhost(); return; }
        SetLineVars(ghostRT, currentObj.coordinates[^1], mapController.ScreenToLatLon(mPos));
        var c = currentMode == DrawMode.Line ? line : polyLine;
        if (ghost.TryGetComponent<Image>(out var img)) img.color = new Color(c.r, c.g, c.b, 0.5f);
        ghost.SetActive(true); ghost.transform.SetAsLastSibling();
    }

    void HideGhost() => ghost?.SetActive(false);

    void CreateTooltip() {
        if (tooltip || !tooltipPrefab) return;
        tooltip = Instantiate(tooltipPrefab, container);
        tooltipRT = tooltip.GetComponent<RectTransform>();
        tooltipTxt = tooltip.GetComponentInChildren<TMP_Text>();
        if (tooltip.TryGetComponent<Image>(out var img)) img.raycastTarget = false;
        if (tooltipTxt) tooltipTxt.raycastTarget = false;
        tooltip.SetActive(false);
    }

    void UpdateTooltip(Vector2 mPos) {
        if (currentMode == DrawMode.Point || !tooltip) { HideTooltip(); return; }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mPos, null, out var lp)) tooltipRT.anchoredPosition = lp + new Vector2(25, -15);

        int pts = currentObj?.coordinates.Count ?? 0;
        var mLL = mapController.ScreenToLatLon(mPos);
        string msg = currentMode == DrawMode.Line 
            ? (pts == 0 ? "Start line" : pts == 1 ? $"{CalcDist(currentObj.coordinates[^1], mLL):F2} km" : $"{GetTotalDist() + CalcDist(currentObj.coordinates[^1], mLL):F2} km") 
            : (pts == 0 ? "Start shape" : pts < 3 ? "Continue" : "Close");

        if (tooltipTxt) tooltipTxt.text = msg;
        tooltip.SetActive(true); tooltip.transform.SetAsLastSibling();
    }

    void HideTooltip() => tooltip?.SetActive(false);

    // Hitung jarak Haversine (KM)
    float CalcDist(Vector2 p1, Vector2 p2) {
        float R = 6371f, dLat = (p2.x - p1.x) * Mathf.Deg2Rad, dLon = (p2.y - p1.y) * Mathf.Deg2Rad;
        float a = Mathf.Sin(dLat/2)*Mathf.Sin(dLat/2) + Mathf.Cos(p1.x*Mathf.Deg2Rad)*Mathf.Cos(p2.x*Mathf.Deg2Rad)*Mathf.Sin(dLon/2)*Mathf.Sin(dLon/2);
        return R * 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
    }

    float GetTotalDist() {
        if (currentObj == null || currentObj.coordinates.Count < 2) return 0;
        float t = 0;
        for (int i = 1; i < currentObj.coordinates.Count; i++) t += CalcDist(currentObj.coordinates[i - 1], currentObj.coordinates[i]);
        return t;
    }

    public void RebuildAllVisuals() { foreach (var obj in allObjs) RebuildVisuals(obj); if (currentObj != null) RebuildVisuals(currentObj, true); }
    public void ActivateMode(DrawMode m) { if (isDrawing && activeMode != m) CancelDrawing(); isToolActive = true; activeMode = currentMode = m; }
    public void DeactivateMode(DrawMode m) { if (activeMode != m) return; if (isDrawing) CancelDrawing(); isToolActive = false; }
    public bool IsModeActive(DrawMode m) => isToolActive && activeMode == m;
    public bool IsCurrentlyDrawing() => isDrawing;

    public void UndoLast() {
        if (isDrawing && currentObj?.coordinates.Count > 0) {
            currentObj.coordinates.RemoveAt(currentObj.coordinates.Count - 1);
            if (currentObj.coordinates.Count == 0) CancelDrawing(); else RebuildVisuals(currentObj, true);
        } else if (allObjs.Count > 0) {
            var last = allObjs[^1];
            if (last.parentObject) Destroy(last.parentObject); else ClearVisuals(last);
            allObjs.RemoveAt(allObjs.Count - 1);
        }
    }

    public void ClearAll() {
        for (int i = allObjs.Count - 1; i >= 0; i--) {
            if (allObjs[i].parentObject) Destroy(allObjs[i].parentObject); else ClearVisuals(allObjs[i]);
        }
        allObjs.Clear(); drawingCounter = 0; ResetState();
    }

    public void LoadPolygon(List<Vector2> coords, bool useTexture = false) {
        if (coords == null || coords.Count == 0) return;
        var obj = new DrawObject { type = DrawMode.Polygon, useTexture = useTexture, coordinates = new List<Vector2>(coords), parentObject = CreateParent("Loaded") };
        RebuildVisuals(obj, false); allObjs.Add(obj);
    }

    public string ExportToJSON() => JsonUtility.ToJson(new Wrapper { objects = allObjs });
    [System.Serializable] class Wrapper { public List<DrawObject> objects; }
}