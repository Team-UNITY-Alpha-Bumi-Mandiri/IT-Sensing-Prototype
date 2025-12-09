using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Custom UI component untuk merender polygon fill dengan bentuk yang sesuai
/// Menggunakan ear-clipping triangulation untuk konversi polygon ke mesh
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIPolygonRenderer : MaskableGraphic
{
    private List<Vector2> vertices = new List<Vector2>();

    /// <summary>
    /// Set vertices polygon dan warna fill
    /// </summary>
    public void SetPolygon(List<Vector2> points, Color fillColor)
    {
        vertices = new List<Vector2>(points);
        color = fillColor;
        SetVerticesDirty();
    }

    /// <summary>
    /// Update vertices saja (tanpa ganti warna)
    /// </summary>
    public void UpdateVertices(List<Vector2> points)
    {
        vertices = new List<Vector2>(points);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (vertices == null || vertices.Count < 3)
            return;

        // Triangulate polygon menggunakan ear-clipping
        List<int> triangles = Triangulate(vertices);

        if (triangles == null || triangles.Count < 3)
            return;

        // Tambahkan vertices ke mesh
        for (int i = 0; i < vertices.Count; i++)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = vertices[i];
            vertex.color = color;
            vh.AddVert(vertex);
        }

        // Tambahkan triangles
        for (int i = 0; i < triangles.Count; i += 3)
        {
            vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }

    /// <summary>
    /// Ear-Clipping Triangulation Algorithm
    /// Konversi polygon menjadi list of triangles
    /// </summary>
    private List<int> Triangulate(List<Vector2> polygon)
    {
        List<int> indices = new List<int>();

        if (polygon.Count < 3)
            return indices;

        // Buat working list dengan index asli
        List<int> remaining = new List<int>();
        for (int i = 0; i < polygon.Count; i++)
            remaining.Add(i);

        // Pastikan polygon dalam urutan counter-clockwise
        if (!IsCounterClockwise(polygon))
        {
            remaining.Reverse();
        }

        int maxIterations = polygon.Count * polygon.Count; // Safety limit
        int iterations = 0;

        while (remaining.Count > 3 && iterations < maxIterations)
        {
            iterations++;
            bool earFound = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int curr = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];

                Vector2 a = polygon[prev];
                Vector2 b = polygon[curr];
                Vector2 c = polygon[next];

                // Cek apakah ini convex vertex (ear candidate)
                if (!IsConvex(a, b, c))
                    continue;

                // Cek apakah ada vertex lain di dalam triangle
                bool hasPointInside = false;
                for (int j = 0; j < remaining.Count; j++)
                {
                    int testIdx = remaining[j];
                    if (testIdx == prev || testIdx == curr || testIdx == next)
                        continue;

                    if (IsPointInTriangle(polygon[testIdx], a, b, c))
                    {
                        hasPointInside = true;
                        break;
                    }
                }

                if (!hasPointInside)
                {
                    // Ini adalah ear! Tambahkan triangle
                    indices.Add(prev);
                    indices.Add(curr);
                    indices.Add(next);

                    // Hapus vertex tengah dari remaining
                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            // Jika tidak ada ear ditemukan, kemungkinan polygon self-intersecting
            if (!earFound)
                break;
        }

        // Tambahkan triangle terakhir
        if (remaining.Count == 3)
        {
            indices.Add(remaining[0]);
            indices.Add(remaining[1]);
            indices.Add(remaining[2]);
        }

        return indices;
    }

    /// <summary>
    /// Cek apakah polygon dalam urutan counter-clockwise
    /// </summary>
    private bool IsCounterClockwise(List<Vector2> polygon)
    {
        float sum = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 curr = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            sum += (next.x - curr.x) * (next.y + curr.y);
        }
        return sum < 0;
    }

    /// <summary>
    /// Cek apakah vertex B adalah convex (sudut < 180 derajat)
    /// </summary>
    private bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        return Cross(b - a, c - b) > 0;
    }

    /// <summary>
    /// Cross product 2D
    /// </summary>
    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    /// <summary>
    /// Cek apakah point P berada di dalam triangle ABC
    /// </summary>
    private bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}
