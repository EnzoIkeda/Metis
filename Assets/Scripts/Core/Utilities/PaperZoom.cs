using System;

// Zoom e arrasto de uma folha dentro de uma janela do mesmo tamanho.
// Coordenadas a partir do canto superior esquerdo da janela, x pra direita e y pra cima (dentro da janela, y e negativo).
public class PaperZoom
{
    public float Width { get; private set; }
    public float Height { get; private set; }
    public float MinScale { get; }
    public float MaxScale { get; }
    public float Scale { get; private set; } = 1f;

    // Posicao do canto superior esquerdo da folha dentro da janela.
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }

    public bool IsZoomed => Scale > MinScale + 0.0001f;

    public PaperZoom(float minScale = 1f, float maxScale = 3f)
    {
        MinScale = Math.Max(0.01f, minScale);
        MaxScale = Math.Max(MinScale, maxScale);
        Scale = MinScale;
    }

    public void Resize(float width, float height)
    {
        Width = Math.Max(0f, width);
        Height = Math.Max(0f, height);
        Clamp();
    }

    public void Reset()
    {
        Scale = MinScale;
        OffsetX = 0f;
        OffsetY = 0f;
        Clamp();
    }

    // Multiplica o zoom mantendo parado o ponto da folha que esta embaixo de (x, y).
    public void ZoomAround(float x, float y, float factor)
    {
        if (factor <= 0f || float.IsNaN(factor) || float.IsInfinity(factor))
            return;
        var next = Math.Max(MinScale, Math.Min(MaxScale, Scale * factor));
        var pageX = (x - OffsetX) / Scale;
        var pageY = (y - OffsetY) / Scale;
        Scale = next;
        OffsetX = x - pageX * Scale;
        OffsetY = y - pageY * Scale;
        Clamp();
    }

    public void Pan(float dx, float dy)
    {
        OffsetX += dx;
        OffsetY += dy;
        Clamp();
    }

    // A folha nunca deixa faixa vazia na janela.
    private void Clamp()
    {
        OffsetX = Math.Max(Width - Width * Scale, Math.Min(0f, OffsetX));
        OffsetY = Math.Max(0f, Math.Min(Height * Scale - Height, OffsetY));
    }
}
