
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

// Responsible for downloading tiles, caching, token checks, and assignment to RawImage.
public class TileLoader : MonoBehaviour
{
    public int maxConcurrentLoads = 6;
    private int activeLoads = 0;

    private Dictionary<string, Texture2D> tileCache = new Dictionary<string, Texture2D>();
    private Dictionary<string, int> tileLoadToken = new Dictionary<string, int>();
    private Queue<TileRequest> loadQueue = new Queue<TileRequest>();
    private int globalToken = 1;

    public string GetKey(int z, int x, int y, object style)
    {
        return $"{z}/{x}/{y}/{(style ?? "default")}";
    }

    // lightweight immutable request wrapper
    public struct TileRequest
    {
        public int z, x, y;
        public string key;
        public RawImage img;
        public int token;
        public TileRequest(int z_, int x_, int y_, RawImage img_)
        {
            z = z_; x = x_; y = y_;
            img = img_;
            key = $"{z}/{x}/{y}";
            token = -1;
        }
    }

    public void Enqueue(TileRequest tr)
    {
        // if cached, assign immediately
        if (tileCache.TryGetValue(tr.key, out Texture2D tex))
        {
            if (tr.img != null)
            {
                tr.img.texture = tex;
                tr.img.color = Color.white;
            }
            return;
        }

        // assign token to invalidate older downloads
        int token = ++globalToken;
        tr.token = token;
        tileLoadToken[tr.key] = token;
        loadQueue.Enqueue(tr);
    }

    void Update()
    {
        ProcessQueue();
    }

    void ProcessQueue()
    {
        while (activeLoads < maxConcurrentLoads && loadQueue.Count > 0)
        {
            var tr = loadQueue.Dequeue();
            // double-check cache (may have been filled)
            if (tileCache.TryGetValue(tr.key, out Texture2D tex))
            {
                if (tr.img != null)
                {
                    tr.img.texture = tex;
                    tr.img.color = Color.white;
                }
                continue;
            }
            StartCoroutine(DoLoad(tr));
            activeLoads++;
        }
    }

    IEnumerator DoLoad(TileRequest tr)
    {
        // check token still valid
        if (!tileLoadToken.TryGetValue(tr.key, out int token) || token != tr.token)
        {
            activeLoads--;
            yield break;
        }

        string url = GetUrlForTile(tr.z, tr.x, tr.y);
        if (string.IsNullOrEmpty(url))
        {
            activeLoads--;
            yield break;
        }

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url + "?nocache=" + UnityEngine.Random.value))
        {
            yield return req.SendWebRequest();

            // token may have changed while downloading
            if (!tileLoadToken.TryGetValue(tr.key, out int after) || after != tr.token)
            {
                activeLoads--;
                yield break;
            }

            if (req.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(req);
                if (tex != null)
                {
                    tileCache[tr.key] = tex;
                    if (tr.img != null && tileLoadToken.TryGetValue(tr.key, out int now) && now == tr.token)
                    {
                        tr.img.texture = tex;
                        tr.img.color = Color.white;
                    }
                }
            }
        }

        activeLoads--;
    }

    // Simple URL builder; default to OSM
    public string GetUrlForTile(int z, int x, int y)
    {
        return $"https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    }

    // Expose cache for Prefetcher (read-only)
    public bool TryGetCached(string key, out Texture2D tex) => tileCache.TryGetValue(key, out tex);

    // Enqueue invisible prefetch (img == null)
    public void EnqueuePrefetch(int z, int x, int y)
    {
        string key = $"{z}/{x}/{y}";
        if (tileCache.ContainsKey(key)) return;
        int token = ++globalToken;
        tileLoadToken[key] = token;
        var tr = new TileRequest(z, x, y, null) { token = token };
        loadQueue.Enqueue(tr);
    }
}
