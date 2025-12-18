using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Komponen UI Graphic custom untuk merender bentuk Polygon arbitrary (bebas).
[RequireComponent(typeof(CanvasRenderer))]
public class UIPolygonRenderer : MaskableGraphic
{
    List<Vector2> vertices = new List<Vector2>(); // Titik-titik sudut polygon

    [Header("Texture Settings")]
    public Texture texture;
    public override Texture mainTexture => texture ? texture : base.mainTexture; // Override tekstur UI
    public Rect? uvOverrideBounds = null; // Opsional: Custom mapping UV

    // Set bentuk polygon dan warnanya dari luar
    public void SetPolygon(List<Vector2> points, Color fillColor)
    {
        vertices = new List<Vector2>(points);
        color = fillColor;
        SetVerticesDirty(); // Minta UI system untuk redraw
    }

    // Core method Unity UI untuk membuat Geometry Mesh
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (vertices.Count < 3) return; // Butuh minimal 3 titik untuk jadi bidang

        // Pecah polygon jadi segitiga-segitiga (Triangulasi)
        var triangles = Triangulate(vertices);
        if (triangles.Count < 3) return;

        // Hitung batas (bounds) untuk mapping koordinat UV (tekstur)
        Bounds b = GetBounds(vertices);
        // Gunakan override jika ada, atau pakai bounds dari vertices
        float minX = uvOverrideBounds?.xMin ?? b.min.x;
        float maxX = uvOverrideBounds?.xMax ?? b.max.x;
        float minY = uvOverrideBounds?.yMin ?? b.min.y;
        float maxY = uvOverrideBounds?.yMax ?? b.max.y;

        // Tambahkan Vertices ke Mesh Buffer
        foreach (var v in vertices)
        {
            var vert = UIVertex.simpleVert;
            vert.position = v;
            vert.color = color;
            // Map posisi vertex (world) ke koordinat tekstur (0-1)
            vert.uv0 = new Vector2(Mathf.InverseLerp(minX, maxX, v.x), Mathf.InverseLerp(minY, maxY, v.y)); 
            vh.AddVert(vert);
        }

        // Susun Triangle Indices
        for (int i = 0; i < triangles.Count; i += 3)
            vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }

    // Helper: Cari bounding box dari kumpulan titik
    Bounds GetBounds(List<Vector2> points)
    {
        if (points.Count == 0) return new Bounds();
        Bounds b = new Bounds(points[0], Vector3.zero);
        foreach (var p in points) b.Encapsulate(p);
        return b;
    }

    // Algoritma Ear-Clipping: Mengubah Polygon kompleks menjadi list segitiga
    List<int> Triangulate(List<Vector2> polygon)
    {
        var indices = new List<int>();
        if (polygon.Count < 3) return indices;

        // Buat list index yang tersedia
        var remaining = new List<int>();
        for (int i = 0; i < polygon.Count; i++) remaining.Add(i);
        
        // Pastikan urutan Counter-Clockwise (CCW) untuk standar winding order Unity
        if(!IsCounterClockwise(polygon)) remaining.Reverse(); 

        int maxIter = polygon.Count * polygon.Count, iter = 0;
        
        // Loop sampai sisa 3 titik (1 segitiga terakhir)
        while (remaining.Count > 3 && iter++ < maxIter)
        {
            bool earFound = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                // Ambil 3 titik berurutan
                int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int curr = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];

                Vector2 a = polygon[prev], b = polygon[curr], c = polygon[next];

                // Cek 1: Sudut harus cembung (convex), cross product > 0
                if (Cross(b - a, c - b) <= 0) continue; 

                // Cek 2: Tidak boleh ada titik lain di dalam segitiga ini (Ear condition)
                bool hasInside = false;
                for (int j = 0; j < remaining.Count; j++)
                {
                    int idx = remaining[j];
                    if (idx != prev && idx != curr && idx != next && IsPointInTriangle(polygon[idx], a, b, c))
                    {
                        hasInside = true; break;
                    }
                }

                // Jika valid (Ear), potong segitiga ini dari polygon
                if (!hasInside)
                {
                    indices.Add(prev); indices.Add(curr); indices.Add(next);
                    remaining.RemoveAt(i); 
                    earFound = true; 
                    break;
                }
            }
            if (!earFound) break; // Fallback jika polygon degeneratif
        }

        // Tambahkan segitiga terakhir
        if (remaining.Count == 3) indices.AddRange(remaining);
        return indices;
    }

    // Cek urutan winding polygon (CCW atau CW)
    bool IsCounterClockwise(List<Vector2> poly)
    {
        float sum = 0;
        for (int i = 0; i < poly.Count; i++) 
            sum += (poly[(i + 1) % poly.Count].x - poly[i].x) * (poly[(i + 1) % poly.Count].y + poly[i].y);
        return sum < 0; // Luas signed area negatif berarti CCW di koordinat Unity yang Y-flipped kadang
    }

    // Hitung Cross Product 2D
    float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // Cek apakah titik p ada dalam segitiga a-b-c
    bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos); // True jika semua tanda sama (di dalam)
    }

    float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
}
