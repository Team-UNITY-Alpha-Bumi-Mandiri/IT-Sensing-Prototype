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

    void Start()
    {
        centerTile = LatLonToTile(latitude, longitude, zoom);
        GenerateTileGrid();
        LoadAllTiles();

        if (searchButton)
            searchButton.onClick.AddListener(OnSearchPressed);

        if (marker != null)
            marker.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Mouse.current == null) return;

        if (IsMouseOverInputArea())
        {
            HandleDrag();
            HandleZoom();
        }
        else dragging = false;
    }

    bool IsMouseOverInputArea()
    {
        if (inputArea == null) return true;
        Vector2 mp = Mouse.current.position.ReadValue();
        return RectTransformUtility.RectangleContainsScreenPoint(inputArea, mp, null);
    }

    Vector2Int LatLonToTile(double lat, double lon, int zoomLevel)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double n = System.Math.Pow(2.0, zoomLevel);

        int x = (int)((lon + 180.0) / 360.0 * n);
        int y = (int)((1.0 -
            System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad))
            / System.Math.PI) / 2.0 * n);

        return new Vector2Int(x, y);
    }

    Vector2 GetMarkerPixelOffset(double lat, double lon, int zoomLevel)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double n = System.Math.Pow(2.0, zoomLevel);

        double tileX = (lon + 180.0) / 360.0 * n;
        double tileY = (1.0 -
            System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad))
            / System.Math.PI) / 2.0 * n;

        double fracX = tileX - System.Math.Floor(tileX);
        double fracY = tileY - System.Math.Floor(tileY);

        return new Vector2((float)(fracX * TILE_SIZE), (float)(-(fracY * TILE_SIZE)));
    }

    void UpdateMarkerPosition()
    {
        if (marker == null || !marker.gameObject.activeSelf) return;

        double latRad = latitude * Mathf.Deg2Rad;
        double n = System.Math.Pow(2.0, zoom);

        double tileX = (longitude + 180.0) / 360.0 * n;
        double tileY = (1.0 -
            System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad))
            / System.Math.PI) / 2.0 * n;

        double cX = centerTile.x;
        double cY = centerTile.y;

        Vector2 frac = GetMarkerPixelOffset(latitude, longitude, zoom);

        float px = (float)((tileX - cX) * TILE_SIZE) + frac.x + tileContainer.anchoredPosition.x;
        float py = (float)(-(tileY - cY) * TILE_SIZE) + frac.y + tileContainer.anchoredPosition.y;

        marker.anchoredPosition = new Vector2(px, py);
    }

    void GenerateTileGrid()
    {
        foreach (Transform t in tileContainer) Destroy(t.gameObject);
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
            case MapStyle.Roadmap: return $"https://mt0.google.com/vt/lyrs=m&x={x}&y={y}&z={zoom}";
            case MapStyle.Terrain: return $"https://mt0.google.com/vt/lyrs=p&x={x}&y={y}&z={zoom}";
            case MapStyle.Satellite: return $"https://mt0.google.com/vt/lyrs=s&x={x}&y={y}&z={zoom}";
            case MapStyle.Hybrid: return $"https://mt0.google.com/vt/lyrs=y&x={x}&y={y}&z={zoom}";
        }
        return "";
    }

    void LoadAllTiles()
    {
        foreach (var kv in tiles)
        {
            Vector2Int offset = kv.Key;
            RawImage img = kv.Value;

            int tx = centerTile.x + offset.x;
            int ty = centerTile.y + offset.y;

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

        if (tileContainer.anchoredPosition.x > TILE_SIZE / 2f)
        {
            tileContainer.anchoredPosition -= new Vector2(TILE_SIZE, 0);
            centerTile.x--;
            LoadAllTiles();
        }
        else if (tileContainer.anchoredPosition.x < -TILE_SIZE / 2f)
        {
            tileContainer.anchoredPosition += new Vector2(TILE_SIZE, 0);
            centerTile.x++;
            LoadAllTiles();
        }

        if (tileContainer.anchoredPosition.y > TILE_SIZE / 2f)
        {
            tileContainer.anchoredPosition -= new Vector2(0, TILE_SIZE);
            centerTile.y++;
            LoadAllTiles();
        }
        else if (tileContainer.anchoredPosition.y < -TILE_SIZE / 2f)
        {
            tileContainer.anchoredPosition += new Vector2(0, TILE_SIZE);
            centerTile.y--;
            LoadAllTiles();
        }

        UpdateMarkerPosition();
    }

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.1f) return;

        int old = zoom;
        zoom = Mathf.Clamp(zoom + (scroll > 0 ? 1 : -1), 2, 19);

        if (zoom != old)
        {
            centerTile = LatLonToTile(latitude, longitude, zoom);
            tileContainer.anchoredPosition = Vector2.zero;
            LoadAllTiles();
        }
    }

    void OnSearchPressed()
    {
        string text = searchField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        StartCoroutine(SearchLocation_IndonesiaFirst(text));
    }

    IEnumerator SearchLocation_IndonesiaFirst(string q)
    {
        string url =
            "https://nominatim.openstreetmap.org/search?"
            + "format=json&addressdetails=1&limit=10&dedupe=1&countrycodes=id&q="
            + UnityWebRequest.EscapeURL(q);

        UnityWebRequest req = UnityWebRequest.Get(url);
        req.SetRequestHeader("User-Agent", "Unity");
        yield return req.SendWebRequest();

        if (!req.result.Equals(UnityWebRequest.Result.Success)
            || string.IsNullOrEmpty(req.downloadHandler.text)
            || req.downloadHandler.text == "[]")
        {
            StartCoroutine(SearchLocation_Global(q));
            yield break;
        }

        NominatimRaw[] arr = ParseNominatimArray(req.downloadHandler.text);
        if (arr == null || arr.Length == 0)
        {
            StartCoroutine(SearchLocation_Global(q));
            yield break;
        }

        OSMResult r = PickBestLocation(arr);
        if (r != null) SetNewLocation(r);
        else StartCoroutine(SearchLocation_Global(q));
    }

    IEnumerator SearchLocation_Global(string q)
    {
        string url =
            "https://nominatim.openstreetmap.org/search?"
            + "format=json&addressdetails=1&limit=10&dedupe=1&q="
            + UnityWebRequest.EscapeURL(q);

        UnityWebRequest req = UnityWebRequest.Get(url);
        req.SetRequestHeader("User-Agent", "Unity");
        yield return req.SendWebRequest();

        if (!req.result.Equals(UnityWebRequest.Result.Success)
            || string.IsNullOrEmpty(req.downloadHandler.text)
            || req.downloadHandler.text == "[]")
        {
            Debug.Log("Lokasi tidak ditemukan.");
            yield break;
        }

        NominatimRaw[] arr = ParseNominatimArray(req.downloadHandler.text);
        if (arr == null || arr.Length == 0)
        {
            Debug.Log("Lokasi tidak ditemukan.");
            yield break;
        }

        OSMResult r = PickBestLocation(arr);
        if (r != null) SetNewLocation(r);
    }

    // ---------------------- FIX PRIORITAS ----------------------
    OSMResult PickBestLocation(NominatimRaw[] raws)
    {
        string[] priority =
        {
            "city",
            "city_district",
            "suburb",
            "town",
            "village",
            "hamlet"
        };

        // PRIORITY MATCH
        foreach (string p in priority)
        {
            foreach (var r in raws)
            {
                if (r.@class == "place" && r.type == p)
                {
                    if (double.TryParse(r.lat, out double la)
                        && double.TryParse(r.lon, out double lo))
                        return new OSMResult { lat = la, lon = lo };
                }
            }
        }

        // fallback: pertama yang valid
        foreach (var r in raws)
        {
            if (double.TryParse(r.lat, out double la)
                && double.TryParse(r.lon, out double lo))
                return new OSMResult { lat = la, lon = lo };
        }

        return null;
    }

    // JSON WRAPPER
    NominatimRaw[] ParseNominatimArray(string json)
    {
        string w = "{\"items\":" + json + "}";
        try { return JsonUtility.FromJson<NominatimWrapper>(w).items; }
        catch { return null; }
    }

    [System.Serializable] public class NominatimRaw
    {
        public string place_id;
        public string lat;
        public string lon;
        public string @class;
        public string type;
    }

    [System.Serializable] public class NominatimWrapper
    {
        public NominatimRaw[] items;
    }

    [System.Serializable] public class OSMResult
    {
        public double lat;
        public double lon;
    }

    void SetNewLocation(OSMResult r)
    {
        latitude = r.lat;
        longitude = r.lon;

        if (marker) marker.gameObject.SetActive(true);

        centerTile = LatLonToTile(latitude, longitude, zoom);
        tileContainer.anchoredPosition = Vector2.zero;
        LoadAllTiles();
    }
}
