using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.InputSystem;

public class MapController : MonoBehaviour
{
    public RawImage mapImage;
    public int zoom = 13;
    public double latitude = -7.797068;
    public double longitude = 110.370529;

    private Texture2D mapTexture;
    private Vector2 dragStartPos;
    private bool dragging = false;
    private bool mapDirty = false;   // hanya load saat selesai drag
    private bool loading = false;    // cegah double coroutine

    private const int tileSize = 256;
    private const int gridSize = 3;

    void Start()
    {
        LoadMap();
    }

    void Update()
    {
        HandleZoom();
        HandleDrag();

        // Jika lokasi berubah dan tidak sedang loading, refresh
        if (mapDirty && !loading && !dragging)
        {
            mapDirty = false;
            LoadMap();
        }
    }

    // =======================
    // ZOOM
    // =======================
    void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            zoom += scroll > 0 ? 1 : -1;
            zoom = Mathf.Clamp(zoom, 1, 19);

            mapDirty = true; // tandai bahwa map perlu reload
        }
    }

    // =======================
    // DRAG
    // =======================
    void HandleDrag()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            dragStartPos = Mouse.current.position.ReadValue();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            dragging = false;
            mapDirty = true;  // reload setelah selesai drag
        }

        if (dragging)
        {
            Vector2 now = Mouse.current.position.ReadValue();
            Vector2 delta = now - dragStartPos;

            dragStartPos = now;

            latitude += delta.y * 0.00005;
            longitude -= delta.x * 0.00005;
        }
    }

    // =======================
    // LOAD MAP (NON-SPAM SAFE)
    // =======================
    void LoadMap()
    {
        if (loading) return;

        if (mapTexture != null)
            Destroy(mapTexture);

        mapTexture = new Texture2D(tileSize * gridSize, tileSize * gridSize);
        StartCoroutine(LoadTilesCoroutine());
    }

    IEnumerator LoadTilesCoroutine()
    {
        loading = true;

        int centerX = LonToTileX(longitude, zoom);
        int centerY = LatToTileY(latitude, zoom);

        int half = gridSize / 2;

        for (int y = -half; y <= half; y++)
        {
            for (int x = -half; x <= half; x++)
            {
                int tileX = centerX + x;
                int tileY = centerY + y;

                string url = $"https://tile.openstreetmap.org/{zoom}/{tileX}/{tileY}.png";

                UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tile = DownloadHandlerTexture.GetContent(req);
                    mapTexture.SetPixels(
                        (x + half) * tileSize,
                        (half - y) * tileSize,
                        tileSize,
                        tileSize,
                        tile.GetPixels()
                    );
                }
            }
        }

        mapTexture.Apply();
        mapImage.texture = mapTexture;

        loading = false;
    }

    // =======================
    // TILE MATH
    // =======================
    int LonToTileX(double lon, int zoom)
    {
        return (int)((lon + 180.0) / 360.0 * Mathf.Pow(2, zoom));
    }

    int LatToTileY(double lat, int zoom)
    {
        double latRad = lat * Mathf.Deg2Rad;
        return (int)((1.0 - Mathf.Log(Mathf.Tan((float)latRad) + 1 / Mathf.Cos((float)latRad)) / Mathf.PI) / 2.0 * Mathf.Pow(2, zoom));
    }
}
