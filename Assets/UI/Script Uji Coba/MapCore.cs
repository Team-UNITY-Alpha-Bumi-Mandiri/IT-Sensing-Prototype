
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Core map logic: tile math, grid, positioning; delegates tile requests to TileLoader.
public class MapCore : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public RectTransform tileContainer;
    public TileLoader tileLoader;
    public Prefetcher prefetcher;

    [Header("UI Settings")]
    public RectTransform inputArea; // area that accepts drag/zoom

    [Header("Map Settings")]
    public static bool IsOfflineMode = false; // Global toggle for offline mode
    public static MapCore Instance { get; private set; } // Singleton for easy access

    public double latitude = -7.797068;
    public double longitude = 110.370529;
    public int zoom = 13;
    public int gridSize = 7; // 7x7 visible grid
    public int tileSize = 256;

    // internal
    [HideInInspector] public Vector2Int centerTile;
    private Dictionary<Vector2Int, RawImage> tiles = new Dictionary<Vector2Int, RawImage>();
    private Texture2D _gridTexture;

    private Texture2D GetGridTexture()
    {
        if (_gridTexture == null)
        {
            int size = tileSize;
            _gridTexture = new Texture2D(size, size);
            Color bgColor = new Color(0.9f, 0.9f, 0.9f, 1f); // Light grey
            Color gridColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Darker grey for lines
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x == 0 || x == size - 1 || y == 0 || y == size - 1)
                        _gridTexture.SetPixel(x, y, gridColor);
                    else
                        _gridTexture.SetPixel(x, y, bgColor);
                }
            }
            _gridTexture.Apply();
        }
        return _gridTexture;
    }

    void Awake()
    {
        Instance = this;
        if (tileContainer == null) Debug.LogWarning("tileContainer not assigned in MapCore.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        centerTile = LatLonToTile(latitude, longitude, zoom);
        GenerateTileGrid();
        UpdateAllTiles();
        if (prefetcher != null) prefetcher.Initialize(this);
    }

    // Create RawImage tiles as children of tileContainer
    public void GenerateTileGrid()
    {
        if (tileContainer == null) return;

        foreach (Transform t in tileContainer)
            Destroy(t.gameObject);

        tiles.Clear();
        int half = gridSize / 2;
        for (int dx = -half; dx <= half; dx++)
        {
            for (int dy = -half; dy <= half; dy++)
            {
                GameObject g = new GameObject($"Tile_{dx}_{dy}", typeof(RawImage));
                g.transform.SetParent(tileContainer, false);
                var img = g.GetComponent<RawImage>();
                img.rectTransform.sizeDelta = new Vector2(tileSize, tileSize);
                img.raycastTarget = false;
                tiles[new Vector2Int(dx, dy)] = img;
            }
        }
    }

    // Compute tile indices from lat/lon
    public Vector2Int LatLonToTile(double lat, double lon, int z)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double n = Math.Pow(2, z);
        double tileX = (lon + 180.0) / 360.0 * n;
        double tileY = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        int x = (int)Math.Floor(tileX);
        int y = (int)Math.Floor(tileY);
        return new Vector2Int(x, y);
    }

    public Vector2 GetFractionalOffset(double lat, double lon)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double n = Math.Pow(2, zoom);
        double tileX = (lon + 180.0) / 360.0 * n;
        double tileY = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        float fx = (float)((tileX - Math.Floor(tileX)) * tileSize);
        float fy = (float)((tileY - Math.Floor(tileY)) * tileSize);
        fx = Mathf.Repeat(fx, tileSize);
        fy = Mathf.Repeat(fy, tileSize);
        return new Vector2(fx, fy);
    }

    // Position tiles based on fractional offset
    public void ApplyFractionalOffset(Vector2 frac)
    {
        foreach (var kv in tiles)
        {
            Vector2Int off = kv.Key;
            RawImage img = kv.Value;
            if (img == null) continue;
            // -frac.x and +frac.y to match non-inverted movement
            Vector2 pos = new Vector2(off.x * tileSize - frac.x, -off.y * tileSize + frac.y);
            img.rectTransform.anchoredPosition = pos;
        }
    }

    // Enqueue requests for visible tiles; called when center changes or zoom
    public void UpdateAllTiles()
    {
        if (tileContainer != null) tileContainer.anchoredPosition = Vector2.zero;
        int n = 1 << zoom;
        int half = gridSize / 2;

        for (int dx = -half; dx <= half; dx++)
        {
            for (int dy = -half; dy <= half; dy++)
            {
                Vector2Int off = new Vector2Int(dx, dy);
                if (!tiles.ContainsKey(off)) continue;
                RawImage img = tiles[off];
                int tx = centerTile.x + dx;
                int tyRaw = centerTile.y + dy;

                if (n > 0) tx = ((tx % n) + n) % n;

                if (tyRaw < 0 || tyRaw >= n)
                {
                    img.texture = null;
                    img.color = new Color(1,1,1,0f);
                    continue;
                }
                int ty = tyRaw;

                // set placeholder
                img.texture = null;
                img.color = new Color(1, 1, 1, 0.15f);

                // If offline mode, use grid texture
                if (IsOfflineMode)
                {
                    img.texture = GetGridTexture();
                    img.color = Color.white;
                    continue;
                }

                // enqueue load through TileLoader
                if (tileLoader != null)
                {
                    var tr = new TileLoader.TileRequest(zoom, tx, ty, img);
                    tileLoader.Enqueue(tr);
                }
            }
        }

        // after enqueuing visible tiles, schedule prefetch
        if (prefetcher != null) prefetcher.SchedulePrefetch();
    }

    // Public method to set center based on lat/lon change
    public void SetLatLon(double lat, double lon, bool updateTiles = true)
    {
        latitude = Mathf.Clamp((float)lat, -85f, 85f);
        longitude = lon;
        Vector2Int newCenter = LatLonToTile(latitude, longitude, zoom);
        if (newCenter != centerTile)
        {
            centerTile = newCenter;
            if (updateTiles) UpdateAllTiles();
        }
        Vector2 frac = GetFractionalOffset(latitude, longitude);
        ApplyFractionalOffset(frac);
    }

    public void SetZoom(int newZoom)
    {
        newZoom = Mathf.Clamp(newZoom, 2, 19);
        if (newZoom == zoom) return;
        zoom = newZoom;
        centerTile = LatLonToTile(latitude, longitude, zoom);
        GenerateTileGrid();
        UpdateAllTiles();
        Vector2 frac = GetFractionalOffset(latitude, longitude);
        ApplyFractionalOffset(frac);
    }
}
