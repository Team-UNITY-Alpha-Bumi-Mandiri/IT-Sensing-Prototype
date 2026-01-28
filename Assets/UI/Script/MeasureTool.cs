using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MeasureTool : MonoBehaviour
{
    [Header("References")]
    public SlippyMapController_noproxy1 map;
    public RectTransform lineContainer;
    public GameObject pointPrefab;
    public GameObject linePrefab;
    public GameObject labelPrefab;

    private List<Vector2> latlonList = new();
    private List<RectTransform> pointList = new();
    private List<LineRenderer> lineList = new();
    private List<RectTransform> labelList = new();

    private LineRenderer previewLine;

    public bool isActive = false; // Diubah menjadi public agar bisa diakses SlippyMapController

    // ========================= START =========================
    void Start()
    {
        // Preview line
        GameObject p = Instantiate(linePrefab, lineContainer);
        previewLine = p.GetComponent<LineRenderer>();
        previewLine.positionCount = 2;
        previewLine.useWorldSpace = false;
        previewLine.enabled = false;
        
        // PASTIKAN DI UNITY HIERARCHY: 
        // MeasureLineContainer ADALAH CHILD dari TileContainer (di SlippyMapController)
    }

    // ========================= UPDATE =========================
    void Update()
    {
        if (!isActive) return;

        Vector2 mouse = Mouse.current.position.ReadValue();
        if (!IsInMap(mouse)) return;

        // Add point on left click
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            AddPoint(mouse);
        }

        // Preview line
        if (pointList.Count > 0)
        {
            // PENTING: Gunakan tileContainer untuk konversi karena LineContainer adalah child-nya.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                map.tileContainer, mouse, null, out Vector2 localMouse);

            Vector2 lastP = pointList[^1].anchoredPosition;

            previewLine.enabled = true;
            previewLine.SetPosition(0, new Vector3(lastP.x, lastP.y, 0));
            previewLine.SetPosition(1, new Vector3(localMouse.x, localMouse.y, 0));
        }
        else previewLine.enabled = false;
    }

    // ========================= CORE =========================

    bool IsInMap(Vector2 screen)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(
            map.tileContainer, screen, null);
    }

    void AddPoint(Vector2 screen)
    {
        // Convert screen → local UI (relatif terhadap tileContainer)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            map.tileContainer, screen, null, out Vector2 localUI);

        Vector2 latlon = ScreenToLatLon(screen);

        latlonList.Add(latlon);

        // Create UI point
        GameObject p = Instantiate(pointPrefab, lineContainer);
        RectTransform prt = p.GetComponent<RectTransform>();
        prt.anchoredPosition = localUI; // Simpan posisi lokal terhadap tileContainer
        pointList.Add(prt);

        // Create label
        GameObject labObj = Instantiate(labelPrefab, lineContainer);
        RectTransform lrt = labObj.GetComponent<RectTransform>();
        lrt.anchoredPosition = localUI + new Vector2(30, 30);
        labelList.Add(lrt);

        UpdateLines();
        UpdateLabels();
    }

    Vector2 ScreenToLatLon(Vector2 screenPos)
    {
        // Konversi screen mouse pos ke local pixel di dalam tileContainer
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            map.tileContainer, screenPos, null, out Vector2 local);

        // Ambil data peta saat ini
        Vector2Int centerTile = map.LatLonToTile(map.latitude, map.longitude, map.zoom);
        Vector2 offset = map.GetFractionalOffset(map.latitude, map.longitude);

        // Hitung posisi pixel absolut dari sudut kiri atas peta (0,0) pada zoom saat ini
        float x = local.x + offset.x;
        // PENTING: Y Unity UI (ke bawah positif) vs Y Mercator (ke bawah positif), 
        // tetapi Unity RectTransform y=0 di tengah. Karena kita menggunakan local dari tileContainer (pivot tengah), 
        // kita perlu membalik offset y agar sesuai dengan konvensi tile y.
        float y = -(local.y - offset.y); 

        double tileX = centerTile.x + (x / 256.0);
        double tileY = centerTile.y + (y / 256.0);

        double n = Math.Pow(2, map.zoom);

        double lon = (tileX / n) * 360.0 - 180.0;
        double latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * tileY / n)));
        double lat = latRad * 180.0 / Math.PI;

        return new Vector2((float)lat, (float)lon);
    }
    
    // ========================= TAMBAHAN BARU =========================
    
    // Fungsi untuk memproyeksikan ulang Lat/Lon ke posisi pixel UI baru
    Vector2 LatLonToLocalUI(Vector2 latlon)
    {
        // 1. Konversi Lat/Lon titik ke koordinat Tile (titik)
        Vector2Int pointTile = map.LatLonToTile(latlon.x, latlon.y, map.zoom);
        
        // 2. Konversi Lat/Lon pusat peta ke koordinat Tile (pusat)
        Vector2Int centerTile = map.LatLonToTile(map.latitude, map.longitude, map.zoom);
        
        // 3. Ambil fractional offset dari pusat peta
        Vector2 offset = map.GetFractionalOffset(map.latitude, map.longitude);
        
        // 4. Hitung selisih Tile antara titik dan pusat peta
        int dx = pointTile.x - centerTile.x;
        int dy = pointTile.y - centerTile.y;

        // Penanganan World Wrap (Longtitude) - Sama seperti di UpdateMarkerPosition
        int n = 1 << map.zoom;
        if (n > 0)
        {
            int wrappedDx = dx;
            if (wrappedDx > n / 2) wrappedDx -= n;
            if (wrappedDx < -n / 2) wrappedDx += n;
            dx = wrappedDx;
        }

        // 5. Hitung posisi pixel lokal baru relatif terhadap TileContainer (pusatnya 0,0)
        
        // Posisi X: Selisih Tile * Ukuran Tile + Fractional Offset
        float localX = dx * 256f + offset.x; 
        
        // Posisi Y: Selisih Tile * Ukuran Tile + Fractional Offset. Karena sumbu Y Mercator dan Y Unity terbalik 
        // relatif terhadap pusat, gunakan tanda negatif pada tile Y.
        float localY = -(dy * 256f + offset.y); 
        
        return new Vector2(localX, localY);
    }
    
    public void ReprojectAllPoints()
    {
        if (latlonList.Count == 0) return;
        
        for (int i = 0; i < latlonList.Count; i++)
        {
            Vector2 newLocalUI = LatLonToLocalUI(latlonList[i]); 
            
            // Perbarui posisi titik dan label di LineContainer (yang ikut bergerak bersama tileContainer)
            pointList[i].anchoredPosition = newLocalUI;
            labelList[i].anchoredPosition = newLocalUI + new Vector2(30, 30);
        }
        
        // Karena posisi titik berubah, garis harus dibuat ulang
        UpdateLines();
    }
    
    // ========================= LINES =========================

    void UpdateLines()
    {
        // Tetap menggunakan metode HANCURKAN dan BUAT ULANG (walau tidak efisien, tapi paling aman)
        foreach (var l in lineList)
            Destroy(l.gameObject);
        lineList.Clear();

        if (pointList.Count < 2) return;

        for (int i = 0; i < pointList.Count - 1; i++)
        {
            // Karena pointList menyimpan posisi lokal relatif terhadap LineContainer (child dari tileContainer), 
            // garis akan berada di posisi yang benar.
            CreateDashedLine(
                pointList[i].anchoredPosition,
                pointList[i + 1].anchoredPosition
            );
        }
    }

    void CreateDashedLine(Vector2 a, Vector2 b)
    {
        GameObject obj = Instantiate(linePrefab, lineContainer);

        LineRenderer lr = obj.GetComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.transform.localScale = Vector3.one;

        lr.SetPosition(0, new Vector3(a.x, a.y, 0));
        lr.SetPosition(1, new Vector3(b.x, b.y, 0));

        lineList.Add(lr);
    }

    // ========================= LABELS =========================

    void UpdateLabels()
    {
        if (latlonList.Count == 0) return;

        float cumulative = 0;

        // Label titik pertama = 0 km
        labelList[0].GetComponent<Text>().text = "0 km";

        for (int i = 1; i < latlonList.Count; i++)
        {
            cumulative += Haversine(latlonList[i - 1], latlonList[i]);

            float km = cumulative / 1000f;

            // Set text di label titik ini
            labelList[i].GetComponent<Text>().text = km.ToString("0.##") + " km";
        }
    }

    // ========================= HELPER =========================

    float Haversine(Vector2 a, Vector2 b)
    {
        double R = 6371000;
        double lat1 = a.x * Math.PI / 180;
        double lat2 = b.x * Math.PI / 180;
        double dLat = (b.x - a.x) * Math.PI / 180;
        double dLon = (b.y - a.y) * Math.PI / 180;

        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1) * Math.Cos(lat2) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return (float)(2 * R * Math.Asin(Math.Sqrt(h)));
    }

    // ========================= MODE TOGGLE =========================

    public void ToggleDistance()
    {
        if (isActive)
        {
            ClearAll();
            isActive = false;
            previewLine.enabled = false;
            return;
        }

        ClearAll();
        isActive = true;
    }

    void ClearAll()
    {
        latlonList.Clear();

        foreach (var p in pointList) Destroy(p.gameObject);
        foreach (var l in lineList) Destroy(l.gameObject);
        foreach (var lab in labelList) Destroy(lab.gameObject);

        pointList.Clear();
        lineList.Clear();
        labelList.Clear();
    }
}