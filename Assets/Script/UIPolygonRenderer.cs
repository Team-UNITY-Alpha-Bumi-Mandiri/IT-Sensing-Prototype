using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Komponen UI untuk render polygon menggunakan ear-clipping triangulasi
[RequireComponent(typeof(CanvasRenderer))]
public class UIPolygonRenderer : MaskableGraphic
{
    List<Vector2> vertices = new List<Vector2>();

    [Header("Texture Settings")]
    public Texture texture;
    public override Texture mainTexture => texture != null ? texture : base.mainTexture;
    public Rect? uvOverrideBounds = null; // Opsional: Override bounds untuk mapping tekstur lebih akurat

    // Set data vertex dan warna polygon
    public void SetPolygon(List<Vector2> points, Color fillColor)
    {
        vertices = new List<Vector2>(points);
        color = fillColor;
        SetVerticesDirty();
    }

    // Fungsi inti Unity UI untuk generate mesh
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (vertices == null || vertices.Count < 3) return;

        var triangles = Triangulate(vertices);
        if (triangles == null || triangles.Count < 3) return;

        // Hitung bounds untuk mapping UV
        Bounds bounds = GetBounds(vertices);
        float minX = uvOverrideBounds.HasValue ? uvOverrideBounds.Value.xMin : bounds.min.x;
        float maxX = uvOverrideBounds.HasValue ? uvOverrideBounds.Value.xMax : bounds.max.x;
        float minY = uvOverrideBounds.HasValue ? uvOverrideBounds.Value.yMin : bounds.min.y;
        float maxY = uvOverrideBounds.HasValue ? uvOverrideBounds.Value.yMax : bounds.max.y;

        foreach (var v in vertices)
        {
            var vert = UIVertex.simpleVert;
            vert.position = v;
            vert.color = color;
            vert.uv0 = new Vector2(Mathf.InverseLerp(minX, maxX, v.x), Mathf.InverseLerp(minY, maxY, v.y)); // Map posisi ke UV 0-1
            vh.AddVert(vert);
        }

        for (int i = 0; i < triangles.Count; i += 3)
            vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }

    Bounds GetBounds(List<Vector2> points)
    {
        if (points.Count == 0) return new Bounds();
        Bounds b = new Bounds(points[0], Vector3.zero);
        foreach (var p in points) b.Encapsulate(p);
        return b;
    }

    // Algoritma Ear-Clipping untuk pecah polygon jadi segitiga
    List<int> Triangulate(List<Vector2> polygon)
    {
        var indices = new List<int>();
        if (polygon.Count < 3) return indices;

        var remaining = new List<int>();
        for (int i = 0; i < polygon.Count; i++) remaining.Add(i);
        if(!IsCounterClockwise(polygon)) remaining.Reverse(); // Pastikan urutan CCW

        int maxIter = polygon.Count * polygon.Count, iter = 0;
        while (remaining.Count > 3 && iter++ < maxIter)
        {
            bool earFound = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int curr = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];

                Vector2 a = polygon[prev], b = polygon[curr], c = polygon[next];
                if (Cross(b - a, c - b) <= 0) continue; // Skip jika cekung

                bool hasInside = false;
                for (int j = 0; j < remaining.Count && !hasInside; j++)
                {
                    int idx = remaining[j];
                    if (idx != prev && idx != curr && idx != next) hasInside = IsPointInTriangle(polygon[idx], a, b, c);
                }

                if (!hasInside)
                {
                    indices.Add(prev); indices.Add(curr); indices.Add(next);
                    remaining.RemoveAt(i); earFound = true; break;
                }
            }
            if (!earFound) break;
        }

        if (remaining.Count == 3) { indices.Add(remaining[0]); indices.Add(remaining[1]); indices.Add(remaining[2]); }
        return indices;
    }

    bool IsCounterClockwise(List<Vector2> polygon)
    {
        float sum = 0;
        for (int i = 0; i < polygon.Count; i++) sum += (polygon[(i + 1) % polygon.Count].x - polygon[i].x) * (polygon[(i + 1) % polygon.Count].y + polygon[i].y);
        return sum < 0;
    }

    float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
}
