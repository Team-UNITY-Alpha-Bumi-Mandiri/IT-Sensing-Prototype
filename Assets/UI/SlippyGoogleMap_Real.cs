using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;  

public class SlippyGoogleMap_Real : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform mapArea;   // Panel map (diam)
    public RectTransform tileRoot;  // Container tile (selalu di tengah)
    public GameObject tilePrefab;

    [Header("Map Settings")]
    public int zoom = 16;
    public int tileSize = 256;
    public int gridSize = 5; // 5x5 tile grid

    // koordinat tile tengah
    int centerX, centerY;

    // offset drag
    Vector2 dragOffset = Vector2.zero;

    // tile storage
    Dictionary<string, RawImage> tileDict = new Dictionary<string, RawImage>();

    Vector2 lastMousePos;

    void Start()
    {
        // default awal JAKARTA
        double lat = -6.2088;
        double lon = 106.8456;

        centerX = Lon2TileX(lon, zoom);
        centerY = Lat2TileY(lat, zoom);

        GenerateTiles();
    }

    void Update()
    {
        HandleDrag();
        HandleZoom();
        CheckTileShift();
    }

    // ======================================================
    // DRAG → Mengubah dragOffset, BUKAN geser panel
    // ======================================================
    void HandleDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            lastMousePos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0))
        {
            Vector2 now = Input.mousePosition;
            Vector2 delta = now - lastMousePos;

            dragOffset += delta;
            tileRoot.anchoredPosition = dragOffset;

            lastMousePos = now;
        }
    }

    // ======================================================
    // ZOOM → ubah zoom, regen tile
    // ======================================================
    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0)
        {
            int oldZoom = zoom;

            if (scroll > 0) zoom++;
            else zoom--;

            zoom = Mathf.Clamp(zoom, 3, 19);

            if (zoom != oldZoom)
            {
                foreach (var t in tileDict.Values)
                    Destroy(t.gameObject);

                tileDict.Clear();

                dragOffset = Vector2.zero;
                tileRoot.anchoredPosition = Vector2.zero;

                GenerateTiles();
            }
        }
    }

    // ======================================================
    // TILE SHIFT (jika dragOffset melewati ukuran tile)
    // ======================================================
    void CheckTileShift()
    {
        // geser kiri
        if (dragOffset.x > tileSize)
        {
            dragOffset.x -= tileSize;
            centerX--;
            RegenerateTiles();
        }
        // geser kanan
        else if (dragOffset.x < -tileSize)
        {
            dragOffset.x += tileSize;
            centerX++;
            RegenerateTiles();
        }

        // geser atas
        if (dragOffset.y > tileSize)
        {
            dragOffset.y -= tileSize;
            centerY++;
            RegenerateTiles();
        }
        // geser bawah
        else if (dragOffset.y < -tileSize)
        {
            dragOffset.y += tileSize;
            centerY--;
            RegenerateTiles();
        }

        // update posisi lagi
        tileRoot.anchoredPosition = dragOffset;
    }

    // ======================================================
    // GENERATE TILE GRID
    // ======================================================
    void GenerateTiles()
    {
        int half = gridSize / 2;

        for (int dx = -half; dx <= half; dx++)
        for (int dy = -half; dy <= half; dy++)
        {
            int x = centerX + dx;
            int y = centerY + dy;

            CreateTile(x, y);
        }
    }

    void RegenerateTiles()
    {
        foreach (var t in tileDict.Values)
            Destroy(t.gameObject);

        tileDict.Clear();
        GenerateTiles();
    }

    // ======================================================
    // CREATE SINGLE TILE
    // ======================================================
    void CreateTile(int x, int y)
    {
        string key = $"{zoom}_{x}_{y}";
        if (tileDict.ContainsKey(key)) return;

        GameObject tile = Instantiate(tilePrefab, tileRoot);
        RawImage img = tile.GetComponent<RawImage>();
        tileDict[key] = img;

        RectTransform rt = tile.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tileSize, tileSize);

        rt.anchoredPosition = new Vector2(
            (x - centerX) * tileSize,
            (centerY - y) * tileSize
        );

        StartCoroutine(LoadTile(img, x, y));
    }

    IEnumerator LoadTile(RawImage img, int x, int y)
    {
        string url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                img.texture = DownloadHandlerTexture.GetContent(req);
            }
        }
    }

    // ======================================================
    // COORD CONVERSION FIXED (NO MORE DOUBLE/FLOAT ERRORS)
    // ======================================================
    int Lon2TileX(double lon, int zoom)
    {
        return (int)((lon + 180.0) / 360.0 * (1 << zoom));
    }

    int Lat2TileY(double lat, int zoom)
    {
        double rad = lat * (Mathf.PI / 180.0);
        double n = Math.Log(Math.Tan(Math.PI / 4.0 + rad / 2.0));
        return (int)((1.0 - n / Math.PI) / 2.0 * (1 << zoom));
    }
}
