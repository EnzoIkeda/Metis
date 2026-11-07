using System;

// Inclinacao e tamanho do papel num instante da animacao.
public readonly struct NewspaperPose
{
    public NewspaperPose(float scale, float rotation)
    {
        Scale = scale;
        Rotation = rotation;
    }

    public float Scale { get; }
    public float Rotation { get; }
}

// Movimento do jornal: chega girando ao abrir e balanca de leve ao virar a pagina.
public static class NewspaperMotion
{
    public const float OpenDuration = 0.3f;
    public const float PageDuration = 0.18f;

    private const float OpenStartScale = 0.55f;
    private const float OpenStartRotation = -14f;
    private const float PageStartScale = 0.96f;
    private const float PageStartRotation = 2.5f;

    public static float Duration(int direction)
    {
        return direction == 0 ? OpenDuration : PageDuration;
    }

    // direction 0 e a abertura; 1 e -1 viram para a edicao seguinte ou a anterior.
    public static NewspaperPose PoseAt(float elapsed, int direction)
    {
        var duration = Duration(direction);
        var t = Math.Clamp(elapsed / duration, 0f, 1f);
        var remaining = (1f - t) * (1f - t) * (1f - t);
        if (direction == 0)
            return new NewspaperPose(1f + (OpenStartScale - 1f) * remaining, OpenStartRotation * remaining);
        return new NewspaperPose(1f + (PageStartScale - 1f) * remaining, -Math.Sign(direction) * PageStartRotation * remaining);
    }
}
