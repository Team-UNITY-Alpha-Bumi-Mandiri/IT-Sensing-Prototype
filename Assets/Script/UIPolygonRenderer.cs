using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// =========================================
// Komponen UI untuk render polygon dengan bentuk bebas
// Menggunakan algoritma Ear-Clipping untuk triangulasi
// =========================================
[RequireComponent(typeof(CanvasRenderer))]
public class UIPolygonRenderer : MaskableGraphic
{
    // Titik-titik sudut polygon
    List<Vector2> vertices = new List<Vector2>();

    // Tekstur opsional
    public Texture texture;
    public override Texture mainTexture => texture != null ? texture : base.mainTexture;

    // Set polygon dengan titik-titik dan warna
    public void SetPolygon(List<Vector2> points, Color fillColor)
    {
        vertices = new List<Vector2>(points);
        color = fillColor;
        SetVerticesDirty(); // Minta redraw
    }

    // Buat mesh dari polygon (dipanggil otomatis oleh Unity UI)
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        // Minimal 3 titik untuk jadi polygon
        if (vertices.Count < 3) return;

        // Pecah polygon jadi segitiga-segitiga
        List<int> tris = Triangulate(vertices);
        if (tris.Count < 3) return;

        // Hitung bounds untuk UV mapping (tekstur)
        Bounds bounds = GetBounds(vertices);
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minY = bounds.min.y;
        float maxY = bounds.max.y;

        // Tambah vertices ke mesh
        foreach (Vector2 v in vertices)
        {
            UIVertex vert = UIVertex.simpleVert;
            vert.position = v;
            vert.color = color;
            
            // Hitung UV (koordinat tekstur 0-1)
            vert.uv0 = new Vector2(
                Mathf.InverseLerp(minX, maxX, v.x),
                Mathf.InverseLerp(minY, maxY, v.y)
            );
            
            vh.AddVert(vert);
        }

        // Buat triangles
        for (int i = 0; i < tris.Count; i += 3)
        {
            vh.AddTriangle(tris[i], tris[i + 1], tris[i + 2]);
        }
    }

    // Cari bounding box dari kumpulan titik
    Bounds GetBounds(List<Vector2> pts)
    {
        if (pts.Count == 0) return new Bounds();

        Bounds b = new Bounds(pts[0], Vector3.zero);
        foreach (Vector2 p in pts)
        {
            b.Encapsulate(p);
        }
        return b;
    }

    // Algoritma Ear-Clipping: potong polygon jadi segitiga-segitiga
    // Cara kerja: cari "telinga" (sudut yang bisa dipotong), potong, ulangi
    List<int> Triangulate(List<Vector2> poly)
    {
        List<int> result = new List<int>();
        if (poly.Count < 3) return result;

        // Buat list index
        List<int> remain = new List<int>();
        for (int i = 0; i < poly.Count; i++)
        {
            remain.Add(i);
        }

        // Pastikan urutan counter-clockwise
        if (!IsCCW(poly))
        {
            remain.Reverse();
        }

        int maxLoop = poly.Count * poly.Count;
        int loop = 0;

        // Loop sampai sisa 3 titik (1 segitiga terakhir)
        while (remain.Count > 3 && loop < maxLoop)
        {
            loop++;
            bool found = false;

            for (int i = 0; i < remain.Count; i++)
            {
                // Ambil 3 titik berurutan
                int prevIdx = (i - 1 + remain.Count) % remain.Count;
                int nextIdx = (i + 1) % remain.Count;

                int prev = remain[prevIdx];
                int curr = remain[i];
                int next = remain[nextIdx];

                Vector2 a = poly[prev];
                Vector2 b = poly[curr];
                Vector2 c = poly[next];

                // Cek apakah sudut convex (cross product > 0)
                if (Cross(b - a, c - b) <= 0) continue;

                // Cek tidak ada titik lain di dalam segitiga ini
                bool hasPointInside = false;
                for (int j = 0; j < remain.Count; j++)
                {
                    int idx = remain[j];
                    if (idx != prev && idx != curr && idx != next)
                    {
                        if (InTriangle(poly[idx], a, b, c))
                        {
                            hasPointInside = true;
                            break;
                        }
                    }
                }

                // Jika valid, potong segitiga ini
                if (!hasPointInside)
                {
                    result.Add(prev);
                    result.Add(curr);
                    result.Add(next);
                    remain.RemoveAt(i);
                    found = true;
                    break;
                }
            }

            if (!found) break;
        }

        // Tambah segitiga terakhir
        if (remain.Count == 3)
        {
            result.AddRange(remain);
        }

        return result;
    }

    // Cek apakah polygon counter-clockwise
    bool IsCCW(List<Vector2> poly)
    {
        float sum = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            int next = (i + 1) % poly.Count;
            sum += (poly[next].x - poly[i].x) * (poly[next].y + poly[i].y);
        }
        return sum < 0;
    }

    // Cross product 2D
    float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    // Cek apakah titik p di dalam segitiga abc
    bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}
