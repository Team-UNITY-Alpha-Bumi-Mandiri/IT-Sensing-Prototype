using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem;

public class SimpleMapController_Baru : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform tileContainer;
    public RectTransform inputArea;

    [Header("Map Settings")]
    public double latitude = -7.797068;
    public double longitude = 110.370529;
    public int zoom = 13;
    public MapStyle currentStyle = MapStyle.OSM;

    public MeasureTool2 measureToolRef;

    [Header("Input Settings")]
public bool isInputEnabled = true; // <-- Tambahkan ini (Default True/Hijau)

    [Header("Smoothness Settings")]
    [Tooltip("Durasi animasi fade-in dalam detik")]
    public float fadeDuration = 0.3f; 
    [Tooltip("Maksimal download berjalan bersamaan")]
    public int maxConcurrentDownloads = 6; 
    [Tooltip("Maksimal tekstur yang diproses per frame (mengurangi lag)")]
    public int maxDecodesPerFrame = 2; 

    public enum MapStyle { OSM, Roadmap, Terrain, Satellite, Hybrid }

    const int TILE_SIZE = 256;
    const int GRID_SIZE = 9; // Grid lebih besar sedikit untuk buffer

    private Vector2Int centerTile;
    private Dictionary<Vector2Int, RawImage> tiles = new Dictionary<Vector2Int, RawImage>();
    private Dictionary<string, Texture2D> tileCache = new Dictionary<string, Texture2D>();
    
    // Antrian Download
    private Queue<TileRequest> downloadQueue = new Queue<TileRequest>();
    private int activeDownloads = 0;
    
    // Drag Logic
    private bool isDragging = false;
    private Vector2 lastMousePos;
    private bool wasInputEnabled = true; // Track previous state
    private int globalUpdateID = 0; // Token validasi update

    struct TileRequest
    {
        public Vector2Int gridPos;
        public string url;
        public int id;
    }

    void Start()
    {
        CreateGrid();
        RefreshMap();
        if (measureToolRef != null) measureToolRef.RebuildAllVisuals();
    }

    void Update()
    {
        HandleInput();
        ProcessDownloadQueue();
    }

    // ========================================================================
    // 1. INPUT HANDLING (DRAG & ZOOM) - Smooth Logic
    // ========================================================================
    void HandleInput()
    {
        if (Mouse.current == null) return;
        
        // Reset lastMousePos when input is re-enabled to prevent jump
        if (isInputEnabled && !wasInputEnabled)
        {
            lastMousePos = Mouse.current.position.ReadValue();
        }
        wasInputEnabled = isInputEnabled;
        
        if (!isInputEnabled) 
        {
            // Reset drag state when input is disabled
            isDragging = false;
            return;
        }
        // --- ZOOM ---
        if (IsMouseInArea())
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll > 0) ChangeZoom(1);
            else if (scroll < 0) ChangeZoom(-1);
        }

        // --- DRAG ---
        if (Mouse.current.leftButton.wasPressedThisFrame && IsMouseInArea())
        {
            isDragging = true;
            lastMousePos = Mouse.current.position.ReadValue();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector2 currentMouse = Mouse.current.position.ReadValue();
            Vector2 delta = currentMouse - lastMousePos;
            lastMousePos = currentMouse;
            MoveMapByPixel(delta);
        }
    }

    bool IsMouseInArea()
    {
        if (inputArea == null) return true;
        return RectTransformUtility.RectangleContainsScreenPoint(inputArea, Mouse.current.position.ReadValue(), null);
    }

    void MoveMapByPixel(Vector2 deltaPixel)
    {
        double n = Math.Pow(2, zoom);
        // Faktor koreksi agar drag terasa 1:1 dengan cursor
        double pixelsPerLon = (TILE_SIZE * n) / 360.0;
        double pixelsPerLat = (TILE_SIZE * n) / (2 * Math.PI);
        double latRad = latitude * Mathf.Deg2Rad;
        double cosLat = Math.Cos(latRad);

        // Geser koordinat
        longitude -= deltaPixel.x / pixelsPerLon;
        latitude -= (deltaPixel.y / pixelsPerLon) * cosLat; // Koreksi Mercator
        
        latitude = Math.Clamp(latitude, -85.0, 85.0);

        // Cek apakah center tile berubah
        Vector2Int newCenter = LatLonToTile(latitude, longitude, zoom);
        
        // Selalu update posisi fisik UI (Smooth movement)
        UpdateTilePositions();

        // Hanya load ulang data jika pindah tile
        if (newCenter != centerTile)
        {
            centerTile = newCenter;
            RefreshMap(false); // false = jangan clear cache visual dulu, biar smooth
        }
    }

    void ChangeZoom(int delta)
    {
        int newZoom = Mathf.Clamp(zoom + delta, 3, 19);
        if (newZoom != zoom)
        {
            zoom = newZoom;
            // Saat zoom, kita reset total agar tidak ada glitch visual
            RefreshMap(true); 
        }
        if (measureToolRef != null) measureToolRef.RebuildAllVisuals();
    }

    // ========================================================================
    // 2. CORE MAP LOGIC & RENDERING
    // ========================================================================
    void CreateGrid()
    {
        foreach (Transform child in tileContainer) Destroy(child.gameObject);
        tiles.Clear();

        int range = GRID_SIZE / 2;
        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                GameObject obj = new GameObject($"Tile_{x}_{y}", typeof(RawImage));
                obj.transform.SetParent(tileContainer, false);
                
                RawImage img = obj.GetComponent<RawImage>();
                img.rectTransform.sizeDelta = new Vector2(TILE_SIZE, TILE_SIZE);
                img.color = Color.clear; // Mulai Transparan

                tiles.Add(new Vector2Int(x, y), img);
            }
        }
    }

    public void RefreshMap(bool clearVisuals = true)
    {
        globalUpdateID++; // Invalidasi request lama
        downloadQueue.Clear(); // Batalkan antrian download
        activeDownloads = 0;
        
        centerTile = LatLonToTile(latitude, longitude, zoom);
        UpdateTilePositions();

        // Queue tile baru
        int n = 1 << zoom;
        
        // Prioritaskan tile tengah (spiral sort atau distance sort)
        var sortedKeys = tiles.Keys.OrderBy(k => Mathf.Abs(k.x) + Mathf.Abs(k.y));

        foreach (Vector2Int gridPos in sortedKeys)
        {
            RawImage img = tiles[gridPos];
            int tx = centerTile.x + gridPos.x;
            int ty = centerTile.y + gridPos.y;

            // World Wrap X
            int finalX = (tx % n + n) % n;
            
            // Validasi Y
            if (ty < 0 || ty >= n)
            {
                img.color = Color.clear;
                continue;
            }

            string url = GetTileUrl(finalX, ty, zoom);

            // Cek Cache
            if (tileCache.TryGetValue(url, out Texture2D cachedTex))
            {
                if (img.texture != cachedTex)
                {
                    img.texture = cachedTex;
                    img.color = Color.white; // Langsung tampil jika cache
                }
            }
            else
            {
                if (clearVisuals) img.color = Color.clear; // Sembunyikan kalau loading baru
                // Masukkan ke antrian download
                downloadQueue.Enqueue(new TileRequest { gridPos = gridPos, url = url, id = globalUpdateID });
            }
        }
    }

    void UpdateTilePositions()
    {
        Vector2 offset = GetFractionalOffset(latitude, longitude);
        foreach (var kvp in tiles)
        {
            // Posisi relatif terhadap anchor container
            float posX = (kvp.Key.x * TILE_SIZE) - offset.x;
            float posY = -(kvp.Key.y * TILE_SIZE) + offset.y;
            kvp.Value.rectTransform.anchoredPosition = new Vector2(posX, posY);
        }
    }

    // ========================================================================
    // 3. DOWNLOAD MANAGER (ANTI-STUTTER)
    // ========================================================================
    void ProcessDownloadQueue()
    {
        if (downloadQueue.Count == 0) return;

        // Hanya izinkan N download berjalan bersamaan
        while (activeDownloads < maxConcurrentDownloads && downloadQueue.Count > 0)
        {
            TileRequest req = downloadQueue.Dequeue();
            StartCoroutine(DownloadRoutine(req));
            activeDownloads++;
        }
    }

    IEnumerator DownloadRoutine(TileRequest req)
    {
        // 1. Download Data
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(req.url))
        {
            yield return www.SendWebRequest();

            activeDownloads--; // Slot download bebas

            // Validasi: Apakah map sudah berubah (geser/zoom) sejak request ini dibuat?
            if (req.id != globalUpdateID) yield break;

            if (www.result == UnityWebRequest.Result.Success)
            {
                // 2. Texture Decoding (Ini berat! Kita harus hati-hati)
                // Kita gunakan DownloadHandlerTexture.GetContent, tapi..
                // Tips Pro: Jangan lakukan ini semua dalam 1 frame jika banyak tile selesai.
                
                // Cek apakah tile masih relevan
                if (tiles.TryGetValue(req.gridPos, out RawImage img))
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(www);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    
                    if (!tileCache.ContainsKey(req.url))
                        tileCache.Add(req.url, tex);

                    img.texture = tex;
                    
                    // 3. Trigger Animasi Fade In
                    StartCoroutine(FadeIn(img));
                }
            }
        }
    }

    IEnumerator FadeIn(RawImage img)
    {
        float t = 0;
        img.color = new Color(1, 1, 1, 0); // Start Transparent
        
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = t / fadeDuration;
            img.color = new Color(1, 1, 1, alpha);
            yield return null;
        }
        img.color = Color.white;
    }

    // ========================================================================
    // 4. UTILS & PUBLIC API
    // ========================================================================
    public void GoToLocation(double lat, double lon, int z)
    {
        latitude = lat;
        longitude = lon;
        zoom = z;
        RefreshMap(true);
    }

    string GetTileUrl(int x, int y, int z)
    {
        switch (currentStyle)
        {
            case MapStyle.Roadmap: return $"https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}";
            case MapStyle.Satellite: return $"https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}";
            default: return $"https://tile.openstreetmap.org/{z}/{x}/{y}.png";
        }
    }

    // Math Helpers
    Vector2Int LatLonToTile(double lat, double lon, int z)
    {
        double n = Math.Pow(2, z);
        double x = (lon + 180.0) / 360.0 * n;
        double latRad = lat * Mathf.Deg2Rad;
        double y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        return new Vector2Int((int)x, (int)y);
    }

    Vector2 GetFractionalOffset(double lat, double lon)
    {
        double n = Math.Pow(2, zoom);
        double x = (lon + 180.0) / 360.0 * n;
        double latRad = lat * Mathf.Deg2Rad;
        double y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        return new Vector2((float)((x - Math.Floor(x)) * TILE_SIZE), (float)((y - Math.Floor(y)) * TILE_SIZE));
    }

    public void ZoomIn()
    {
        ChangeZoom(1); // Memanggil logika zoom yang sudah ada
    }

    public void ZoomOut()
    {
        ChangeZoom(-1); // Memanggil logika zoom yang sudah ada
    }

    // ========================================================================
    // 6. HELPER UNTUK ALAT UKUR (MEASURE TOOL)
    // ========================================================================

    // Mengubah Lat/Lon dunia nyata menjadi posisi lokal di dalam TileContainer
    public Vector2 LatLonToLocalPosition(double lat, double lon)
    {
        Vector2 offset = GetFractionalOffset(latitude, longitude);
        Vector2Int center = LatLonToTile(latitude, longitude, zoom);
        Vector2Int target = LatLonToTile(lat, lon, zoom);

        // Hitung selisih tile
        int dx = target.x - center.x;
        int dy = target.y - center.y;

        // Hitung posisi pixel relatif terhadap pusat container
        float posX = (dx * TILE_SIZE) + GetFractionalOffset(lat, lon).x - offset.x;
        // Koreksi offset internal tile
        double n = Math.Pow(2, zoom);
        double x_pos = ((lon + 180.0) / 360.0 * n);
        double y_pos = (1.0 - Math.Log(Math.Tan(lat * Mathf.Deg2Rad) + 1.0 / Math.Cos(lat * Mathf.Deg2Rad)) / Math.PI) / 2.0 * n;
        
        // Kalkulasi posisi pixel presisi (sub-tile accuracy)
        float preciseX = (float)((x_pos - Math.Floor(x_pos)) * TILE_SIZE);
        float preciseY = (float)((y_pos - Math.Floor(y_pos)) * TILE_SIZE);

        // Gabungkan tile index + sub-tile offset
        // (Kita hitung ulang offset relatif terhadap center tile yang sedang aktif)
        double centerX = ((longitude + 180.0) / 360.0 * n);
        double centerY = (1.0 - Math.Log(Math.Tan(latitude * Mathf.Deg2Rad) + 1.0 / Math.Cos(latitude * Mathf.Deg2Rad)) / Math.PI) / 2.0 * n;

        float finalX = (float)((x_pos - centerX) * TILE_SIZE);
        float finalY = (float)-((y_pos - centerY) * TILE_SIZE); // Y terbalik

        return new Vector2(finalX, finalY);
    }

    // Mengubah posisi Mouse di layar menjadi Lat/Lon
    public Vector2 ScreenToLatLon(Vector2 screenPos)
    {
        // Ubah posisi screen ke posisi lokal di dalam inputArea (atau TileContainer)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(tileContainer, screenPos, null, out Vector2 localPos);

        double n = Math.Pow(2, zoom);
        double centerX = ((longitude + 180.0) / 360.0 * n);
        double centerY = (1.0 - Math.Log(Math.Tan(latitude * Mathf.Deg2Rad) + 1.0 / Math.Cos(latitude * Mathf.Deg2Rad)) / Math.PI) / 2.0 * n;

        double targetTileX = centerX + (localPos.x / TILE_SIZE);
        double targetTileY = centerY - (localPos.y / TILE_SIZE); // Y terbalik

        double finalLon = (targetTileX / n) * 360.0 - 180.0;
        double latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * targetTileY / n)));
        double finalLat = latRad * 180.0 / Math.PI;

        return new Vector2((float)finalLat, (float)finalLon);
    }
}