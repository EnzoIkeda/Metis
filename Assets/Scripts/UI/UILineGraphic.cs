using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Uma linha a desenhar: reta ou curva, com ponta de seta e tracejado opcionais.
public struct UILine
{
    public Vector2 Start;
    public Vector2 End;

    // Ponto de controle da curva; igual ao meio da reta deixa a linha reta.
    public Vector2 Control;

    public float Width;
    public Color Color;
    public float ArrowLength;
    public bool Dashed;
}

// Grafico de UI que desenha linhas com espessura, sem textura e sem biblioteca externa.
[RequireComponent(typeof(CanvasRenderer))]
public class UILineGraphic : MaskableGraphic
{
    private const int CurveSegments = 20;
    private const float DashLength = 18f;

    private readonly List<UILine> _lines = new List<UILine>();
    private readonly List<Vector2> _points = new List<Vector2>();

    public void SetLines(IEnumerable<UILine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
        SetVerticesDirty();
    }

    public void Clear()
    {
        _lines.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (var line in _lines)
            AddLine(vh, line);
    }

    private void AddLine(VertexHelper vh, UILine line)
    {
        _points.Clear();
        for (var i = 0; i <= CurveSegments; i++)
            _points.Add(Bezier(line, i / (float)CurveSegments));

        // A seta ocupa o fim da curva, entao a linha para antes dela.
        var tip = _points[_points.Count - 1];
        var direction = (tip - _points[_points.Count - 2]).normalized;
        if (line.ArrowLength > 0f)
            TrimEnd(line.ArrowLength * 0.8f);

        var traveled = 0f;
        for (var i = 1; i < _points.Count; i++)
        {
            var a = _points[i - 1];
            var b = _points[i];
            var length = Vector2.Distance(a, b);
            if (line.Dashed == false || Mathf.FloorToInt(traveled / DashLength) % 2 == 0)
                AddQuad(vh, a, b, line.Width, line.Color);
            traveled += length;
        }

        if (line.ArrowLength > 0f)
            AddArrow(vh, tip, direction, line.ArrowLength, Mathf.Max(line.Width * 2.2f, line.ArrowLength * 0.7f), line.Color);
    }

    private static Vector2 Bezier(UILine line, float t)
    {
        var u = 1f - t;
        return u * u * line.Start + 2f * u * t * line.Control + t * t * line.End;
    }

    // Recorta o fim da polilinha pelo comprimento pedido.
    private void TrimEnd(float length)
    {
        while (_points.Count > 1 && length > 0f)
        {
            var last = _points[_points.Count - 1];
            var previous = _points[_points.Count - 2];
            var segment = Vector2.Distance(previous, last);
            if (segment > length)
            {
                _points[_points.Count - 1] = Vector2.MoveTowards(last, previous, length);
                return;
            }
            length -= segment;
            _points.RemoveAt(_points.Count - 1);
        }
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        var normal = Vector2.Perpendicular((b - a).normalized) * (width * 0.5f);
        var index = vh.currentVertCount;
        vh.AddVert(a + normal, color, Vector2.zero);
        vh.AddVert(b + normal, color, Vector2.zero);
        vh.AddVert(b - normal, color, Vector2.zero);
        vh.AddVert(a - normal, color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }

    private static void AddArrow(VertexHelper vh, Vector2 tip, Vector2 direction, float length, float width, Color color)
    {
        var back = tip - direction * length;
        var side = Vector2.Perpendicular(direction) * (width * 0.5f);
        var index = vh.currentVertCount;
        vh.AddVert(tip, color, Vector2.zero);
        vh.AddVert(back + side, color, Vector2.zero);
        vh.AddVert(back - side, color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
    }
}
