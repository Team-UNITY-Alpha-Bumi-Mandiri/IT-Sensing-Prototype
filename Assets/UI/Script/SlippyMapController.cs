using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem;

public class SlippyMapController : MonoBehaviour
{
    [Header("UI")]
    public RectTransform tileContainer;
    public RectTransform inputArea;

    [Header("Map Settings")]
    public double latitude = -7.797068;
    public double longitude = 110.370529;
    public int zoom = 13;

    public enum MapStyle { OSM, Terrain, Roadmap }
    public MapStyle currentStyle = MapStyle.OSM;

    const int TILE_SIZE = 256;
    const int GRID_SIZE = 5;

    private Vector2Int centerTile;
    private Dictionary<Vector2Int, RawImage> tiles = new Dictionary<Vector2Int, RawImage>();

    private bool dragging = false;
    private Vector2 lastMousePos;

    void Start()
    {
        if (tileContainer == null) Debug.LogError("tileContainer belum di-assign di Inspector!");
        if (inputArea == null) Debug.LogWarning("inputArea belum di-assign — input area checks will fail.");

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
        else
        {
            dragging = false;
        }
    }

    bool IsMouseOverInputArea()
    {
        if (inputArea == null) return false;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        return RectTransformUtility.RectangleContainsScreenPoint(inputArea, mousePos, null);
    }

    Vector2Int LatLonToTile(double lat, double lon, int zoomLevel)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double n = System.Math.Pow(2.0, zoomLevel);

        int x = (int)((lon + 180.0) / 360.0 * n);
        int y = (int)((1.0 - System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad)) / System.Math.PI) / 2.0 * n);

        return new Vector2Int(x, y);
    }

    // ------------------ TILE CREATION ------------------
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

                tiles[new Vector2Int(dx, dy)] = img;
            }
        }
    }

    // ------------------ GET TILE URL BY STYLE ------------------
   string GetTileURL(int x, int y)
{
    switch (currentStyle)
    {
        case MapStyle.OSM:
            return $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";

        case MapStyle.Terrain:
            return $"https://mt0.google.com/vt/lyrs=p&hl=en&x={x}&y={y}&z={zoom}";

        case MapStyle.Roadmap:
            return $"https://mt0.google.com/vt/lyrs=m&hl=en&x={x}&y={y}&z={zoom}";

        default:
            return $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
    }
}


    // ------------------ RESET TILE SYSTEM ------------------
    void ResetTiles()
    {
        // stop all loads to prevent race
        StopAllCoroutines();

        // Destroy old tile gameobjects
        if (tileContainer != null)
        {
            foreach (Transform child in tileContainer)
                Destroy(child.gameObject);
        }
        tiles.Clear();

        // Create fresh grid
        GenerateTileGrid();
    }

    // ------------------ LOAD ALL TILES ------------------
    void LoadAllTiles()
    {
        if (tiles == null) return;

        foreach (var kvp in tiles)
        {
            Vector2Int offset = kvp.Key;
            RawImage img = kvp.Value;

            // Reset visual state
            if (img == null) continue;
            img.texture = null;
            img.color = new Color(1, 1, 1, 0.3f);

            int tx = centerTile.x + offset.x;
            int ty = centerTile.y + offset.y;

            StartCoroutine(LoadTile(tx, ty, img));
        }
    }

    IEnumerator LoadTile(int x, int y, RawImage img)
    {
        string url = GetTileURL(x, y) + "?v=" + Random.value;  // force no-cache
        Debug.Log($"[SlippyMap] Loading tile: style={currentStyle} x={x} y={y} z={zoom} url={url}");

        UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);
        // request no-cache headers (may or may not be honored by server)
        req.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
        req.SetRequestHeader("Pragma", "no-cache");
        req.SetRequestHeader("Expires", "0");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            // if img was destroyed meanwhile, skip
            if (img == null) yield break;

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            img.texture = tex;
            img.color = Color.white;
        }
        else
        {
            Debug.LogWarning($"[SlippyMap] Failed to load tile: {url} -> {req.result} / {req.error}");
        }
    }

    // ------------------ DRAG SYSTEM ------------------
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

        Vector2 currentPos = Mouse.current.position.ReadValue();
        Vector2 delta = currentPos - lastMousePos;
        lastMousePos = currentPos;

        tileContainer.anchoredPosition += delta;

        if (tileContainer.anchoredPosition.x > TILE_SIZE / 2f)
        {
            tileContainer.anchoredPosition -= new Vector2(TILE_SIZE, 0);
            centerTile.x -= 1;
            LoadAllTiles();
        }
        else if (tileContainer.anchoredPosition.x < -TILE_SIZE / 2f)
        {
            tileContainer.anchoredPosition += new Vector2(TILE_SIZE, 0);
            centerTile.x += 1;
            LoadAllTiles();
        }

        if (tileContainer.anchoredPosition.y > TILE_SIZE / 2f)
        {
            tileContainer.anchoredPosition -= new Vector2(0, TILE_SIZE);
            centerTile.y += 1;
            LoadAllTiles();
        }
        else if (tileContainer.anchoredPosition.y < -TILE_SIZE / 2f)
        {
            tileContainer.anchoredPosition += new Vector2(0, TILE_SIZE);
            centerTile.y -= 1;
            LoadAllTiles();
        }
    }

    // ------------------ ZOOM SYSTEM ------------------
    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        int oldZoom = zoom;
        zoom = Mathf.Clamp(zoom + (scroll > 0 ? 1 : -1), 2, 19);

        if (zoom == oldZoom) return;

        centerTile = LatLonToTile(latitude, longitude, zoom);
        LoadAllTiles();
    }

    public void ZoomIn()
    {
        int oldZoom = zoom;
        zoom = Mathf.Clamp(zoom + 1, 2, 19);

        if (zoom != oldZoom)
        {
            centerTile = LatLonToTile(latitude, longitude, zoom);
            LoadAllTiles();
        }
    }

    public void ZoomOut()
    {
        int oldZoom = zoom;
        zoom = Mathf.Clamp(zoom - 1, 2, 19);

        if (zoom != oldZoom)
        {
            centerTile = LatLonToTile(latitude, longitude, zoom);
            LoadAllTiles();
        }
    }

    // ------------------ CHANGE MAP STYLE (enum) ------------------
    public void SetMapStyle(MapStyle style)
    {
        if (currentStyle == style)
        {
            Debug.Log($"[SlippyMap] SetMapStyle called but style already {style}");
            return;
        }

        Debug.Log($"[SlippyMap] Changing style from {currentStyle} -> {style}");
        currentStyle = style;

        ResetTiles();   // stop coroutine + recreate tiles
        LoadAllTiles(); // load new tiles
    }

    // Overload: keep string version for Inspector string calls
    public void SetMapStyle(string styleName)
    {
        // try parse string to enum (case-insensitive)
        if (System.Enum.TryParse<MapStyle>(styleName, true, out MapStyle parsed))
        {
            SetMapStyle(parsed);
        }
        else
        {
            Debug.LogWarning($"[SlippyMap] Unknown styleName '{styleName}' passed to SetMapStyle(string)");
        }
    }

    // ------------------ Helper methods for buttons (no-param) ------------------
    public void SetStyleOSM()      => SetMapStyle(MapStyle.OSM);
    public void SetStyleTerrain()  => SetMapStyle(MapStyle.Terrain);
    public void SetStyleRoadmap()  => SetMapStyle(MapStyle.Roadmap);
}
