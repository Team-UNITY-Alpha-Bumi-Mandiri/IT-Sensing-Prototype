using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Events;
using System.IO;

// Tool utama untuk menggambar Titik, Garis, dan Polygon di atas peta.
public class DrawTool : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController; // Referensi Peta
    public RectTransform container; // Wadah objek gambar (UI Parent)

    [Header("Prefabs")]
    public GameObject pointMarkerPrefab; // Prefab titik
    public GameObject lineSegmentPrefab; // Prefab segmen garis
    public GameObject polygonFillPrefab; // Prefab isi polygon (tidak dipakai langsung krn generate mesh)
    public GameObject vertexPointPrefab; // Prefab titik sudut polygon
    public GameObject tooltipPrefab;     // Prefab tooltip info

    [Header("Settings")]
    public float snapDist = 30f; // Jarak snapping (pixel)
    public bool forceTextureOnNext = false; // Flag untuk pakai tekstur di gambar berikutnya
    public enum DrawMode { Point, Line, Polygon, Delete }
    public UnityEvent<DrawObject> onDrawComplete; // Event saat selesai gambar
    
    [Header("Visuals")]
    Color colPoint = Color.blue, colVertex = Color.white;
    Color colLine = new Color(0, 0.42f, 1), colFill = new Color(0, 0.42f, 1, 0.3f);
    public string customTexturePath = "Assets/Image Map/mapa del mundo pixel art.jpg";
    private Texture2D cachedTex;

    // State Internal
    bool isDrawing, isToolActive;
    DrawMode currentMode = DrawMode.Point;
    int drawIdCounter;
    double lastLat, lastLon; int lastZoom; // Cache posisi peta untuk update visual

    // Data Objek
    List<DrawObject> allObjs = new(); // Semua objek yg sudah digambar
    DrawObject activeObj; // Objek yang sedang digambar
    
    // UI Helper
    GameObject ghostObj, tooltipObj;
    TMP_Text tooltipText;

    // Kelas pembungkus data gambar
    [System.Serializable]
    public class DrawObject {
        public string id = System.Guid.NewGuid().ToString();
        public DrawMode type;
        public bool useTexture;
        public List<Vector2> coordinates = new(); // Data Geo-Spasial (Lat, Lon)
        public List<GameObject> visuals = new();  // Referensi objek UI (Line, Dot)
        public GameObject rootObj; // Parent GameObject di Hierarchy
    }

    void Start() {
        if (!container) return;
        CreateHelpers(); // Buat Ghost & Tooltip
        LoadTexture();   // Muat tekstur custom
    }

    // Muat tekstur dari file sistem
    void LoadTexture() {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, customTexturePath);
        if (!File.Exists(path)) path = customTexturePath; // Fallback path relatif
        if (File.Exists(path)) {
            cachedTex = new Texture2D(2, 2);
            cachedTex.LoadImage(File.ReadAllBytes(path));
        }
    }

    void Update() {
        if (!mapController || !container) return;
        
        // Cek jika peta bergerak -> Rebuild posisi visual agar tetap nempel di lokasi geografis
        if (mapController.latitude != lastLat || mapController.longitude != lastLon || mapController.zoom != lastZoom) {
            RefreshAllVisuals();
            (lastLat, lastLon, lastZoom) = (mapController.latitude, mapController.longitude, mapController.zoom);
        }
        
        InputHandler(); // Proses Input Mouse/Keyboard
    }

    // --- LOGIKA INPUT ---

    void InputHandler() {
        if (!isToolActive || Mouse.current == null) return;
        Vector2 mPos = Mouse.current.position.ReadValue();
        
        // Update Ghost Line & Tooltip
        if (isDrawing && activeObj?.coordinates.Count > 0) UpdateGhost(mPos); else ghostObj?.SetActive(false);
        UpdateTooltip(mPos);

        // Klik Kiri: Tambah Titik / Hapus
        if (Mouse.current.leftButton.wasPressedThisFrame) {
            if (currentMode == DrawMode.Delete) TryDelete(mPos);
            else AddPoint(mapController.ScreenToLatLon(mPos), mPos);
        }
        // Klik Kanan: Undo titik terakhir
        if (Mouse.current.rightButton.wasPressedThisFrame) Undo();
        // ESC: Batal gambar
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && isDrawing) CancelDraw();
    }

    // --- LOGIKA GAMBAR (DRAWING) ---

    void AddPoint(Vector2 latLon, Vector2 screenPos) {
        // Mode Titik: Langsung jadi
        if (currentMode == DrawMode.Point) { 
            CreateObject(DrawMode.Point, new List<Vector2>{ latLon }); 
            onDrawComplete?.Invoke(allObjs[^1]); // Trigger event
            return; 
        }

        // Mode Garis/Polygon: Mulai baru jika belum
        if (!isDrawing) StartDrawing();

        // Cek Snapping untuk Finish (Tutup Loop / Akhiri Garis)
        if (activeObj.coordinates.Count >= (currentMode == DrawMode.Polygon ? 3 : 2)) {
            Vector2 target = activeObj.coordinates[currentMode == DrawMode.Line ? ^1 : 0]; // Snap ke awal(Poly) atau akhir(Line)
            if (Vector2.Distance(screenPos, GeoToScreen(target)) < snapDist) { FinishDrawing(); return; }
        }

        // Tambah titik
        activeObj.coordinates.Add(latLon);
        RebuildVisual(activeObj, true); // Update tampilan
    }

    // Mulai sesi gambar baru
    void StartDrawing() {
        isDrawing = true;
        activeObj = new DrawObject { type = currentMode, useTexture = forceTextureOnNext, rootObj = CreateRoot($"Draw_{++drawIdCounter}_{currentMode}") };
    }

    // Selesai gambar -> Simpan ke list permanen
    void FinishDrawing() {
        if (activeObj == null) return;
        RebuildVisual(activeObj, false); // Finalize visual (tutup polygon)
        allObjs.Add(activeObj);
        onDrawComplete?.Invoke(activeObj);
        ResetState();
    }

    // Batal gambar -> Hapus data sementara
    void CancelDraw() {
        if (activeObj?.rootObj) Destroy(activeObj.rootObj);
        ResetState();
    }

    void ResetState() {
        activeObj = null; isDrawing = forceTextureOnNext = isToolActive = false;
        currentMode = DrawMode.Point; // Reset ke default
        ghostObj?.SetActive(false); tooltipObj?.SetActive(false);
    }

    // --- LOGIKA VISUALISASI ---

    // Refresh posisi semua objek (dipanggil saat map geser)
    void RefreshAllVisuals() {
        foreach (var obj in allObjs) RebuildVisual(obj);
        if (activeObj != null) RebuildVisual(activeObj, true);
    }

    // Bangun ulang elemen UI (Garis, Titik, Fill) dari data koordinat
    void RebuildVisual(DrawObject obj, bool isEditing = false) {
        ClearVisuals(obj); // Hapus elemen lama
        if (obj.coordinates.Count == 0) return;
        
        Transform parent = obj.rootObj ? obj.rootObj.transform : container;
        Color cLine = obj.type == DrawMode.Line ? colLine : colLine;

        // Render Titik Tunggal
        if (obj.type == DrawMode.Point) { 
            SpawnIcon(pointMarkerPrefab, obj.coordinates[0], parent, colPoint, obj); 
            return; 
        }

        // Render Vertex (Titik Sudut) saat mode edit
        if (isEditing) foreach (var p in obj.coordinates) SpawnIcon(vertexPointPrefab, p, parent, colVertex, obj);
        
        // Render Garis-garis segmen
        for (int i = 1; i < obj.coordinates.Count; i++) 
            SpawnLine(obj.coordinates[i-1], obj.coordinates[i], parent, cLine, obj);

        // Render Polygon (Tutup Loop & Isi)
        if (obj.type == DrawMode.Polygon && obj.coordinates.Count >= 3) {
            if (!isEditing) SpawnLine(obj.coordinates[^1], obj.coordinates[0], parent, cLine, obj); // Garis penutup
            CreateFill(obj); // Luas area (Mesh)
        }
    }

    // Spawn Icon/Marker di posisi geo
    void SpawnIcon(GameObject prefab, Vector2 ll, Transform p, Color c, DrawObject container) {
        var go = Instantiate(prefab, p);
        go.GetComponent<RectTransform>().anchoredPosition = MapLoc(ll);
        if (go.TryGetComponent<Image>(out var img)) img.color = c;
        container.visuals.Add(go);
    }

    // Spawn Garis Antara 2 Titik Geo
    void SpawnLine(Vector2 a, Vector2 b, Transform p, Color c, DrawObject container) {
        var go = Instantiate(lineSegmentPrefab, p);
        var rt = go.GetComponent<RectTransform>();
        SetRectGeo(rt, a, b);
        if (go.TryGetComponent<Image>(out var img)) img.color = c;
        container.visuals.Add(go);
    }

    // Atur posisi & rotasi RectTransform agar menghubungkan titik A dan B
    void SetRectGeo(RectTransform rt, Vector2 a, Vector2 b) {
        var p1 = MapLoc(a); var p2 = MapLoc(b);
        var dir = p2 - p1;
        rt.anchoredPosition = p1 + dir * 0.5f; // Titik tengah
        rt.sizeDelta = new Vector2(dir.magnitude, rt.sizeDelta.y); // Panjang
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg); // Sudut
    }

    // Generate Mesh Polygon Fill
    void CreateFill(DrawObject obj) {
        var go = new GameObject("Fill", typeof(RectTransform), typeof(UIPolygonRenderer));
        go.transform.SetParent(obj.rootObj.transform, false); go.transform.SetAsFirstSibling();
        
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = container.sizeDelta; rt.anchoredPosition = Vector2.zero;

        // Konversi LatLon -> Screen Local Position
        var pts = new List<Vector2>();
        foreach (var c in obj.coordinates) pts.Add(MapLoc(c));

        var r = go.GetComponent<UIPolygonRenderer>();
        if (obj.useTexture && cachedTex) { r.SetPolygon(pts, Color.white); r.texture = cachedTex; }
        else r.SetPolygon(pts, colFill);
        
        obj.visuals.Add(go);
    }

    // --- HELPER & UTILITY ---

    void ClearVisuals(DrawObject obj) {
        foreach (var v in obj.visuals) if (v) Destroy(v);
        obj.visuals.Clear();
    }

    GameObject CreateRoot(string name) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(container, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        return go;
    }

    // Konversi LatLon -> Local UI Pos
    Vector2 MapLoc(Vector2 ll) => mapController.LatLonToLocalPosition(ll.x, ll.y);
    Vector2 GeoToScreen(Vector2 ll) => RectTransformUtility.WorldToScreenPoint(null, container.TransformPoint(MapLoc(ll)));

    // --- LOGIKA HAPUS (DELETE) ---

    void TryDelete(Vector2 mPos) {
        for (int i = allObjs.Count - 1; i >= 0; i--) {
            if (HitTest(allObjs[i], mPos)) {
                if (allObjs[i].rootObj) Destroy(allObjs[i].rootObj);
                allObjs.RemoveAt(i);
                return; // Hapus satu per satu
            }
        }
    }

    // Cek apakah mouse mengenai objek (Point/Line/Poly)
    bool HitTest(DrawObject obj, Vector2 mPos) {
        var pts = new List<Vector2>();
        foreach(var c in obj.coordinates) pts.Add(GeoToScreen(c));

        // Cek Titik
        if (obj.type == DrawMode.Point) return Vector2.Distance(mPos, pts[0]) < snapDist;

        // Cek Garis (Jarak ke segmen garis)
        for (int i = 0; i < pts.Count - 1; i++) if (DistLine(mPos, pts[i], pts[i+1]) < snapDist) return true;
        
        // Cek Polygon (Garis tutup & Area dalam)
        if (obj.type == DrawMode.Polygon && pts.Count >= 3) {
            if (DistLine(mPos, pts[^1], pts[0]) < snapDist) return true;
            if (IsInPoly(mPos, pts)) return true;
        }
        return false;
    }

    // Matematika: Jarak titik ke segmen garis
    float DistLine(Vector2 p, Vector2 a, Vector2 b) {
        Vector2 n = b - a; float len2 = n.sqrMagnitude;
        float t = len2 == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, n) / len2);
        return Vector2.Distance(p, a + n * t);
    }

    // Matematika: Point in Polygon (Raycasting)
    bool IsInPoly(Vector2 p, List<Vector2> poly) {
        bool inPoly = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) 
                inPoly = !inPoly;
        return inPoly;
    }

    // --- UI HELPER GHOST & TOOLTIP ---

    void CreateHelpers() {
        if (!ghostObj && lineSegmentPrefab) {
            ghostObj = Instantiate(lineSegmentPrefab, container);
            ghostObj.GetComponent<Image>().color = new Color(1,1,1,0.5f);
            ghostObj.SetActive(false);
        }
        if (!tooltipObj && tooltipPrefab) {
            tooltipObj = Instantiate(tooltipPrefab, container);
            tooltipText = tooltipObj.GetComponentInChildren<TMP_Text>();
            tooltipObj.SetActive(false);
        }
    }

    void UpdateGhost(Vector2 mPos) {
        if (!ghostObj) return;
        ghostObj.SetActive(true); ghostObj.transform.SetAsLastSibling();
        SetRectGeo(ghostObj.GetComponent<RectTransform>(), activeObj.coordinates[^1], mapController.ScreenToLatLon(mPos));
    }

    void UpdateTooltip(Vector2 mPos) {
        if (!tooltipObj || currentMode == DrawMode.Point) { tooltipObj?.SetActive(false); return; }
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mPos, null, out var lp);
        tooltipObj.GetComponent<RectTransform>().anchoredPosition = lp + new Vector2(25, -15);
        tooltipObj.SetActive(true); tooltipObj.transform.SetAsLastSibling();

        // Teks info
        int n = activeObj?.coordinates.Count ?? 0;
        string txt = currentMode == DrawMode.Line ? (n > 0 ? $"Jarak: {CalcDist(activeObj.coordinates[^1], mapController.ScreenToLatLon(mPos)):F2} km" : "Klik mulai") : (n > 2 ? "Klik awal utk tutup" : "Klik tambah titik");
        if (tooltipText) tooltipText.text = txt;
    }

    float CalcDist(Vector2 p1, Vector2 p2) => 12742 * Mathf.Asin(Mathf.Sqrt(0.5f - Mathf.Cos((p2.x - p1.x) * Mathf.Deg2Rad)/2 + Mathf.Cos(p1.x * Mathf.Deg2Rad) * Mathf.Cos(p2.x * Mathf.Deg2Rad) * (1 - Mathf.Cos((p2.y - p1.y) * Mathf.Deg2Rad))/2));

    // --- PUBLIC API ---

    public void ActivateMode(DrawMode m) { if (isDrawing && currentMode != m) CancelDraw(); isToolActive = true; currentMode = m; }
    public void DeactivateMode(DrawMode m) { if (currentMode == m) { CancelDraw(); isToolActive = false; } }
    public bool IsModeActive(DrawMode m) => isToolActive && currentMode == m;
    public void LoadPolygon(List<Vector2> c, bool tex) { CreateObject(DrawMode.Polygon, c, tex); }
    public void CreateObject(DrawMode type, List<Vector2> coords, bool tex = false) {
        var obj = new DrawObject{ type = type, useTexture = tex, coordinates = new(coords), rootObj = CreateRoot("Loaded") };
        RebuildVisual(obj); allObjs.Add(obj);
    }
    public void Undo() { 
        if (isDrawing && activeObj?.coordinates.Count > 0) { 
            activeObj.coordinates.RemoveAt(activeObj.coordinates.Count - 1); 
            if (activeObj.coordinates.Count == 0) CancelDraw(); else RebuildVisual(activeObj, true); 
        } else if (allObjs.Count > 0) { 
            Destroy(allObjs[^1].rootObj); allObjs.RemoveAt(allObjs.Count - 1); 
        } 
    }
    public void ClearAll() { foreach(var o in allObjs) Destroy(o.rootObj); allObjs.Clear(); ResetState(); }
}