using System;

// Curva de volume da fusao entre faixas, em tempo real.
public static class MusicFade
{
    public const float DefaultDuration = 1.2f;

    // Volume depois de elapsed segundos indo de from para to; duracao zero ou negativa vai direto ao fim.
    public static float VolumeAt(float elapsed, float duration, float from, float to)
    {
        if (duration <= 0f || float.IsNaN(elapsed))
            return to;
        var t = Math.Clamp(elapsed / duration, 0f, 1f);
        // Suave no comeco e no fim, para a troca nao dar tranco.
        var eased = t * t * (3f - 2f * t);
        return from + (to - from) * eased;
    }

    public static bool IsDone(float elapsed, float duration)
    {
        return duration <= 0f || elapsed >= duration;
    }
}
