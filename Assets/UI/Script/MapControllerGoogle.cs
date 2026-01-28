using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class MapControllerGoogle : MonoBehaviour
{
    public RectTransform tileContainer;
    public int zoom = 15;
    public double latitude = -6.2088;
    public double longitude = 106.8456;

    private Vector2Int centerTile;
    private const int TILE_SIZE = 256;
    private Dictionary<string, RawImage> tileImages = new Dictionary<string, RawImage>();

    private Vector2 dragStartPos;

    void Start()
    {
        centerTile = LatLonToTile(latitude, longitude, zoom);
        GenerateTiles();
        LoadAllTiles();
    }

    void Update()
    {
        HandleDragging();
        HandleZoom();
    }

    // -----------------------------------------------------------
    // FIXED VERSION: using System.Math (double)
    // -----------------------------------------------------------
    public Vector2Int LatLonToTile(double lat, double lon, int zoom)
    {
        double latRad = lat * (System.Math.PI / 180.0);
        double n = System.Math.Pow(2.0, zoom);

        int x = (int)((lon + 180.0) / 360.0 * n);

        int y = (int)(
            (1.0 - System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad)) / System.Math.PI)
            / 2.0 * n
        );

        return new Vector2Int(x, y);
    }

    // -----------------------------------------------------------
    void GenerateTiles()
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                GameObject g = new GameObject($"Tile_{dx}_{dy}", typeof(RawImage));
                g.transform.SetParent(tileContainer, false);

                RawImage img = g.GetComponent<RawImage>();
                img.rectTransform.sizeDelta = new Vector2(TILE_SIZE, TILE_SIZE);

                string key = $"{dx}_{dy}";
                tileImages[key] = img;
            }
        }
    }

    // -----------------------------------------------------------
    IEnumerator LoadTile(int x, int y, RawImage img)
    {
        string url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";

        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            img.texture = DownloadHandlerTexture.GetContent(www);
        }
    }

    // -----------------------------------------------------------
    void LoadAllTiles()
    {
        foreach (var kv in tileImages)
        {
            string key = kv.Key;
            RawImage img = kv.Value;

            string[] parts = key.Split('_');
            int dx = int.Parse(parts[0]);
            int dy = int.Parse(parts[1]);

            int tx = centerTile.x + dx;
            int ty = centerTile.y + dy;

            img.rectTransform.anchoredPosition = new Vector2(dx * TILE_SIZE, -dy * TILE_SIZE);

            StartCoroutine(LoadTile(tx, ty, img));
        }
    }

    // -----------------------------------------------------------
    void HandleDragging()
    {
        if (Input.GetMouseButtonDown(0))
            dragStartPos = Input.mousePosition;

        if (Input.GetMouseButton(0))
        {
            Vector2 delta = (Vector2)Input.mousePosition - dragStartPos;
            dragStartPos = Input.mousePosition;

            tileContainer.anchoredPosition += delta;

            if (Mathf.Abs(tileContainer.anchoredPosition.x) > TILE_SIZE / 2)
                ShiftTilesHorizontal();

            if (Mathf.Abs(tileContainer.anchoredPosition.y) > TILE_SIZE / 2)
                ShiftTilesVertical();
        }
    }

    void ShiftTilesHorizontal()
    {
        int direction = tileContainer.anchoredPosition.x > 0 ? -1 : 1;
        centerTile.x += direction;
        tileContainer.anchoredPosition = Vector2.zero;
        LoadAllTiles();
    }

    void ShiftTilesVertical()
    {
        int direction = tileContainer.anchoredPosition.y > 0 ? -1 : 1;
        centerTile.y += direction;
        tileContainer.anchoredPosition = Vector2.zero;
        LoadAllTiles();
    }

    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0)
        {
            zoom = Mathf.Clamp(zoom + (scroll > 0 ? 1 : -1), 2, 19);
            centerTile = LatLonToTile(latitude, longitude, zoom);
            LoadAllTiles();
        }
    }
}
