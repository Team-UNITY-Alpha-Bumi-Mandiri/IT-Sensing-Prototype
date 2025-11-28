using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem;

public class SlippyMapController_noproxy1 : MonoBehaviour
{
    [Header("UI")]
    public RectTransform tileContainer;
    public RectTransform inputArea;
    public InputField searchField;
    public Button searchButton;
    public RectTransform marker;
    public GameObject infoBubble;
    public Text infoText;

    [Header("Map Settings")]
    public double latitude = -7.797068;
    public double longitude = 110.370529;
    public int zoom = 13;

    public enum MapStyle { OSM, Roadmap, Terrain, Satellite, Hybrid }
    public MapStyle currentStyle = MapStyle.OSM;

    const int TILE_SIZE = 256;
    const int GRID_SIZE = 5;

    private Vector2Int centerTile;
    private Dictionary<Vector2Int, RawImage> tiles = new Dictionary<Vector2Int, RawImage>();

    private bool dragging = false;
    private Vector2 lastMousePos;

    // Search
    private bool hasSearchMarker = false;
    private Vector2Int searchedTile;
    private double searchedLat;
    private double searchedLon;
    private string searchedName = "";

    // Smooth pan
    private bool isPanning = false;
    private float panTime = 0;
    private float panDuration = 0.6f;
    private double panStartLat, panStartLon;
    private double panTargetLat, panTargetLon;

    void Start()
    {
        centerTile = LatLonToTile(latitude, longitude, zoom);

        GenerateTileGrid();
        LoadAllTiles();

        if (searchButton)
            searchButton.onClick.AddListener(OnSearch);

        if (marker)
        {
            marker.gameObject.SetActive(false);
            if (marker.GetComponent<Button>() != null)
                marker.GetComponent<Button>().onClick.AddListener(OnMarkerClicked);
        }

        if (infoBubble)
            infoBubble.SetActive(false);
    }

    void Update()
    {
        if (Mouse.current == null) return;

        if (isPanning)
        {
            UpdatePan();
            return;
        }

        // CLICK MAP → hide marker & bubble
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
        else dragging = false;
    }

    // ======================================================
    // HELPER INPUT FUNCTIONS
    // ======================================================
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

    // ======================================================
    // TILE MATH
    // ======================================================
    double LerpDouble(double a, double b, double t) => a + (b - a) * t;

    Vector2Int LatLonToTile(double lat, double lon, int zoomLevel)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double n = System.Math.Pow(2.0, zoomLevel);

        int x = (int)((lon + 180.0) / 360.0 * n);
        int y = (int)((1.0 - System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad)) / System.Math.PI) / 2.0 * n);

        return new Vector2Int(x, y);
    }

    Vector2 GetFractionalOffset(double lat, double lon)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double n = System.Math.Pow(2.0, zoom);

        double tileX = (lon + 180.0) / 360.0 * n;
        double tileY = (1.0 - System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad)) / System.Math.PI) / 2.0 * n;

        float fx = (float)((tileX - System.Math.Floor(tileX)) * TILE_SIZE);
        float fy = (float)((tileY - System.Math.Floor(tileY)) * TILE_SIZE);

        return new Vector2(fx, fy);
    }

    // ======================================================
    // TILE GRID
    // ======================================================
    void GenerateTileGrid()
    {
        foreach (Transform t in tileContainer)
            Destroy(t.gameObject);

        tiles.Clear();

        int half = GRID_SIZE / 2;

        for (int dx = -half; dx <= half; dx++)
        {
            for (int dy = -half; dy <= half; dy++)
            {
                GameObject go = new GameObject($"Tile_{dx}_{dy}", typeof(RawImage));
                go.transform.SetParent(tileContainer, false);

                RawImage img = go.GetComponent<RawImage>();
                img.rectTransform.sizeDelta = new Vector2(TILE_SIZE, TILE_SIZE);
                img.rectTransform.anchoredPosition = new Vector2(dx * TILE_SIZE, -dy * TILE_SIZE);
                img.raycastTarget = false;

                tiles[new Vector2Int(dx, dy)] = img;
            }
        }
    }

    string GetTileURL(int x, int y)
    {
        switch (currentStyle)
        {
            case MapStyle.OSM: return $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
            case MapStyle.Roadmap: return $"https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={zoom}";
            case MapStyle.Terrain: return $"https://mt1.google.com/vt/lyrs=p&x={x}&y={y}&z={zoom}";
            case MapStyle.Satellite: return $"https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={zoom}";
            case MapStyle.Hybrid: return $"https://mt1.google.com/vt/lyrs=y&x={x}&y={y}&z={zoom}";
        }
        return "";
    }

    void LoadAllTiles()
    {
        foreach (var kv in tiles)
        {
            var off = kv.Key;
            RawImage img = kv.Value;

            int tx = centerTile.x + off.x;
            int ty = centerTile.y + off.y;

            img.texture = null;
            img.color = new Color(1, 1, 1, 0.3f);

            StartCoroutine(LoadTile(tx, ty, img));
        }

        UpdateMarkerPosition();
    }

    IEnumerator LoadTile(int x, int y, RawImage img)
    {
        string url = GetTileURL(x, y) + "?nocache=" + Random.value;
        UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            img.texture = tex;
            img.color = Color.white;
        }
    }

    // ======================================================
    // DRAG MAP
    // ======================================================
    void HandleDrag()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            lastMousePos = Mouse.current.position.ReadValue();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            dragging = false;

        if (!dragging) return;

        Vector2 now = Mouse.current.position.ReadValue();
        Vector2 delta = now - lastMousePos;
        lastMousePos = now;

        tileContainer.anchoredPosition += delta;

        if (tileContainer.anchoredPosition.x > TILE_SIZE / 2)
        {
            tileContainer.anchoredPosition -= new Vector2(TILE_SIZE, 0);
            centerTile.x--;
            LoadAllTiles();
        }
        else if (tileContainer.anchoredPosition.x < -TILE_SIZE / 2)
        {
            tileContainer.anchoredPosition += new Vector2(TILE_SIZE, 0);
            centerTile.x++;
            LoadAllTiles();
        }

        if (tileContainer.anchoredPosition.y > TILE_SIZE / 2)
        {
            tileContainer.anchoredPosition -= new Vector2(0, TILE_SIZE);
            centerTile.y++;
            LoadAllTiles();
        }
        else if (tileContainer.anchoredPosition.y < -TILE_SIZE / 2)
        {
            tileContainer.anchoredPosition += new Vector2(0, TILE_SIZE);
            centerTile.y--;
            LoadAllTiles();
        }

        UpdateMarkerPosition();
    }

    // ======================================================
    // ZOOM
    // ======================================================
    void HandleZoom()
    {
        float s = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(s) < 0.1f) return;
        SetZoom(zoom + (s > 0 ? 1 : -1));
    }

    public void ZoomIn() => SetZoom(zoom + 1);
    public void ZoomOut() => SetZoom(zoom - 1);

    void SetZoom(int newZoom)
    {
        newZoom = Mathf.Clamp(newZoom, 2, 19);
        if (newZoom == zoom) return;

        zoom = newZoom;

        centerTile = LatLonToTile(latitude, longitude, zoom);
        tileContainer.anchoredPosition = Vector2.zero;

        LoadAllTiles();
        UpdateMarkerPosition();
    }

    // ======================================================
    // MARKER
    // ======================================================
    void UpdateMarkerPosition()
    {
        if (!hasSearchMarker || marker == null) return;

        Vector2 frac = GetFractionalOffset(searchedLat, searchedLon);
        searchedTile = LatLonToTile(searchedLat, searchedLon, zoom);

        int dx = searchedTile.x - centerTile.x;
        int dy = searchedTile.y - centerTile.y;

        Vector2 pos = new Vector2(
            dx * TILE_SIZE + frac.x,
            -(dy * TILE_SIZE + frac.y)
        );

        marker.anchoredPosition = pos;

        if (infoBubble != null && infoBubble.activeSelf)
        {
            RectTransform bubble = infoBubble.GetComponent<RectTransform>();
            bubble.anchoredPosition = pos + new Vector2(0, 90f);
        }
    }

    // ======================================================
    // SEARCH (PHOTON)
    // ======================================================
    void OnSearch()
    {
        string q = searchField.text.Trim();
        if (q.Length == 0) return;

        StartCoroutine(PhotonSearch(q));
    }

    IEnumerator PhotonSearch(string q)
    {
        string url = "https://photon.komoot.io/api/?limit=1&q=" + UnityWebRequest.EscapeURL(q);

        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) yield break;

        PhotonResponse result = JsonUtility.FromJson<PhotonResponse>(req.downloadHandler.text);
        if (result == null || result.features.Length == 0) yield break;

        var feature = result.features[0];
        searchedLon = feature.geometry.coordinates[0];
        searchedLat = feature.geometry.coordinates[1];
        searchedName = feature.properties.name;

        hasSearchMarker = true;
        marker.gameObject.SetActive(true);

        StartPan(searchedLat, searchedLon);
    }

    // ======================================================
    // SMOOTH PAN
    // ======================================================
    void StartPan(double lat, double lon)
    {
        isPanning = true;
        panTime = 0;

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

        latitude = LerpDouble(panStartLat, panTargetLat, t);
        longitude = LerpDouble(panStartLon, panTargetLon, t);

        Vector2 frac = GetFractionalOffset(latitude, longitude);
        tileContainer.anchoredPosition = new Vector2(frac.x, -frac.y);

        Vector2Int newCenter = LatLonToTile(latitude, longitude, zoom);
        if (newCenter != centerTile)
        {
            centerTile = newCenter;
            LoadAllTiles();
        }

        UpdateMarkerPosition();

        if (t >= 1)
        {
            isPanning = false;
            tileContainer.anchoredPosition = Vector2.zero;

            centerTile = LatLonToTile(latitude, longitude, zoom);
            LoadAllTiles();
            UpdateMarkerPosition();
        }
    }

    // ======================================================
    // MARKER BUBBLE
    // ======================================================
    public void OnMarkerClicked()
    {
        if (!hasSearchMarker) return;

        if (infoBubble != null)
        {
            infoBubble.SetActive(true);
            if (infoText != null)
                infoText.text = searchedName;
        }

        UpdateMarkerPosition();
    }

    // ======================================================
    // JSON TYPES
    // ======================================================
    [System.Serializable]
    public class PhotonResponse { public PhotonFeature[] features; }
    [System.Serializable]
    public class PhotonFeature { public PhotonGeometry geometry; public PhotonProperties properties; }
    [System.Serializable]
    public class PhotonGeometry { public double[] coordinates; }
    [System.Serializable]
    public class PhotonProperties { public string name; }
}
