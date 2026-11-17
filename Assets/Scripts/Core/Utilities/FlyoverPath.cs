using System;

// Posicao e tamanho ortografico da camera num instante do sobrevoo.
public readonly struct CameraPose
{
    public CameraPose(float x, float y, float z, float size)
    {
        X = x;
        Y = y;
        Z = z;
        Size = size;
    }

    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float Size { get; }
}

// Sobrevoo de abertura: parte de perto e termina exatamente no enquadramento de repouso da cena.
public class FlyoverPath
{
    public const float DefaultDuration = 4f;
    public const float DefaultStartSize = 7f;

    private readonly CameraPose _start;
    private readonly CameraPose _end;

    public FlyoverPath(CameraPose start, CameraPose end, float duration = DefaultDuration)
    {
        _start = start;
        _end = end;
        Duration = Math.Max(duration, 0f);
    }

    public float Duration { get; }
    public CameraPose Start => _start;
    public CameraPose End => _end;

    public bool IsFinished(float elapsed)
    {
        return Duration <= 0f || elapsed >= Duration;
    }

    public CameraPose PoseAtTime(float elapsed)
    {
        return Duration <= 0f ? _end : PoseAt(elapsed / Duration);
    }

    // t em [0, 1]; fora disso fica no comeco ou no fim.
    public CameraPose PoseAt(float t)
    {
        if (float.IsNaN(t) || t <= 0f)
            return _start;
        if (t >= 1f)
            return _end;

        // Sai devagar e pousa devagar.
        var eased = t * t * (3f - 2f * t);
        return new CameraPose(
            Lerp(_start.X, _end.X, eased),
            Lerp(_start.Y, _end.Y, eased),
            Lerp(_start.Z, _end.Z, eased),
            SizeAt(eased));
    }

    // O zoom e percebido em proporcao, entao o tamanho cresce em razao constante, e nao em passos iguais.
    private float SizeAt(float eased)
    {
        if (_start.Size > 0f && _end.Size > 0f)
            return (float)(_start.Size * Math.Pow(_end.Size / _start.Size, eased));
        return Lerp(_start.Size, _end.Size, eased);
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + (to - from) * t;
    }
}
