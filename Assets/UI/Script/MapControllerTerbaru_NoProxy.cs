using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem;

public class MapController_NoProxy : MonoBehaviour
{
    [Header("UI")]
    public RectTransform tileContainer;
    public RectTransform inputArea;

    [Header("Map Settings")]
    public double latitude = -7.797068;
    public double longitude = 110.370529;
    public int zoom = 13;

    public enum MapStyle
    {
        OSM,
        Roadmap,
        Terrain,
        Satellite,
        Hybrid
    }
    public MapStyle currentStyle = MapStyle.OSM;

    const int TILE_SIZE = 256;
    const int GRID_SIZE = 5;

    private Vector2Int centerTile;
    private Dictionary<Vector2Int, RawImage> tiles = new Dictionary<Vector2Int, RawImage>();

    private bool dragging = false;
    private Vector2 lastMousePos;

    // ----------------------------------------------------------
    void Start()
    {
        if (!tileContainer) Debug.LogError("tileContainer belum di assign!");

        centerTile = LatLonToTile(latitude, longitude, zoom);
        GenerateTileGrid();
        LoadAllTiles();
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

    // ----------------------------------------------------------
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

    // ----------------------------------------------------------
    void GenerateTileGrid()
    {
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

    // ----------------------------------------------------------
    string GetTileURL(int x, int y)
    {
        switch (currentStyle)
        {
            case MapStyle.OSM:
                return $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";

            case MapStyle.Roadmap:
                return $"https://mt0.google.com/vt/lyrs=m&x={x}&y={y}&z={zoom}";

            case MapStyle.Terrain:
                return $"https://mt0.google.com/vt/lyrs=p&x={x}&y={y}&z={zoom}";

            case MapStyle.Satellite:
                return $"https://mt0.google.com/vt/lyrs=s&x={x}&y={y}&z={zoom}";

            case MapStyle.Hybrid:
                return $"https://mt0.google.com/vt/lyrs=y&x={x}&y={y}&z={zoom}";
        }

        return "";
    }

    // ----------------------------------------------------------
    void ResetTiles()
    {
        StopAllCoroutines();

        foreach (Transform c in tileContainer)
            Destroy(c.gameObject);

        tiles.Clear();
        GenerateTileGrid();
    }

    // ----------------------------------------------------------
    void LoadAllTiles()
    {
        foreach (var kvp in tiles)
        {
            Vector2Int offset = kvp.Key;
            RawImage img = kvp.Value;

            img.texture = null;
            img.color = new Color(1, 1, 1, 0.3f);

            int tx = centerTile.x + offset.x;
            int ty = centerTile.y + offset.y;

            StartCoroutine(LoadTile(tx, ty, img));
        }
    }

    IEnumerator LoadTile(int x, int y, RawImage img)
    {
        string baseURL = GetTileURL(x, y);

        string url = baseURL + (baseURL.Contains("?") ? "&" : "?") + "nocache=" + Random.value;

        UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            if (img == null) yield break;
            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            img.texture = tex;
            img.color = Color.white;
        }
        else
        {
            Debug.LogWarning("Tile failed: " + url + " Code=" + req.responseCode);
        }
    }

    // ----------------------------------------------------------
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
    }

    // ----------------------------------------------------------
    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.1f) return;

        int old = zoom;
        zoom = Mathf.Clamp(zoom + (scroll > 0 ? 1 : -1), 2, 19);

        if (zoom != old)
        {
            centerTile = LatLonToTile(latitude, longitude, zoom);
            LoadAllTiles();
        }
    }

    public void ZoomIn() => SetZoom(zoom + 1);
    public void ZoomOut() => SetZoom(zoom - 1);

    void SetZoom(int newZ)
    {
        newZ = Mathf.Clamp(newZ, 2, 19);
        if (newZ == zoom) return;

        zoom = newZ;
        centerTile = LatLonToTile(latitude, longitude, zoom);
        LoadAllTiles();
    }

    // ----------------------------------------------------------
    // STYLE SWITCH — FIXED VERSION (LANGSUNG UPDATE)
    // ----------------------------------------------------------
    public void SetMapStyle(MapStyle style)
    {
        currentStyle = style;

        // Reset posisi container
        tileContainer.anchoredPosition = Vector2.zero;

        // Hitung ulang tile tengah (wajib)
        centerTile = LatLonToTile(latitude, longitude, zoom);

        // Reset grid & reload tile baru
        ResetTiles();
        LoadAllTiles();
    }

    // Buttons
    public void SetOSM() => SetMapStyle(MapStyle.OSM);
    public void SetRoadmap() => SetMapStyle(MapStyle.Roadmap);
    public void SetTerrain() => SetMapStyle(MapStyle.Terrain);
    public void SetSatellite() => SetMapStyle(MapStyle.Satellite);
    public void SetHybrid() => SetMapStyle(MapStyle.Hybrid);
}
