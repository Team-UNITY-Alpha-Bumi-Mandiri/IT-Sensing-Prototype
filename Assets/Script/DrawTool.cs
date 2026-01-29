using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Events;
using System.IO;

// ============================================================
// DrawTool - Tool untuk menggambar di peta
// ============================================================
// Mode:
// - Point   : Gambar titik tunggal
// - Line    : Gambar garis (minimal 2 titik, klik titik terakhir untuk selesai)
// - Polygon : Gambar polygon (minimal 3 titik, klik titik pertama untuk menutup)
// - Delete  : Hapus objek dengan klik
// 
// Kontrol:
// - Klik kiri  : Tambah titik / Delete objek
// - Klik kanan : Undo titik terakhir
// - ESC        : Batal gambar
// ============================================================
public class DrawTool : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;  // Controller peta untuk konversi koordinat
    public RectTransform container;                 // Container untuk visual drawing

    [Header("Prefabs")]
    public GameObject pointMarkerPrefab;   // Marker untuk mode Point
    public GameObject lineSegmentPrefab;   // Segment garis
    public GameObject vertexPointPrefab;   // Titik sudut polygon
    public GameObject tooltipPrefab;       // Tooltip saat drawing

    [Header("Settings")]
    public float snapDist = 30f;                // Jarak snap untuk finish drawing (pixel)
    public bool forceTextureOnNext = false;     // Paksa pakai texture untuk drawing berikutnya
    public string texturePath = "Assets/Image Map/mapa del mundo pixel art.jpg";
    public string currentDrawingLayer = "";     // Layer untuk drawing yang sedang aktif

    public enum DrawMode { Point, Line, Polygon, Delete, Edit, Cut }

    // Events
    public UnityEvent<DrawObject> onDrawComplete;  // Dipanggil saat drawing selesai
    public UnityEvent<DrawObject> onObjectDeleted; // Dipanggil saat objek dihapus

    // Warna visual
    readonly Color colPoint = Color.blue;
    readonly Color colVertex = Color.white;
    readonly Color colLine = new Color(0, 0.42f, 1);      // Biru
    readonly Color colFill = new Color(0, 0.42f, 1, 0.3f); // Biru transparan
    
    // State internal
    Texture2D cachedTex;
    bool isDrawing, isActive;
    DrawMode mode = DrawMode.Point;
    int drawId;
    double lastLat, lastLon;
    int lastZoom;

    List<DrawObject> allObjs = new List<DrawObject>();  // Semua objek yang sudah digambar
    DrawObject activeObj;                               // Objek yang sedang digambar
    GameObject ghost, tooltip;                          // Helper visual
    TMP_Text tooltipText;

    // Edit Mode State
    List<DrawObject> editObjs = new List<DrawObject>();
    int dragVertexIndex = -1;
    DrawObject dragObj = null;

    // Cut Mode State
    List<Vector2> cutPoints = new List<Vector2>();
    List<GameObject> cutVisuals = new List<GameObject>();
    float lastClickTime = 0f;

    // Struktur data objek gambar
    [System.Serializable]
    public class DrawObject
    {
        public string id = System.Guid.NewGuid().ToString();  // ID unik
        public DrawMode type;                                  // Tipe objek
        public bool useTexture;                                // Apakah pakai texture fill
        public string layerName;                               // Nama layer
        public List<Vector2> coordinates = new List<Vector2>(); // Koordinat (lat, lon)
        public List<GameObject> visuals = new List<GameObject>(); // Visual GameObjects
        public GameObject rootObj;                              // Parent object
    }

    // ============================================================
    // LIFECYCLE
    // ============================================================

    void Start()
    {
        if (container == null) return;
        CreateHelpers();
        LoadTexture();
    }

    void Update()
    {
        if (mapController == null || container == null) return;

        // Refresh visual jika peta bergerak/zoom
        if (mapController.latitude != lastLat || mapController.longitude != lastLon || mapController.zoom != lastZoom)
        {
            RefreshAll();
            lastLat = mapController.latitude;
            lastLon = mapController.longitude;
            lastZoom = mapController.zoom;
        }
        HandleInput();
    }

    // ============================================================
    // INPUT HANDLING
    // ============================================================

    void HandleInput()
    {
        if (!isActive || Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Update ghost line dan tooltip saat drawing
        if (isDrawing && activeObj?.coordinates.Count > 0)
            UpdateGhost(mousePos);
        else if (ghost != null)
            ghost.SetActive(false);
        
        UpdateTooltip(mousePos);

        // Klik kiri: tambah titik, delete, cut, atau start drag vertex
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Cek double click untuk Cut mode
            if (mode == DrawMode.Cut && cutPoints.Count >= 2 && Time.time - lastClickTime < 0.3f)
            {
                FinishCut();
                lastClickTime = 0f;
            }
            else if (mode == DrawMode.Delete) TryDelete(mousePos);
            else if (mode == DrawMode.Edit) TryStartVertexDrag(mousePos);
            else if (mode == DrawMode.Cut) AddCutPoint(mapController.ScreenToLatLon(mousePos), mousePos);
            else AddPoint(mapController.ScreenToLatLon(mousePos), mousePos);
            
            lastClickTime = Time.time;
        }
        
        // Update drag vertex saat Edit Mode
        if (mode == DrawMode.Edit && dragObj != null)
        {
            UpdateVertexDrag(mousePos);
        }
        
        // Release drag
        if (Mouse.current.leftButton.wasReleasedThisFrame && dragObj != null)
        {
            EndVertexDrag();
        }

        // Klik kanan: undo atau cancel cut
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (mode == DrawMode.Cut && cutPoints.Count > 0) ClearCutLine();
            else Undo();
        }

        // ESC: batal
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            if (mode == DrawMode.Cut) ClearCutLine();
            else if (isDrawing) Cancel();
        }
    }

    // ============================================================
    // DRAWING LOGIC
    // ============================================================

    // Tambah titik ke drawing aktif
    void AddPoint(Vector2 latLon, Vector2 screenPos)
    {
        // Mode Point: langsung selesai
        if (mode == DrawMode.Point)
        {
            CreateObj(DrawMode.Point, new List<Vector2> { latLon });
            // Auto-save disabled - gunakan tombol Save
            return;
        }

        // Mulai drawing baru jika belum ada
        if (!isDrawing) StartDraw();

        // Cek snap untuk finish
        int minPts = (mode == DrawMode.Polygon) ? 3 : 2;
        if (activeObj.coordinates.Count >= minPts)
        {
            int targetIdx = (mode == DrawMode.Line) ? activeObj.coordinates.Count - 1 : 0;
            if (Vector2.Distance(screenPos, GeoToScreen(activeObj.coordinates[targetIdx])) < snapDist)
            {
                FinishDraw();
                return;
            }
        }

        // Tambah titik
        activeObj.coordinates.Add(latLon);
        Rebuild(activeObj, true);
    }

    // Mulai drawing baru
    void StartDraw()
    {
        isDrawing = true;
        drawId++;
        activeObj = new DrawObject
        {
            type = mode,
            useTexture = forceTextureOnNext,
            layerName = currentDrawingLayer,
            rootObj = CreateRoot($"Draw_{drawId}_{mode}")
        };
    }

    // Selesaikan drawing
    void FinishDraw()
    {
        if (activeObj == null) return;
        Rebuild(activeObj, false);
        allObjs.Add(activeObj);
        // Auto-save disabled - gunakan tombol Save
        Reset();
    }

    // Batalkan drawing
    void Cancel()
    {
        if (activeObj?.rootObj != null) Destroy(activeObj.rootObj);
        Reset();
    }

    // Reset state
    void Reset()
    {
        activeObj = null;
        isDrawing = false;
        forceTextureOnNext = false;
        isActive = false;
        mode = DrawMode.Point;
        if (ghost != null) ghost.SetActive(false);
        if (tooltip != null) tooltip.SetActive(false);
    }

    // ============================================================
    // VISUAL RENDERING
    // ============================================================

    // Refresh semua visual (dipanggil saat peta bergerak)
    void RefreshAll()
    {
        foreach (var obj in allObjs) Rebuild(obj);
        if (activeObj != null) Rebuild(activeObj, true);
    }

    // Rebuild visual untuk satu objek
    void Rebuild(DrawObject obj, bool editing = false)
    {
        ClearVisuals(obj);
        if (obj.coordinates.Count == 0) return;

        Transform parent = obj.rootObj?.transform ?? container;

        // Mode Point: satu marker
        if (obj.type == DrawMode.Point)
        {
            SpawnIcon(pointMarkerPrefab, obj.coordinates[0], parent, colPoint, obj);
            return;
        }

        // Vertex points saat editing
        if (editing)
            foreach (var p in obj.coordinates)
                SpawnIcon(vertexPointPrefab, p, parent, colVertex, obj);

        // Garis penghubung
        for (int i = 1; i < obj.coordinates.Count; i++)
            SpawnLine(obj.coordinates[i - 1], obj.coordinates[i], parent, colLine, obj);

        // Polygon: tutup loop dan fill
        if (obj.type == DrawMode.Polygon && obj.coordinates.Count >= 3)
        {
            if (!editing)
                SpawnLine(obj.coordinates[^1], obj.coordinates[0], parent, colLine, obj);
            CreateFill(obj);
        }
    }

    // Spawn icon marker
    void SpawnIcon(GameObject prefab, Vector2 latLon, Transform parent, Color c, DrawObject obj)
    {
        var go = Instantiate(prefab, parent);
        go.GetComponent<RectTransform>().anchoredPosition = MapLoc(latLon);
        var img = go.GetComponent<Image>();
        if (img != null) img.color = c;
        obj.visuals.Add(go);
    }

    // Spawn line segment
    void SpawnLine(Vector2 a, Vector2 b, Transform parent, Color c, DrawObject obj)
    {
        var go = Instantiate(lineSegmentPrefab, parent);
        SetLineRect(go.GetComponent<RectTransform>(), a, b);
        var img = go.GetComponent<Image>();
        if (img != null) img.color = c;
        obj.visuals.Add(go);
    }

    // Set posisi dan rotasi line segment
    void SetLineRect(RectTransform rt, Vector2 a, Vector2 b)
    {
        Vector2 p1 = MapLoc(a), p2 = MapLoc(b);
        Vector2 dir = p2 - p1;
        rt.anchoredPosition = p1 + dir * 0.5f;
        rt.sizeDelta = new Vector2(dir.magnitude, rt.sizeDelta.y);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // Buat polygon fill
    void CreateFill(DrawObject obj)
    {
        var go = new GameObject("Fill", typeof(RectTransform), typeof(UIPolygonRenderer));
        go.transform.SetParent(obj.rootObj.transform, false);
        go.transform.SetAsFirstSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = container.sizeDelta;
        rt.anchoredPosition = Vector2.zero;

        // Konversi koordinat
        var pts = new List<Vector2>();
        foreach (var c in obj.coordinates) pts.Add(MapLoc(c));

        var renderer = go.GetComponent<UIPolygonRenderer>();
        if (obj.useTexture && cachedTex != null)
        {
            renderer.SetPolygon(pts, Color.white);
            renderer.texture = cachedTex;
        }
        else renderer.SetPolygon(pts, colFill);

        obj.visuals.Add(go);
    }

    // Hapus semua visual objek
    void ClearVisuals(DrawObject obj)
    {
        foreach (var v in obj.visuals) if (v != null) Destroy(v);
        obj.visuals.Clear();
    }

    // ============================================================
    // HELPERS
    // ============================================================

    // Buat root GameObject untuk objek gambar
    GameObject CreateRoot(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(container, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        return go;
    }

    // Konversi lat/lon ke local position
    Vector2 MapLoc(Vector2 latLon) => mapController.LatLonToLocalPosition(latLon.x, latLon.y);
    
    // Konversi lat/lon ke screen position
    Vector2 GeoToScreen(Vector2 latLon) => RectTransformUtility.WorldToScreenPoint(null, container.TransformPoint(MapLoc(latLon)));

    // Load texture untuk polygon fill
    void LoadTexture()
    {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, texturePath);
        if (!File.Exists(path)) path = texturePath;
        if (!File.Exists(path)) return;
        
        cachedTex = new Texture2D(2, 2);
        cachedTex.LoadImage(File.ReadAllBytes(path));
    }

    // Buat helper objects (ghost line, tooltip)
    void CreateHelpers()
    {
        if (ghost == null && lineSegmentPrefab != null)
        {
            ghost = Instantiate(lineSegmentPrefab, container);
            ghost.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
            ghost.SetActive(false);
        }
        if (tooltip == null && tooltipPrefab != null)
        {
            tooltip = Instantiate(tooltipPrefab, container);
            tooltipText = tooltip.GetComponentInChildren<TMP_Text>();
            tooltip.SetActive(false);
        }
    }

    // Update ghost line
    void UpdateGhost(Vector2 mousePos)
    {
        if (ghost == null) return;
        ghost.SetActive(true);
        ghost.transform.SetAsLastSibling();
        SetLineRect(ghost.GetComponent<RectTransform>(), activeObj.coordinates[^1], mapController.ScreenToLatLon(mousePos));
    }

    // Update tooltip
    void UpdateTooltip(Vector2 mousePos)
    {
        if (tooltip == null || (mode != DrawMode.Line && mode != DrawMode.Polygon))
        {
            if (tooltip != null) tooltip.SetActive(false);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mousePos, null, out Vector2 localPos);
        tooltip.GetComponent<RectTransform>().anchoredPosition = localPos + new Vector2(25, -15);
        tooltip.SetActive(true);
        tooltip.transform.SetAsLastSibling();

        int n = activeObj?.coordinates.Count ?? 0;
        string txt = mode == DrawMode.Line
            ? n == 0 ? "click to start drawing line"
                : n >= 2 ? $"{CalcDist(activeObj.coordinates[^1], mapController.ScreenToLatLon(mousePos)):F2} km\nclick last point to finish"
                : $"{CalcDist(activeObj.coordinates[^1], mapController.ScreenToLatLon(mousePos)):F2} km\nclick to continue"
            : n == 0 ? "click to start drawing shape"
                : n > 2 ? "click first point to close shape"
                : "click to continue drawing shape";

        if (tooltipText != null) tooltipText.text = txt;
    }

    // Hitung jarak haversine (km)
    float CalcDist(Vector2 p1, Vector2 p2)
    {
        float dLat = (p2.x - p1.x) * Mathf.Deg2Rad;
        float dLon = (p2.y - p1.y) * Mathf.Deg2Rad;
        float a = 0.5f - Mathf.Cos(dLat) / 2 + Mathf.Cos(p1.x * Mathf.Deg2Rad) * Mathf.Cos(p2.x * Mathf.Deg2Rad) * (1 - Mathf.Cos(dLon)) / 2;
        return 12742 * Mathf.Asin(Mathf.Sqrt(a));
    }

    // ============================================================
    // DELETE MODE
    // ============================================================

    // Coba hapus objek di posisi klik
    void TryDelete(Vector2 mousePos)
    {
        for (int i = allObjs.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(currentDrawingLayer) && allObjs[i].layerName != currentDrawingLayer)
                continue;
            if (!HitTest(allObjs[i], mousePos)) continue;
            onObjectDeleted?.Invoke(allObjs[i]);
            if (allObjs[i].rootObj != null) Destroy(allObjs[i].rootObj);
            allObjs.RemoveAt(i);
            return;
        }
    }

    // Hit test untuk objek
    bool HitTest(DrawObject obj, Vector2 mousePos)
    {
        var pts = new List<Vector2>();
        foreach (var c in obj.coordinates) pts.Add(GeoToScreen(c));

        if (obj.type == DrawMode.Point) return Vector2.Distance(mousePos, pts[0]) < snapDist;

        // Cek proximity ke garis
        for (int i = 0; i < pts.Count - 1; i++)
            if (DistToLine(mousePos, pts[i], pts[i + 1]) < snapDist) return true;

        // Polygon: cek closing line dan point-in-poly
        if (obj.type == DrawMode.Polygon && pts.Count >= 3)
        {
            if (DistToLine(mousePos, pts[^1], pts[0]) < snapDist) return true;
            if (InPoly(mousePos, pts)) return true;
        }
        return false;
    }

    // Jarak titik ke garis
    float DistToLine(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 n = b - a;
        float len2 = n.sqrMagnitude;
        float t = len2 != 0 ? Mathf.Clamp01(Vector2.Dot(p - a, n) / len2) : 0;
        return Vector2.Distance(p, a + n * t);
    }

    // Point in polygon test
    bool InPoly(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    // Aktifkan mode gambar
    public void ActivateMode(DrawMode m)
    {
        if (isDrawing && mode != m) Cancel();
        isActive = true;
        mode = m;
    }

    // Nonaktifkan mode gambar
    public void DeactivateMode(DrawMode m)
    {
        if (mode != m) return;
        Cancel();
        isActive = false;
    }

    // Cek apakah mode aktif
    public bool IsModeActive(DrawMode m) => isActive && mode == m;

    // Load polygon dari koordinat
    public void LoadPolygon(List<Vector2> coords, bool useTexture, string layerName = "Loaded", string id = null)
        => CreateObj(DrawMode.Polygon, coords, layerName, useTexture, id);

    // Buat objek gambar (simple)
    public void CreateObj(DrawMode type, List<Vector2> coords, bool tex = false)
    {
        var obj = new DrawObject { type = type, useTexture = tex, coordinates = new List<Vector2>(coords), rootObj = CreateRoot("Loaded") };
        Rebuild(obj);
        allObjs.Add(obj);
    }

    // Buat objek gambar (dengan layer dan ID)
    public void CreateObj(DrawMode type, List<Vector2> coords, string layer, bool tex = false, string id = null)
    {
        var obj = new DrawObject
        {
            id = string.IsNullOrEmpty(id) ? System.Guid.NewGuid().ToString() : id,
            type = type, layerName = layer, useTexture = tex,
            coordinates = new List<Vector2>(coords), rootObj = CreateRoot("Loaded_" + layer)
        };
        Rebuild(obj);
        allObjs.Add(obj);
    }

    // Set visibility layer
    public void SetLayerVisibility(string layer, bool visible)
    {
        foreach (var obj in allObjs)
            if (obj.layerName == layer && obj.rootObj != null)
                obj.rootObj.SetActive(visible);
    }

    // Undo titik terakhir atau objek terakhir
    public void Undo()
    {
        if (isDrawing && activeObj?.coordinates.Count > 0)
        {
            activeObj.coordinates.RemoveAt(activeObj.coordinates.Count - 1);
            if (activeObj.coordinates.Count == 0) Cancel();
            else Rebuild(activeObj, true);
        }
        else if (allObjs.Count > 0)
        {
            if (allObjs[^1].rootObj != null) Destroy(allObjs[^1].rootObj);
            allObjs.RemoveAt(allObjs.Count - 1);
        }
    }

    // Hapus semua objek
    public void ClearAll()
    {
        foreach (var obj in allObjs) if (obj.rootObj != null) Destroy(obj.rootObj);
        allObjs.Clear();
        Reset();
    }

    // Set visibility semua objek
    public void SetAllVisibility(bool visible)
    {
        foreach (var obj in allObjs)
            if (obj.rootObj != null) obj.rootObj.SetActive(visible);
    }

    // Cek apakah drawing dengan ID tertentu ada
    public bool HasDrawing(string id) => allObjs.Exists(x => x.id == id);

    // Ambil semua drawings di layer tertentu
    public List<DrawObject> GetLayerDrawings(string layerName)
    {
        return allObjs.FindAll(x => x.layerName == layerName);
    }

    // Sembunyikan semua visual (dipanggil saat ganti project)
    public void ForceHideAllVisuals()
    {
        foreach (var obj in allObjs)
            if (obj.rootObj != null) obj.rootObj.SetActive(false);
        if (ghost != null) ghost.SetActive(false);
        if (tooltip != null) tooltip.SetActive(false);
    }

    // Tampilkan/sembunyikan drawing dengan ID tertentu
    public void ShowDrawing(string id, bool visible)
    {
        var obj = allObjs.Find(x => x.id == id);
        if (obj?.rootObj != null) obj.rootObj.SetActive(visible);
    }

    // Rename layer
    public void RenameLayer(string oldName, string newName)
    {
        foreach (var obj in allObjs)
            if (obj.layerName == oldName) obj.layerName = newName;
        Debug.Log($"[DrawTool] Renamed layer {oldName} to {newName}");
    }

    // Hapus semua objek di layer tertentu
    public void DeleteLayer(string layerName)
    {
        var toRemove = allObjs.FindAll(x => x.layerName == layerName);
        foreach (var obj in toRemove)
        {
            if (obj.rootObj != null) Destroy(obj.rootObj);
            allObjs.Remove(obj);
        }
        Debug.Log($"[DrawTool] Deleted layer {layerName} ({toRemove.Count} objects)");
    }

    public void EditLayer(string layerName)
    {
        editObjs = allObjs.FindAll(x => x.layerName == layerName);
        foreach (var obj in editObjs)
            Rebuild(obj, true);  // Tampilkan vertex
    }

    public void CancelEditLayer()
    {
        dragObj = null;
        dragVertexIndex = -1;
        foreach (var obj in editObjs)
            Rebuild(obj, false);  // Sembunyikan vertex
        editObjs.Clear();
    }

    // Mulai drag vertex terdekat
    void TryStartVertexDrag(Vector2 mousePos)
    {
        foreach (var obj in editObjs)
        {
            for (int i = 0; i < obj.coordinates.Count; i++)
            {
                Vector2 vertexScreen = GeoToScreen(obj.coordinates[i]);
                if (Vector2.Distance(mousePos, vertexScreen) < snapDist)
                {
                    dragObj = obj;
                    dragVertexIndex = i;
                    Debug.Log($"[DrawTool] Started dragging vertex {i}");
                    return;
                }
            }
        }
    }

    // Update posisi vertex saat drag
    void UpdateVertexDrag(Vector2 mousePos)
    {
        if (dragObj == null || dragVertexIndex < 0) return;
        
        Vector2 latLon = mapController.ScreenToLatLon(mousePos);
        dragObj.coordinates[dragVertexIndex] = latLon;
        
        // Rebuild visual
        Rebuild(dragObj, true);
    }

    // Selesai drag vertex
    void EndVertexDrag()
    {
        dragObj = null;
        dragVertexIndex = -1;
    }

    public void DeactivateAllModes()
    {
        // Cancel edit jika sedang edit
        if (IsModeActive(DrawMode.Edit))
        {
            CancelEditLayer();
        }
        
        // Cancel drawing jika sedang drawing
        if (isDrawing)
        {
            Cancel();
        }
        
        // Clear cut line jika ada
        ClearCutLine();
        
        // Deactivate semua mode satu per satu
        DeactivateMode(DrawMode.Polygon);
        DeactivateMode(DrawMode.Point);
        DeactivateMode(DrawMode.Line);
        DeactivateMode(DrawMode.Delete);
        DeactivateMode(DrawMode.Edit);
        DeactivateMode(DrawMode.Cut);
        
        // Reset state
        isActive = false;
        currentDrawingLayer = "";
        dragObj = null;
        dragVertexIndex = -1;
    }

    // ============================================================
    // CUT MODE
    // ============================================================

    // Tambah titik ke cut line
    void AddCutPoint(Vector2 latLon, Vector2 screenPos)
    {
        cutPoints.Add(latLon);
        
        // Spawn visual marker (warna merah)
        var marker = Instantiate(vertexPointPrefab, container);
        marker.GetComponent<RectTransform>().anchoredPosition = MapLoc(latLon);
        var markerImg = marker.GetComponent<Image>();
        if (markerImg != null) markerImg.color = Color.red;
        cutVisuals.Add(marker);
        
        // Spawn line ke titik sebelumnya
        if (cutPoints.Count > 1)
        {
            var line = Instantiate(lineSegmentPrefab, container);
            SetLineRect(line.GetComponent<RectTransform>(), cutPoints[cutPoints.Count - 2], latLon);
            var lineImg = line.GetComponent<Image>();
            if (lineImg != null) lineImg.color = Color.red;
            cutVisuals.Add(line);
        }
    }

    // Selesaikan cut dan insert vertex di titik intersect
    void FinishCut()
    {
        List<DrawObject> toRemove = new List<DrawObject>();
        List<DrawObject> toAdd = new List<DrawObject>();
        foreach (var obj in allObjs)
        {
            if (!string.IsNullOrEmpty(currentDrawingLayer) && obj.layerName != currentDrawingLayer) continue;
            
            List<(int segmentIndex, Vector2 point)> intersections = new List<(int, Vector2)>();

            int segmentCount = obj.coordinates.Count;
            if (obj.type == DrawMode.Line) segmentCount--;

            for (int i = 0; i < segmentCount; i++)
            {
                int next = (i + 1) % obj.coordinates.Count;
                Vector2 p1 = obj.coordinates[i];
                Vector2 p2 = obj.coordinates[next];
                
                for (int j = 0; j < cutPoints.Count - 1; j++)
                {
                    if (LineIntersect(p1, p2, cutPoints[j], cutPoints[j + 1], out Vector2 intersection))
                    {
                        intersections.Add((i, intersection));
                    }
                }
            }

            if (intersections.Count < 2)
            {
                continue;
            }

            var int1 = intersections[0];
            var int2 = intersections[1];

            if (obj.type == DrawMode.Polygon)
            {
                SplitPolygon(obj, int1, int2, toRemove, toAdd);
            }
            else if (obj.type == DrawMode.Line)
            {
                SplitLine(obj, int1, int2, toRemove, toAdd);
            }
        }

        foreach (var obj in toRemove)
        {
            if (obj.rootObj != null) Destroy(obj.rootObj);
            allObjs.Remove(obj);
        }

        foreach (var obj in toAdd)
        {
            allObjs.Add(obj);
            Rebuild(obj, false);
        }

        ClearCutLine();
        DeactivateMode(DrawMode.Cut);
    }

    // Cek apakah dua garis berpotongan
    bool LineIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
    {
        intersection = Vector2.zero;
        
        float d = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
        if (Mathf.Abs(d) < 0.0001f) return false;  // Parallel
        
        float t = ((p1.x - p3.x) * (p3.y - p4.y) - (p1.y - p3.y) * (p3.x - p4.x)) / d;
        float u = -((p1.x - p2.x) * (p1.y - p3.y) - (p1.y - p2.y) * (p1.x - p3.x)) / d;
        
        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            intersection = new Vector2(p1.x + t * (p2.x - p1.x), p1.y + t * (p2.y - p1.y));
            return true;
        }
        return false;
    }

    void SplitPolygon(DrawObject obj, (int segmentIndex, Vector2 point) int1, (int segmentIndex, Vector2 point) int2, List<DrawObject> toRemove, List<DrawObject> toAdd)
    {
        if (int1.segmentIndex > int2.segmentIndex)
        {
            var temp = int1;
            int1 = int2;
            int2 = temp;
        }

        List<Vector2> newCoords1 = new List<Vector2>();
        newCoords1.Add(int1.point);
        for (int i = int1.segmentIndex + 1; i <= int2.segmentIndex; i++) newCoords1.Add(obj.coordinates[i]);
        newCoords1.Add(int2.point);

        List<Vector2> newCoords2 = new List<Vector2>();
        newCoords2.Add(int2.point);
        for (int i = int2.segmentIndex + 1; i < obj.coordinates.Count; i++) newCoords2.Add(obj.coordinates[i]);
        for (int i = 0; i <= int1.segmentIndex; i++) newCoords2.Add(obj.coordinates[i]);
        newCoords2.Add(int1.point);

        if (newCoords1.Count >= 3)
        {
            var newObj1 = new DrawObject
            {
                id = System.Guid.NewGuid().ToString(),
                type = DrawMode.Polygon,
                layerName = obj.layerName,
                coordinates = newCoords1,
                rootObj = CreateRoot(obj.layerName + "_split1")
            };
            toAdd.Add(newObj1);
        }

        if (newCoords2.Count >= 3)
        {
            var newObj2 = new DrawObject
            {
                id = System.Guid.NewGuid().ToString(),
                type = DrawMode.Polygon,
                layerName = obj.layerName,
                coordinates = newCoords2,
                rootObj = CreateRoot(obj.layerName + "_split2")
            };
            toAdd.Add(newObj2);
        }

        toRemove.Add(obj);
    }

    void SplitLine(DrawObject obj, (int segmentIndex, Vector2 point) int1, (int segmentIndex, Vector2 point) int2, List<DrawObject> toRemove, List<DrawObject> toAdd)
    {
        if (int1.segmentIndex > int2.segmentIndex)
        {
            var temp = int1;
            int1 = int2;
            int2 = temp;
        }
        
        List<Vector2> newCoords1 = new List<Vector2>();
        for (int i = 0; i <= int1.segmentIndex; i++)
            newCoords1.Add(obj.coordinates[i]);
        newCoords1.Add(int1.point);
        
        List<Vector2> newCoords2 = new List<Vector2>();
        newCoords2.Add(int1.point);
        for (int i = int1.segmentIndex + 1; i <= int2.segmentIndex; i++)
            newCoords2.Add(obj.coordinates[i]);
        newCoords2.Add(int2.point);
        
        List<Vector2> newCoords3 = new List<Vector2>();
        newCoords3.Add(int2.point);
        for (int i = int2.segmentIndex + 1; i < obj.coordinates.Count; i++)
            newCoords3.Add(obj.coordinates[i]);
        
        if (newCoords1.Count >= 2)
        {
            var newObj = new DrawObject
            {
                id = System.Guid.NewGuid().ToString(),
                type = DrawMode.Line,
                layerName = obj.layerName,
                coordinates = newCoords1,
                rootObj = CreateRoot(obj.layerName + "_split1")
            };
            toAdd.Add(newObj);
        }
        
        if (newCoords2.Count >= 2)
        {
            var newObj = new DrawObject
            {
                id = System.Guid.NewGuid().ToString(),
                type = DrawMode.Line,
                layerName = obj.layerName,
                coordinates = newCoords2,
                rootObj = CreateRoot(obj.layerName + "_split2")
            };
            toAdd.Add(newObj);
        }
        
        if (newCoords3.Count >= 2)
        {
            var newObj = new DrawObject
            {
                id = System.Guid.NewGuid().ToString(),
                type = DrawMode.Line,
                layerName = obj.layerName,
                coordinates = newCoords3,
                rootObj = CreateRoot(obj.layerName + "_split3")
            };
            toAdd.Add(newObj);
        }
        
        toRemove.Add(obj);
    }

    // Hapus cut line visual
    void ClearCutLine()
    {
        foreach (var v in cutVisuals)
            if (v != null) Destroy(v);
        cutVisuals.Clear();
        cutPoints.Clear();
    }

    public void SaveAllChanges()
    {
        foreach (var obj in allObjs)
        {
            onDrawComplete?.Invoke(obj);
        }
    }
}