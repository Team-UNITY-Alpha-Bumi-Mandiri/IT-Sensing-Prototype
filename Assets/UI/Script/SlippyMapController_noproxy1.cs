using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem;

public class SlippyMapController_noproxy1 : MonoBehaviour
{
    [Header("UI (assign in Inspector)")]
    public RectTransform tileContainer;
    public RectTransform inputArea;
    public InputField searchField;
    public Button searchButton;
    public RectTransform marker;
    public GameObject infoBubble;
    public Text infoText;

    // FITUR GAYA PETA
    [Header("Map Style Control")]
    [Tooltip("Array tombol untuk setiap gaya peta (indeks harus sesuai dengan urutan enum MapStyle).")]
    public Button[] styleButtons;
    // AKHIR FITUR GAYA PETA

    [Header("Map Settings")]
    public double latitude = -7.797068;
    public double longitude = 110.370529;
    public int zoom = 13;

    public enum MapStyle { OSM, Roadmap, Terrain, Satellite, Hybrid }
    public MapStyle currentStyle = MapStyle.OSM;

    const int TILE_SIZE = 256;
    const int GRID_SIZE = 7; // 7x7 grid

    private Vector2Int centerTile;
    private Dictionary<Vector2Int, RawImage> tiles = new Dictionary<Vector2Int, RawImage>();

    // Drag state
    private bool dragging = false;
    private Vector2 lastMousePos;
    private Vector2 fractionalOffset = Vector2.zero;

    // Search/marker
    private bool hasSearchMarker = false;
    private double searchedLat;
    private double searchedLon;
    private string searchedName = "";

    // Pan / animation
    private bool isPanning = false;
    private float panTime = 0f;
    private float panDuration = 0.6f;
    private double panStartLat, panStartLon;
    private double panTargetLat, panTargetLon;

    // Caching & loading control
    private Dictionary<string, Texture2D> tileCache = new Dictionary<string, Texture2D>();
    private Dictionary<string, int> tileLoadToken = new Dictionary<string, int>();
    private int globalLoadCounter = 1;
    private int maxConcurrentLoads = 4;
    private int activeLoads = 0;
    private Queue<TileRequest> loadQueue = new Queue<TileRequest>();

    // Behavior toggles
    public bool suspendLoadingDuringDrag = false;
    public bool prioritizeCenterFirst = true;

    [Header("Loading & Animation")]
    [Tooltip("Waktu tunda pemuatan tile setelah mouse berhenti bergerak.")]
    public float dragLoadDelay = 0.15f; 
    private float dragLoadTimer = 0f;
    [Tooltip("Durasi animasi fade in tile.")]
    public float tileFadeDuration = 0.2f; 
    [Tooltip("Hanya fade in, fade out dihilangkan.")]
    public bool useFadeAnimation = true; 

    struct TileRequest
    {
        public int z;
        public int x;
        public int y;
        public string key;
        public RawImage img;
        public int token;
    }

    void Start()
    {
        centerTile = LatLonToTile(latitude, longitude, zoom);
        GenerateTileGrid();
        LoadAllTiles();

        if (searchButton != null)
            searchButton.onClick.AddListener(OnSearch);

        if (marker != null)
        {
            marker.gameObject.SetActive(false);
            var mb = marker.GetComponent<Button>();
            if (mb != null) mb.onClick.AddListener(OnMarkerClicked);
        }

        if (infoBubble != null)
            infoBubble.SetActive(false);

        // LOGIKA START UNTUK KONTROL GAYA PETA
        if (styleButtons != null)
        {
            for (int i = 0; i < styleButtons.Length; i++)
            {
                int styleIndex = i; 
                styleButtons[i].onClick.AddListener(() => SetMapStyleByIndex(styleIndex));
            }
        }
    }

    void Update()
    {
        if (Mouse.current == null) return;

        if (isPanning)
        {
            UpdatePan();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!IsPointerOverMarker() && IsMouseOverInputArea())
                HideMarkerAndBubble();
        }

        if (IsMouseOverInputArea())
        {
            HandleDrag();
            HandleZoom();
        }
        else
        {
            dragging = false;
        }

        if (!isPanning && !dragging)
        {
            if (dragLoadTimer > 0)
            {
                dragLoadTimer -= Time.deltaTime;
                if (dragLoadTimer <= 0)
                {
                    LoadAllTiles();
                    dragLoadTimer = 0;
                }
            }
        }

        ProcessLoadQueue();
    }

    bool IsMouseOverInputArea()
    {
        if (inputArea == null) return true;
        Vector2 mp = Mouse.current.position.ReadValue();
        return RectTransformUtility.RectangleContainsScreenPoint(inputArea, mp, null);
    }

    bool IsPointerOverMarker()
    {
        if (marker == null || !marker.gameObject.activeSelf) return false;
        Vector2 mp = Mouse.current.position.ReadValue();
        return RectTransformUtility.RectangleContainsScreenPoint(marker, mp, null);
    }

    void HideMarkerAndBubble()
    {
        hasSearchMarker = false;
        if (marker) marker.gameObject.SetActive(false);
        if (infoBubble) infoBubble.SetActive(false);
    }

    // ===========================
    // Lat/Lon <-> tile math
    // ===========================
    Vector2Int LatLonToTile(double lat, double lon, int z)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double n = Mathf.Pow(2, z);

        double tileX = (lon + 180.0) / 360.0 * n;
        double tileY = (1.0 - Math.Log(Mathf.Tan((float)latRad) + 1f / Mathf.Cos((float)latRad)) / Math.PI) / 2.0 * n;

        int x = Mathf.FloorToInt((float)tileX);
        int y = Mathf.FloorToInt((float)tileY);

        return new Vector2Int(x, y);
    }

    Vector2 GetFractionalOffset(double lat, double lon)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double n = Mathf.Pow(2, zoom);

        double tileX = (lon + 180.0) / 360.0 * n;
        double tileY = (1.0 - Math.Log(Mathf.Tan((float)latRad) + 1f / Mathf.Cos((float)latRad)) / Math.PI) / 2.0 * n;

        float fx = (float)((tileX - Math.Floor(tileX)) * TILE_SIZE);
        float fy = (float)((tileY - Math.Floor(tileY)) * TILE_SIZE);

        fx = Mathf.Repeat(fx, TILE_SIZE);
        fy = Mathf.Repeat(fy, TILE_SIZE);

        return new Vector2(fx, fy);
    }

    // ===========================
    // Grid generation & render
    // ===========================
    void GenerateTileGrid()
    {
        if (tileContainer == null) return;

        foreach (Transform t in tileContainer)
            Destroy(t.gameObject);

        tiles.Clear();

        int half = GRID_SIZE / 2;
        for (int dx = -half; dx <= half; dx++)
        {
            for (int dy = -half; dy <= half; dy++)
            {
                GameObject g = new GameObject($"Tile_{dx}_{dy}", typeof(RawImage));
                g.transform.SetParent(tileContainer, false);

                RawImage img = g.GetComponent<RawImage>();
                img.rectTransform.sizeDelta = new Vector2(TILE_SIZE, TILE_SIZE);
                img.raycastTarget = false;
                img.color = new Color(1, 1, 1, 0.2f); 

                tiles[new Vector2Int(dx, dy)] = img;
            }
        }
    }

    void ApplyFractionalOffset(Vector2 frac)
    {
        foreach (var kv in tiles)
        {
            Vector2Int off = kv.Key;
            RawImage img = kv.Value;
            if (img == null) continue;

            // Menggunakan -frac.x dan +frac.y untuk memastikan pergerakan UI sesuai dengan pergerakan dunia
            Vector2 pos = new Vector2(off.x * TILE_SIZE - frac.x, -off.y * TILE_SIZE + frac.y);
            img.rectTransform.anchoredPosition = pos;
        }
    }

    string GetTileURLForStyle(int z, int x, int y)
    {
        switch (currentStyle)
        {
            case MapStyle.OSM:
                return $"https://tile.openstreetmap.org/{z}/{x}/{y}.png";
            case MapStyle.Roadmap:
                return $"https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}";
            case MapStyle.Terrain:
                return $"https://mt1.google.com/vt/lyrs=p&x={x}&y={y}&z={z}";
            case MapStyle.Satellite:
                return $"https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}";
            case MapStyle.Hybrid:
                return $"https://mt1.google.com/vt/lyrs=y&x={x}&y={y}&z={z}";
        }
        return "";
    }

    string TileKey(int z, int x, int y)
    {
        return $"{z}/{x}/{y}/{currentStyle}";
    }

    // ===========================
    // Load management & FADE Coroutines
    // ===========================
    void LoadAllTiles()
    {
        loadQueue.Clear(); 
        
        if (tileContainer != null)
            tileContainer.anchoredPosition = Vector2.zero;

        int n = 1 << zoom;
        int half = GRID_SIZE / 2;

        List<TileRequest> requests = new List<TileRequest>();

        for (int dx = -half; dx <= half; dx++)
        {
            for (int dy = -half; dy <= half; dy++)
            {
                Vector2Int off = new Vector2Int(dx, dy);
                if (!tiles.ContainsKey(off)) continue;

                RawImage img = tiles[off];
                int tx = centerTile.x + dx;
                int tyRaw = centerTile.y + dy;

                if (n > 0)
                    tx = (tx % n + n) % n;

                if (tyRaw < 0 || tyRaw >= n)
                {
                    img.texture = null;
                    img.color = new Color(1, 1, 1, 0f);
                    continue;
                }

                int ty = tyRaw;
                
                img.texture = null;
                img.color = new Color(1, 1, 1, 0.2f);

                string key = TileKey(zoom, tx, ty);
                int token = ++globalLoadCounter;
                tileLoadToken[key] = token;

                TileRequest tr = new TileRequest { z = zoom, x = tx, y = ty, key = key, img = img, token = token };
                requests.Add(tr);
            }
        }

        if (prioritizeCenterFirst)
        {
            requests = requests.OrderBy(r =>
            {
                int relX = r.x - centerTile.x;
                int relY = r.y - centerTile.y;
                int nwrap = 1 << zoom;
                if (nwrap > 0)
                {
                    int wrapX = relX;
                    if (wrapX > nwrap / 2) wrapX -= nwrap;
                    if (wrapX < -nwrap / 2) wrapX += nwrap;
                    relX = wrapX;
                }
                return Math.Abs(relX) + Math.Abs(relY);
            }).ToList();
        }

        foreach (var r in requests)
            loadQueue.Enqueue(r);
    }

    void ProcessLoadQueue()
    {
        if (suspendLoadingDuringDrag && dragging)
            return;

        while (activeLoads < maxConcurrentLoads && loadQueue.Count > 0)
        {
            TileRequest tr = loadQueue.Dequeue();

            if (tileCache.TryGetValue(tr.key, out Texture2D cachedTex))
            {
                if (tr.img != null)
                {
                    if (useFadeAnimation)
                    {
                        StartCoroutine(FadeInTile(tr.img, cachedTex));
                    }
                    else
                    {
                        tr.img.texture = cachedTex;
                        tr.img.color = Color.white;
                    }
                }
                continue;
            }

            StartCoroutine(LoadTileCoroutine(tr));
            activeLoads++;
        }
    }

    IEnumerator LoadTileCoroutine(TileRequest tr)
    {
        if (!tileLoadToken.TryGetValue(tr.key, out int currentToken) || currentToken != tr.token)
        {
            activeLoads--;
            yield break;
        }

        string url = GetTileURLForStyle(tr.z, tr.x, tr.y);
        if (string.IsNullOrEmpty(url))
        {
            activeLoads--;
            yield break;
        }

        string finalUrl = url + "?nocache=" + UnityEngine.Random.value;

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(finalUrl))
        {
            yield return req.SendWebRequest();

            if (!tileLoadToken.TryGetValue(tr.key, out int afterToken) || afterToken != tr.token)
            {
                activeLoads--;
                yield break;
            }

            if (req.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                if (tex != null)
                {
                    tileCache[tr.key] = tex;
                    if (tr.img != null && tileLoadToken.TryGetValue(tr.key, out int nowToken) && nowToken == tr.token)
                    {
                        if (useFadeAnimation)
                        {
                            StartCoroutine(FadeInTile(tr.img, tex));
                        }
                        else
                        {
                            tr.img.texture = tex;
                            tr.img.color = Color.white;
                        }
                    }
                }
            }
            else
            {
                Debug.LogError($"Failed to load tile {tr.key}: {req.error}. URL: {finalUrl}");
            }
        }

        activeLoads--;
    }

    IEnumerator FadeInTile(RawImage img, Texture2D tex)
    {
        if (img == null || tex == null) yield break;

        img.texture = tex;
        float startAlpha = img.color.a; 
        
        if (startAlpha < 0.01f)
        {
            startAlpha = 0.01f;
            img.color = new Color(1, 1, 1, startAlpha);
        }
        
        float elapsed = 0f;

        while (elapsed < tileFadeDuration)
        {
            elapsed += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(startAlpha, 1f, elapsed / tileFadeDuration);
            img.color = new Color(1, 1, 1, currentAlpha);
            yield return null;
        }
        
        img.color = Color.white;
    }

    // ===========================
    // Drag / Zoom (FINAL REVISI UNTUK INVERSION)
    // ===========================
    void HandleDrag()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            lastMousePos = Mouse.current.position.ReadValue();
            dragLoadTimer = 0f; 
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            dragging = false; 

        if (!dragging) return;

        Vector2 now = Mouse.current.position.ReadValue();
        Vector2 delta = now - lastMousePos; // Delta mouse dalam pixel
        lastMousePos = now;

        // Hitung total pixel dunia di zoom saat ini
        double totalPixels = TILE_SIZE * Math.Pow(2, zoom);
        double pixelPerDeg = totalPixels / 360.0;
        
        // Pergerakan Longitude (X):
        // Diubah dari -= menjadi +=.
        // Jika drag ke kanan (delta.x positif) dan pergerakan sebelumnya terasa terbalik, 
        // maka += akan membalikkan arah secara efektif.
        longitude += delta.x / pixelPerDeg; 

        // Pergerakan Latitude (Y): Perlu koreksi Mercator (cos(lat))
        double latRad = latitude * Mathf.Deg2Rad;
        double scale = Math.Cos(latRad);
        if (scale <= 0) scale = 0.0001; 

        // Diubah dari += menjadi -=.
        // Jika drag ke atas (delta.y positif) dan pergerakan sebelumnya terasa terbalik,
        // maka -= akan membalikkan arah secara efektif.
        latitude -= delta.y / (pixelPerDeg * scale); 
        
        // Batasi latitude agar tidak melebihi batas Proyeksi Mercator
        latitude = Mathf.Clamp((float)latitude, -85.0511287798f, 85.0511287798f); 

        Vector2Int newCenter = LatLonToTile(latitude, longitude, zoom);
        
        if (newCenter != centerTile)
        {
            centerTile = newCenter;
        }
        
        dragLoadTimer = dragLoadDelay;

        Vector2 frac = GetFractionalOffset(latitude, longitude);
        ApplyFractionalOffset(frac);

        UpdateMarkerPosition();
    }
    // ===========================
    // AKHIR REVISI HANDLE DRAG
    // ===========================

    void HandleZoom()
    {
        float s = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(s) < 0.1f) return;

        if (s > 0) SetZoom(zoom + 1); else SetZoom(zoom - 1);
    }

    public void ZoomIn() => SetZoom(zoom + 1);
    public void ZoomOut() => SetZoom(zoom - 1);

    void SetZoom(int newZoom)
    {
        newZoom = Mathf.Clamp(newZoom, 2, 19);
        if (newZoom == zoom) return;

        zoom = newZoom;
        centerTile = LatLonToTile(latitude, longitude, zoom);
        fractionalOffset = GetFractionalOffset(latitude, longitude);

        loadQueue.Clear();
        
        LoadAllTiles(); 
        
        ApplyFractionalOffset(fractionalOffset);
        UpdateMarkerPosition();
    }

    // ===========================
    // Marker / Search / Pan
    // ===========================
    void UpdateMarkerPosition()
    {
        if (!hasSearchMarker || marker == null) return;

        Vector2 frac = GetFractionalOffset(searchedLat, searchedLon);
        Vector2Int t = LatLonToTile(searchedLat, searchedLon, zoom);

        int dx = t.x - centerTile.x;
        int dy = t.y - centerTile.y;

        int n = 1 << zoom;
        if (n > 0)
        {
            int wrappedDx = dx;
            if (wrappedDx > n / 2) wrappedDx -= n;
            if (wrappedDx < -n / 2) wrappedDx += n;
            dx = wrappedDx;
        }

        Vector2 pos = new Vector2(dx * TILE_SIZE + frac.x, -(dy * TILE_SIZE + frac.y));
        marker.anchoredPosition = pos;

        if (infoBubble != null && infoBubble.activeSelf)
            infoBubble.GetComponent<RectTransform>().anchoredPosition = pos + new Vector2(0, 80f);
    }

    void OnSearch()
    {
        if (searchField == null) return;
        string q = searchField.text.Trim();
        if (q.Length == 0) return;
        StartCoroutine(PhotonSearch(q));
    }

    IEnumerator PhotonSearch(string q)
    {
        string url = $"https://photon.komoot.io/api/?limit=1&q={UnityWebRequest.EscapeURL(q)}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) yield break;

        PhotonResponse res = JsonUtility.FromJson<PhotonResponse>(req.downloadHandler.text);
        if (res == null || res.features == null || res.features.Length == 0) yield break;

        var f = res.features[0];
        searchedLon = f.geometry.coordinates[0];
        searchedLat = f.geometry.coordinates[1];
        searchedName = BuildFullAddress(f.properties);

        hasSearchMarker = true;
        if (marker) marker.gameObject.SetActive(true);

        StartPan(searchedLat, searchedLon);
    }

    void StartPan(double lat, double lon)
    {
        isPanning = true;
        panTime = 0f;
        panStartLat = latitude;
        panStartLon = longitude;
        panTargetLat = lat;
        panTargetLon = lon;
    }

    void UpdatePan()
    {
        panTime += Time.deltaTime;
        float t = Mathf.Clamp01(panTime / panDuration);
        t = t * t * (3 - 2 * t);

        latitude = Mathf.Lerp((float)panStartLat, (float)panTargetLat, t);
        longitude = Mathf.Lerp((float)panStartLon, (float)panTargetLon, t);

        Vector2 frac = GetFractionalOffset(latitude, longitude);
        ApplyFractionalOffset(frac);

        Vector2Int newCenter = LatLonToTile(latitude, longitude, zoom);
        
        if (newCenter != centerTile)
        {
            centerTile = newCenter;
            dragLoadTimer = dragLoadDelay;
        }

        UpdateMarkerPosition();

        if (t >= 1f)
        {
            isPanning = false;
            centerTile = LatLonToTile(latitude, longitude, zoom);
            Vector2 finalFrac = GetFractionalOffset(latitude, longitude);
            fractionalOffset = finalFrac;
            
            LoadAllTiles(); 
            
            ApplyFractionalOffset(finalFrac);
            UpdateMarkerPosition();
        }
    }

    public void OnMarkerClicked()
    {
        if (!hasSearchMarker) return;
        if (infoBubble != null) infoBubble.SetActive(true);
        if (infoText != null) infoText.text = searchedName;
        UpdateMarkerPosition();
    }
    
    // ===========================
    // Map Style Control
    // ===========================
    public void SetMapStyleByIndex(int styleIndex)
    {
        if (styleIndex < 0 || styleIndex >= Enum.GetValues(typeof(MapStyle)).Length)
        {
            Debug.LogError($"Indeks gaya peta tidak valid: {styleIndex}");
            return;
        }

        MapStyle newStyle = (MapStyle)styleIndex;
        
        if (newStyle != currentStyle)
        {
            Debug.Log($"Mengubah gaya peta dari {currentStyle} ke {newStyle}");
            currentStyle = newStyle;
            
            tileCache.Clear();
            tileLoadToken.Clear();
            
            centerTile = LatLonToTile(latitude, longitude, zoom);
            LoadAllTiles();
            ApplyFractionalOffset(GetFractionalOffset(latitude, longitude));

            UpdateMarkerPosition();
        }
    }

    [Serializable] public class PhotonResponse { public PhotonFeature[] features; }
    [Serializable] public class PhotonFeature { public PhotonGeometry geometry; public PhotonProperties properties; }
    [Serializable] public class PhotonGeometry { public double[] coordinates; }
    [Serializable] public class PhotonProperties
    {
        public string name; public string street; public string housenumber; public string city;
        public string postcode; public string county; public string state; public string country;
    }

    string BuildFullAddress(PhotonProperties p)
    {
        if (p == null) return "";
        List<string> parts = new List<string>();
        if (!string.IsNullOrEmpty(p.name)) parts.Add(p.name);
        string st = "";
        if (!string.IsNullOrEmpty(p.street)) st += p.street;
        if (!string.IsNullOrEmpty(p.housenumber)) st += " " + p.housenumber;
        if (st.Length > 0) parts.Add(st);
        if (!string.IsNullOrEmpty(p.city)) parts.Add(p.city);
        if (!string.IsNullOrEmpty(p.postcode)) parts.Add(p.postcode);
        if (!string.IsNullOrEmpty(p.state)) parts.Add(p.state);
        if (!string.IsNullOrEmpty(p.country)) parts.Add(p.country);
        return string.Join(", ", parts);
    }
}