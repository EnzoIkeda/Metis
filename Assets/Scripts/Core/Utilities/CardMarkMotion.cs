using System;

// Como a peca nova chega: cai do alto acelerando, achata ao bater no chao e volta ao tamanho.
// A peca antiga afunda e encolhe antes. Tempos em segundos desde o comeco de cada peca.
public static class CardMarkMotion
{
    public const float StartHeight = 6f;
    public const float DropDuration = 0.45f;
    public const float SettleDuration = 0.25f;
    public const float SinkDuration = 0.25f;

    // Atraso entre as pecas de uma mesma marca, para a queda correr pelos lotes.
    public const float Stagger = 0.12f;

    public const float Squash = 0.3f;
    public const float Spread = 0.15f;

    public static float Total => DropDuration + SettleDuration;

    public static bool HasLanded(float time) => time >= DropDuration;

    // Altura acima do lugar final.
    public static float Height(float time)
    {
        if (time <= 0f)
            return StartHeight;
        if (time >= DropDuration)
            return 0f;
        var t = time / DropDuration;
        return StartHeight * (1f - t * t);
    }

    // Escala sobre a original: (largura, altura). Achata so depois de pousar.
    public static (float Horizontal, float Vertical) Scale(float time)
    {
        if (time < DropDuration || time >= Total)
            return (1f, 1f);
        var wave = (float)Math.Sin(Math.PI * (time - DropDuration) / SettleDuration);
        return (1f + Spread * wave, 1f - Squash * wave);
    }

    // Escala da peca que sai, de 1 a 0.
    public static float SinkScale(float time)
    {
        if (time <= 0f)
            return 1f;
        if (time >= SinkDuration)
            return 0f;
        return 1f - time / SinkDuration;
    }

    // Quando a peca de indice i comeca a cair; a antiga do mesmo lugar afunda antes, ja no comeco.
    public static float StartOf(int index) => SinkDuration + index * Stagger;

    public static float Duration(int pieces) => pieces <= 0 ? 0f : StartOf(pieces - 1) + Total;
}
