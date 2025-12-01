
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Prefetch tiles around the visible grid into the TileLoader cache.
public class Prefetcher : MonoBehaviour
{
    public int prefetchRadius = 2; // additional tiles outside visible grid
    public bool enablePrefetch = true;
    private MapCore mapCore;
    private TileLoader tileLoader;

    public void Initialize(MapCore core)
    {
        mapCore = core;
        tileLoader = core.tileLoader;
    }

    // Called after UpdateAllTiles enqueues visible tiles
    public void SchedulePrefetch()
    {
        if (!enablePrefetch || mapCore == null || tileLoader == null) return;
        StartCoroutine(RunPrefetch());
    }

    IEnumerator RunPrefetch()
    {
        // small delay to avoid launching too many requests when user pans rapidly
        yield return new WaitForSeconds(0.05f);

        int n = 1 << mapCore.zoom;
        int half = mapCore.gridSize / 2;
        int range = half + prefetchRadius;

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                // skip main grid
                if (Mathf.Abs(dx) <= half && Mathf.Abs(dy) <= half) continue;

                int tx = mapCore.centerTile.x + dx;
                int tyRaw = mapCore.centerTile.y + dy;

                if (n > 0) tx = ((tx % n) + n) % n;
                if (tyRaw < 0 || tyRaw >= n) continue;

                int ty = tyRaw;
                // enqueue prefetch (invisible)
                tileLoader.EnqueuePrefetch(mapCore.zoom, tx, ty);

                // small yield to spread requests over frames
                yield return null;
            }
        }
    }
}
