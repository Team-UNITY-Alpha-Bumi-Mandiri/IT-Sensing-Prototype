using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasRenderer))]
public class PolygonRenderer : MaskableGraphic
{
    [SerializeField]
    private List<Vector2> points = new List<Vector2>();

    protected override void Awake()
    {
        base.Awake();
        // Paksa pakai material default UI agar tidak invisible
        if (m_Material == null) m_Material = Canvas.GetDefaultCanvasMaterial();
    }

    public void SetPoints(List<Vector2> newPoints, Color newColor)
    {
        this.points = new List<Vector2>(newPoints);
        this.color = newColor;
        
        SetAllDirty(); // Paksa render ulang
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points == null || points.Count < 3) return;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        // Tambah Titik
        foreach (Vector2 p in points)
        {
            vert.position = new Vector3(p.x, p.y, 0);
            vh.AddVert(vert);
        }

        // Buat Segitiga (Fan)
        for (int i = 1; i < points.Count - 1; i++)
        {
            vh.AddTriangle(0, i, i + 1);
        }
    }
}