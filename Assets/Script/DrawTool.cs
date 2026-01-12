using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Events;
using System.IO;

// =========================================
// Tool untuk menggambar Point, Line, dan Polygon di peta
// Mode: Point (titik), Line (garis), Polygon (area), Delete (hapus)
// =========================================
public class DrawTool : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController; // Kontrol peta
    public RectTransform container;                // Parent objek gambar

    [Header("Prefabs")]
    public GameObject pointMarkerPrefab; // Prefab titik
    public GameObject lineSegmentPrefab; // Prefab garis
    public GameObject polygonFillPrefab; // Prefab fill (tidak dipakai langsung)
    public GameObject vertexPointPrefab; // Prefab sudut polygon
    public GameObject tooltipPrefab;     // Prefab tooltip

    [Header("Settings")]
    public float snapDist = 30f;            // Jarak snap dalam pixel
    public bool forceTextureOnNext = false; // Pakai tekstur di gambar berikutnya

    // Enum mode gambar
    public enum DrawMode { Point, Line, Polygon, Delete }

    // Event selesai gambar (untuk didengarkan script lain)
    public UnityEvent<DrawObject> onDrawComplete;
    public UnityEvent<DrawObject> onObjectDeleted; // Event saat objek dihapus

    [Header("Warna")]
    Color colPoint = Color.blue;
    Color colVertex = Color.white;
    Color colLine = new Color(0, 0.42f, 1);
    Color colFill = new Color(0, 0.42f, 1, 0.3f);
    
    public string texturePath = "Assets/Image Map/mapa del mundo pixel art.jpg";
    Texture2D cachedTex;

    // State internal
    bool isDrawing = false;
    bool isActive = false;
    DrawMode mode = DrawMode.Point;
    int drawId = 0;
    
    public string currentDrawingLayer = ""; // Layer saat ini (dari UI)
    
    // Cache posisi peta
    double lastLat, lastLon;
    int lastZoom;

    // Data objek
    List<DrawObject> allObjs = new List<DrawObject>();
    DrawObject activeObj;
    
    // UI Helper
    GameObject ghost;
    GameObject tooltip;
    TMP_Text tooltipText;

    // =========================================
    // Kelas data objek gambar
    // =========================================
    [System.Serializable]
    public class DrawObject
    {
        public string id = System.Guid.NewGuid().ToString();
        public DrawMode type;
        public bool useTexture;
        public string layerName; // Nama layer penampung
        public List<Vector2> coordinates = new List<Vector2>(); // Lat, Lon
        public List<GameObject> visuals = new List<GameObject>();  // UI objects
        public GameObject rootObj;
    }

    // =========================================
    // INISIALISASI
    // =========================================
    void Start()
    {
        if (container != null)
        {
            CreateHelpers();
            LoadTexture();
        }
    }

    // Muat tekstur dari file
    void LoadTexture()
    {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, texturePath);
        
        if (!File.Exists(path))
        {
            path = texturePath;
        }

        if (File.Exists(path))
        {
            cachedTex = new Texture2D(2, 2);
            cachedTex.LoadImage(File.ReadAllBytes(path));
        }
    }

    // =========================================
    // UPDATE LOOP
    // =========================================
    void Update()
    {
        if (mapController == null || container == null) return;

        // Refresh visual jika peta bergerak
        bool mapMoved = mapController.latitude != lastLat || 
                        mapController.longitude != lastLon || 
                        mapController.zoom != lastZoom;
        
        if (mapMoved)
        {
            RefreshAll();
            lastLat = mapController.latitude;
            lastLon = mapController.longitude;
            lastZoom = mapController.zoom;
        }

        HandleInput();
    }

    // =========================================
    // HANDLE INPUT
    // =========================================
    void HandleInput()
    {
        if (!isActive || Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Update ghost dan tooltip
        if (isDrawing && activeObj != null && activeObj.coordinates.Count > 0)
        {
            UpdateGhost(mousePos);
        }
        else
        {
            if (ghost != null) ghost.SetActive(false);
        }
        UpdateTooltip(mousePos);

        // Klik kiri: tambah titik atau hapus
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (mode == DrawMode.Delete)
            {
                TryDelete(mousePos);
            }
            else
            {
                Vector2 latLon = mapController.ScreenToLatLon(mousePos);
                AddPoint(latLon, mousePos);
            }
        }

        // Klik kanan: undo
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            Undo();
        }

        // ESC: batal gambar
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && isDrawing)
        {
            Cancel();
        }
    }

    // =========================================
    // LOGIKA GAMBAR
    // =========================================
    
    // Tambah titik baru
    void AddPoint(Vector2 latLon, Vector2 screenPos)
    {
        // Mode Point: langsung jadi 1 titik
        if (mode == DrawMode.Point)
        {
            CreateObj(DrawMode.Point, new List<Vector2> { latLon });
            onDrawComplete?.Invoke(allObjs[allObjs.Count - 1]);
            return;
        }

        // Mode Line/Polygon: mulai baru jika belum
        if (!isDrawing)
        {
            StartDraw();
        }

        // Cek snap untuk selesai
        int minPoints = (mode == DrawMode.Polygon) ? 3 : 2;
        if (activeObj.coordinates.Count >= minPoints)
        {
            // Target snap: awal (polygon) atau akhir (line)
            int targetIdx = (mode == DrawMode.Line) ? activeObj.coordinates.Count - 1 : 0;
            Vector2 target = activeObj.coordinates[targetIdx];
            Vector2 targetScreen = GeoToScreen(target);

            if (Vector2.Distance(screenPos, targetScreen) < snapDist)
            {
                FinishDraw();
                return;
            }
        }

        // Tambah titik
        activeObj.coordinates.Add(latLon);
        Rebuild(activeObj, true);
    }

    // Mulai gambar baru
    void StartDraw()
    {
        isDrawing = true;
        drawId++;
        
        activeObj = new DrawObject
        {
            type = mode,
            useTexture = forceTextureOnNext,
            layerName = currentDrawingLayer, // Simpan layer asal
            rootObj = CreateRoot($"Draw_{drawId}_{mode}")
        };
    }

    // Selesai gambar
    void FinishDraw()
    {
        if (activeObj == null) return;

        Rebuild(activeObj, false); // Finalize visual
        allObjs.Add(activeObj);
        onDrawComplete?.Invoke(activeObj);
        Reset();
    }

    // Batal gambar
    void Cancel()
    {
        if (activeObj != null && activeObj.rootObj != null)
        {
            Destroy(activeObj.rootObj);
        }
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

    // =========================================
    // VISUAL
    // =========================================
    
    // Refresh semua visual
    void RefreshAll()
    {
        foreach (DrawObject obj in allObjs)
        {
            Rebuild(obj);
        }

        if (activeObj != null)
        {
            Rebuild(activeObj, true);
        }
    }

    // Bangun ulang visual satu objek
    void Rebuild(DrawObject obj, bool editing = false)
    {
        ClearVisuals(obj);

        if (obj.coordinates.Count == 0) return;

        Transform parent = (obj.rootObj != null) ? obj.rootObj.transform : container;

        // Point: hanya 1 marker
        if (obj.type == DrawMode.Point)
        {
            SpawnIcon(pointMarkerPrefab, obj.coordinates[0], parent, colPoint, obj);
            return;
        }

        // Vertex (titik sudut) saat editing
        if (editing)
        {
            foreach (Vector2 p in obj.coordinates)
            {
                SpawnIcon(vertexPointPrefab, p, parent, colVertex, obj);
            }
        }

        // Garis-garis penghubung
        for (int i = 1; i < obj.coordinates.Count; i++)
        {
            SpawnLine(obj.coordinates[i - 1], obj.coordinates[i], parent, colLine, obj);
        }

        // Polygon: tutup loop dan fill
        if (obj.type == DrawMode.Polygon && obj.coordinates.Count >= 3)
        {
            // Garis penutup (saat tidak editing)
            if (!editing)
            {
                int lastIdx = obj.coordinates.Count - 1;
                SpawnLine(obj.coordinates[lastIdx], obj.coordinates[0], parent, colLine, obj);
            }

            // Fill area
            CreateFill(obj);
        }
    }

    // Spawn icon/marker di posisi geo
    void SpawnIcon(GameObject prefab, Vector2 latLon, Transform parent, Color c, DrawObject obj)
    {
        GameObject go = Instantiate(prefab, parent);
        go.GetComponent<RectTransform>().anchoredPosition = MapLoc(latLon);
        
        Image img = go.GetComponent<Image>();
        if (img != null)
        {
            img.color = c;
        }
        
        obj.visuals.Add(go);
    }

    // Spawn garis antara 2 titik
    void SpawnLine(Vector2 a, Vector2 b, Transform parent, Color c, DrawObject obj)
    {
        GameObject go = Instantiate(lineSegmentPrefab, parent);
        SetLineRect(go.GetComponent<RectTransform>(), a, b);
        
        Image img = go.GetComponent<Image>();
        if (img != null)
        {
            img.color = c;
        }
        
        obj.visuals.Add(go);
    }

    // Set posisi dan rotasi garis
    void SetLineRect(RectTransform rt, Vector2 a, Vector2 b)
    {
        Vector2 p1 = MapLoc(a);
        Vector2 p2 = MapLoc(b);
        Vector2 dir = p2 - p1;

        rt.anchoredPosition = p1 + dir * 0.5f; // Titik tengah
        rt.sizeDelta = new Vector2(dir.magnitude, rt.sizeDelta.y); // Panjang
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg); // Rotasi
    }

    // Buat fill polygon
    void CreateFill(DrawObject obj)
    {
        GameObject go = new GameObject("Fill", typeof(RectTransform), typeof(UIPolygonRenderer));
        go.transform.SetParent(obj.rootObj.transform, false);
        go.transform.SetAsFirstSibling();

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = container.sizeDelta;
        rt.anchoredPosition = Vector2.zero;

        // Konversi koordinat
        List<Vector2> pts = new List<Vector2>();
        foreach (Vector2 c in obj.coordinates)
        {
            pts.Add(MapLoc(c));
        }

        UIPolygonRenderer renderer = go.GetComponent<UIPolygonRenderer>();
        if (obj.useTexture && cachedTex != null)
        {
            renderer.SetPolygon(pts, Color.white);
            renderer.texture = cachedTex;
        }
        else
        {
            renderer.SetPolygon(pts, colFill);
        }

        obj.visuals.Add(go);
    }

    // =========================================
    // HELPER
    // =========================================
    
    void ClearVisuals(DrawObject obj)
    {
        foreach (GameObject v in obj.visuals)
        {
            if (v != null) Destroy(v);
        }
        obj.visuals.Clear();
    }

    GameObject CreateRoot(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(container, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        return go;
    }

    // Konversi LatLon ke posisi lokal UI
    Vector2 MapLoc(Vector2 latLon)
    {
        return mapController.LatLonToLocalPosition(latLon.x, latLon.y);
    }

    // Konversi LatLon ke posisi layar
    Vector2 GeoToScreen(Vector2 latLon)
    {
        Vector3 worldPos = container.TransformPoint(MapLoc(latLon));
        return RectTransformUtility.WorldToScreenPoint(null, worldPos);
    }

    // =========================================
    // DELETE
    // =========================================
    void TryDelete(Vector2 mousePos)
    {
        for (int i = allObjs.Count - 1; i >= 0; i--)
        {
            if (HitTest(allObjs[i], mousePos))
            {
                onObjectDeleted?.Invoke(allObjs[i]);

                if (allObjs[i].rootObj != null)
                {
                    Destroy(allObjs[i].rootObj);
                }
                allObjs.RemoveAt(i);
                return;
            }
        }
    }

    // Cek apakah mouse mengenai objek
    bool HitTest(DrawObject obj, Vector2 mousePos)
    {
        // Konversi koordinat ke screen
        List<Vector2> pts = new List<Vector2>();
        foreach (Vector2 c in obj.coordinates)
        {
            pts.Add(GeoToScreen(c));
        }

        // Cek titik
        if (obj.type == DrawMode.Point)
        {
            return Vector2.Distance(mousePos, pts[0]) < snapDist;
        }

        // Cek garis
        for (int i = 0; i < pts.Count - 1; i++)
        {
            if (DistToLine(mousePos, pts[i], pts[i + 1]) < snapDist)
            {
                return true;
            }
        }

        // Cek polygon
        if (obj.type == DrawMode.Polygon && pts.Count >= 3)
        {
            // Cek garis penutup
            if (DistToLine(mousePos, pts[pts.Count - 1], pts[0]) < snapDist)
            {
                return true;
            }

            // Cek di dalam polygon
            if (InPoly(mousePos, pts))
            {
                return true;
            }
        }

        return false;
    }

    // Jarak titik ke segmen garis
    float DistToLine(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 n = b - a;
        float len2 = n.sqrMagnitude;

        float t = 0;
        if (len2 != 0)
        {
            t = Mathf.Clamp01(Vector2.Dot(p - a, n) / len2);
        }

        return Vector2.Distance(p, a + n * t);
    }

    // Cek titik di dalam polygon (raycasting)
    bool InPoly(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        int j = poly.Count - 1;

        for (int i = 0; i < poly.Count; i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    // =========================================
    // GHOST & TOOLTIP
    // =========================================
    void CreateHelpers()
    {
        // Ghost line
        if (ghost == null && lineSegmentPrefab != null)
        {
            ghost = Instantiate(lineSegmentPrefab, container);
            ghost.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
            ghost.SetActive(false);
        }

        // Tooltip
        if (tooltip == null && tooltipPrefab != null)
        {
            tooltip = Instantiate(tooltipPrefab, container);
            tooltipText = tooltip.GetComponentInChildren<TMP_Text>();
            tooltip.SetActive(false);
        }
    }

    void UpdateGhost(Vector2 mousePos)
    {
        if (ghost == null) return;

        ghost.SetActive(true);
        ghost.transform.SetAsLastSibling();

        Vector2 lastPoint = activeObj.coordinates[activeObj.coordinates.Count - 1];
        Vector2 mouseLatLon = mapController.ScreenToLatLon(mousePos);
        SetLineRect(ghost.GetComponent<RectTransform>(), lastPoint, mouseLatLon);
    }

    void UpdateTooltip(Vector2 mousePos)
    {
        if (tooltip == null || mode == DrawMode.Point)
        {
            if (tooltip != null) tooltip.SetActive(false);
            return;
        }

        // Posisikan tooltip
        RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mousePos, null, out Vector2 localPos);
        tooltip.GetComponent<RectTransform>().anchoredPosition = localPos + new Vector2(25, -15);
        tooltip.SetActive(true);
        tooltip.transform.SetAsLastSibling();

        // Teks tooltip
        int n = (activeObj != null) ? activeObj.coordinates.Count : 0;
        string txt;

        if (mode == DrawMode.Line)
        {
            if (n == 0)
            {
                // Belum mulai gambar
                txt = "click to start drawing line";
            }
            else
            {
                // Sudah ada titik, hitung jarak
                Vector2 lastPt = activeObj.coordinates[n - 1];
                Vector2 mousePt = mapController.ScreenToLatLon(mousePos);
                float dist = CalcDist(lastPt, mousePt);

                if (n >= 2)
                {
                    // Bisa selesai
                    txt = $"{dist:F2} km\nclick last point to finish line";
                }
                else
                {
                    // Lanjut gambar
                    txt = $"{dist:F2} km\nclick to continue drawing line";
                }
            }
        }
        else
        {
            // Mode Polygon
            if (n == 0)
            {
                txt = "click to start drawing shape";
            }
            else if (n > 2)
            {
                txt = "click the first point to close this shape";
            }
            else
            {
                txt = "click to continue drawing shape";
            }
        }

        if (tooltipText != null)
        {
            tooltipText.text = txt;
        }
    }

    // Hitung jarak dalam km (rumus Haversine)
    float CalcDist(Vector2 p1, Vector2 p2)
    {
        float dLat = (p2.x - p1.x) * Mathf.Deg2Rad;
        float dLon = (p2.y - p1.y) * Mathf.Deg2Rad;

        float a = 0.5f - Mathf.Cos(dLat) / 2 +
                  Mathf.Cos(p1.x * Mathf.Deg2Rad) * Mathf.Cos(p2.x * Mathf.Deg2Rad) *
                  (1 - Mathf.Cos(dLon)) / 2;

        return 12742 * Mathf.Asin(Mathf.Sqrt(a)); // 12742 = diameter bumi (km)
    }

    // =========================================
    // PUBLIC API
    // =========================================
    
    // Aktifkan mode tertentu
    public void ActivateMode(DrawMode m)
    {
        if (isDrawing && mode != m)
        {
            Cancel();
        }
        isActive = true;
        mode = m;
    }

    // Nonaktifkan mode
    public void DeactivateMode(DrawMode m)
    {
        if (mode == m)
        {
            Cancel();
            isActive = false;
        }
    }

    // Cek apakah mode aktif
    public bool IsModeActive(DrawMode m)
    {
        return isActive && mode == m;
    }

    // Load polygon dari luar
    public void LoadPolygon(List<Vector2> coords, bool useTexture, string layerName = "Loaded", string id = null)
    {
        CreateObj(DrawMode.Polygon, coords, layerName, useTexture, id);
    }

    // Buat objek baru
    public void CreateObj(DrawMode type, List<Vector2> coords, bool tex = false)
    {
        DrawObject obj = new DrawObject
        {
            type = type,
            useTexture = tex,
            coordinates = new List<Vector2>(coords),
            rootObj = CreateRoot("Loaded")
        };

        Rebuild(obj);
        allObjs.Add(obj);
    }

    // Buat objek baru dengan Layer
    public void CreateObj(DrawMode type, List<Vector2> coords, string layer, bool tex = false, string id = null)
    {
        DrawObject obj = new DrawObject
        {
            id = string.IsNullOrEmpty(id) ? System.Guid.NewGuid().ToString() : id,
            type = type,
            layerName = layer,
            useTexture = tex,
            coordinates = new List<Vector2>(coords),
            rootObj = CreateRoot("Loaded_" + layer)
        };

        Rebuild(obj);
        allObjs.Add(obj);
    }

    // Set visibilitas layer tertentu
    public void SetLayerVisibility(string layer, bool visible)
    {
        foreach (var obj in allObjs)
        {
            if (obj.layerName == layer && obj.rootObj != null)
            {
                obj.rootObj.SetActive(visible);
            }
        }
    }

    // Undo: hapus titik terakhir atau objek terakhir
    public void Undo()
    {
        if (isDrawing && activeObj != null && activeObj.coordinates.Count > 0)
        {
            // Hapus titik terakhir
            activeObj.coordinates.RemoveAt(activeObj.coordinates.Count - 1);

            if (activeObj.coordinates.Count == 0)
            {
                Cancel();
            }
            else
            {
                Rebuild(activeObj, true);
            }
        }
        else if (allObjs.Count > 0)
        {
            // Hapus objek terakhir
            int lastIdx = allObjs.Count - 1;
            if (allObjs[lastIdx].rootObj != null)
            {
                Destroy(allObjs[lastIdx].rootObj);
            }
            allObjs.RemoveAt(lastIdx);
        }
    }

    // Hapus semua objek
    public void ClearAll()
    {
        foreach (DrawObject obj in allObjs)
        {
            if (obj.rootObj != null)
            {
                Destroy(obj.rootObj);
            }
        }
        allObjs.Clear();
        Reset();
    }

    // Set visibility semua objek (tanpa hapus data)
    public void SetAllVisibility(bool visible)
    {
        foreach (DrawObject obj in allObjs)
        {
            if (obj.rootObj != null)
            {
                obj.rootObj.SetActive(visible);
            }
        }
    }

    // Cek apakah drawing dengan ID tertentu ada
    public bool HasDrawing(string id)
    {
        return allObjs.Exists(x => x.id == id);
    }

    // Paksa sembunyikan semua visual di container (Brute Force)
    public void ForceHideAllVisuals()
    {
        if (container == null) return;

        // Hanya sembunyikan objek yang terdaftar di DrawTool
        foreach (DrawObject obj in allObjs)
        {
            if (obj.rootObj != null)
            {
                obj.rootObj.SetActive(false);
            }
        }

        // Sembunyikan helper
        if (ghost != null) ghost.SetActive(false);
        if (tooltip != null) tooltip.SetActive(false);
    }

    // Set visibility drawing berdasarkan ID
    public void ShowDrawing(string id, bool visible)
    {
        DrawObject obj = allObjs.Find(x => x.id == id);
        if (obj != null && obj.rootObj != null)
        {
            obj.rootObj.SetActive(visible);
        }
    }
}