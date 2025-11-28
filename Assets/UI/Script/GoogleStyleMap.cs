using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem;

public class SlippyGoogleMapFinal : MonoBehaviour
{
    [Header("References")]
    public RectTransform mapRoot;

    [Header("Settings")]
    public int zoom = 4;
    public float tileSize = 256f;

    private Dictionary<string, RawImage> tiles = new Dictionary<string, RawImage>();
    private Vector2 offset;                
    private Vector2 dragVelocity;          

    void Start()
    {
        Debug.Log("MAP STARTED OK");
        GenerateTiles();
    }

    void Update()
    {
        HandleDrag();
        HandleZoom();
        UpdateTilePositions();
    }

    // ---------------------------------------------------------
    // DRAG (smooth + inertia)
    // ---------------------------------------------------------
    void HandleDrag()
    {
        var mouse = Mouse.current;
        if (mouse.leftButton.isPressed)
        {
            dragVelocity += mouse.delta.ReadValue() * 1.5f;
        }

        offset += dragVelocity * Time.deltaTime;
        dragVelocity = Vector2.Lerp(dragVelocity, Vector2.zero, Time.deltaTime * 8f);
    }

    // ---------------------------------------------------------
    // ZOOM
    // ---------------------------------------------------------
    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            zoom = Mathf.Clamp(zoom + (scroll > 0 ? 1 : -1), 2, 18);

            foreach (var t in tiles.Values)
                Destroy(t.gameObject);

            tiles.Clear();
            GenerateTiles();
        }
    }

    // ---------------------------------------------------------
    // GENERATE TILES
    // ---------------------------------------------------------
    void GenerateTiles()
    {
        int cx = 1 << (zoom - 1);
        int cy = 1 << (zoom - 1);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                CreateTile(cx + dx, cy + dy);
            }
        }
    }

    // Create tile object + start download
    void CreateTile(int x, int y)
    {
        string id = $"{zoom}_{x}_{y}";

        GameObject go = new GameObject(id, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(mapRoot, false);

        RawImage img = go.GetComponent<RawImage>();
        img.color = Color.white;

        tiles[id] = img;

        StartCoroutine(LoadTile(img, x, y));
    }

    // ---------------------------------------------------------
    // DOWNLOAD TILE (HTTPS SAFE SERVER)
    // ---------------------------------------------------------
    IEnumerator LoadTile(RawImage img, int x, int y)
    {
        string url = $"https://a.tile.openstreetmap.fr/osmfr/{zoom}/{x}/{y}.png";

        UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            img.texture = DownloadHandlerTexture.GetContent(req);
            img.rectTransform.sizeDelta = new Vector2(tileSize, tileSize);
        }
        else
        {
            Debug.LogError("TILE FAILED: " + url + " // " + req.error);
        }
    }

    // ---------------------------------------------------------
    // UPDATE POSITIONS
    // ---------------------------------------------------------
    void UpdateTilePositions()
    {
        foreach (var kvp in tiles)
        {
            string[] s = kvp.Key.Split('_');
            int x = int.Parse(s[1]);
            int y = int.Parse(s[2]);

            Vector2 pos = new Vector2(
                x * tileSize + offset.x - (mapRoot.sizeDelta.x / 2f),
                -(y * tileSize) + offset.y + (mapRoot.sizeDelta.y / 2f)
            );

            kvp.Value.rectTransform.anchoredPosition = pos;
        }
    }
}
