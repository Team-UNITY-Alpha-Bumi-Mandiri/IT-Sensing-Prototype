using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Custom UI component untuk merender polygon fill dengan ear-clipping triangulation
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIPolygonRenderer : MaskableGraphic
{
    List<Vector2> vertices = new List<Vector2>();

    public void SetPolygon(List<Vector2> points, Color fillColor)
    {
        vertices = new List<Vector2>(points);
        color = fillColor;
        SetVerticesDirty();
    }

    public void UpdateVertices(List<Vector2> points)
    {
        vertices = new List<Vector2>(points);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (vertices == null || vertices.Count < 3) return;

        var triangles = Triangulate(vertices);
        if (triangles == null || triangles.Count < 3) return;

        // Add vertices
        foreach (var v in vertices)
        {
            var vert = UIVertex.simpleVert;
            vert.position = v;
            vert.color = color;
            vh.AddVert(vert);
        }

        // Add triangles
        for (int i = 0; i < triangles.Count; i += 3)
            vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }

    List<int> Triangulate(List<Vector2> polygon)
    {
        var indices = new List<int>();
        if (polygon.Count < 3) return indices;

        // Create index list
        var remaining = new List<int>();
        for (int i = 0; i < polygon.Count; i++) remaining.Add(i);

        // Ensure counter-clockwise
        if (!IsCounterClockwise(polygon)) remaining.Reverse();

        int maxIter = polygon.Count * polygon.Count;
        int iter = 0;

        while (remaining.Count > 3 && iter++ < maxIter)
        {
            bool earFound = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int curr = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];

                Vector2 a = polygon[prev], b = polygon[curr], c = polygon[next];

                // Check if convex
                if (Cross(b - a, c - b) <= 0) continue;

                // Check no points inside
                bool hasInside = false;
                for (int j = 0; j < remaining.Count && !hasInside; j++)
                {
                    int idx = remaining[j];
                    if (idx != prev && idx != curr && idx != next)
                        hasInside = IsPointInTriangle(polygon[idx], a, b, c);
                }

                if (!hasInside)
                {
                    indices.Add(prev);
                    indices.Add(curr);
                    indices.Add(next);
                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound) break;
        }

        // Add last triangle
        if (remaining.Count == 3)
        {
            indices.Add(remaining[0]);
            indices.Add(remaining[1]);
            indices.Add(remaining[2]);
        }

        return indices;
    }

    bool IsCounterClockwise(List<Vector2> polygon)
    {
        float sum = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var curr = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            sum += (next.x - curr.x) * (next.y + curr.y);
        }
        return sum < 0;
    }

    float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
        (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
}