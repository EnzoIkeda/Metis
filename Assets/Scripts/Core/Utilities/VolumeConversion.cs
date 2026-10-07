using System;

// Converte volume linear (0 a 1) em decibeis pro mixer, com piso de silencio.
public static class VolumeConversion
{
    public const float SilenceDecibels = -80f;

    public static float LinearToDecibels(float linear)
    {
        if (float.IsNaN(linear) || linear <= 0f)
            return SilenceDecibels;
        if (linear > 1f)
            linear = 1f;

        var decibels = 20f * (float)Math.Log10(linear);
        return decibels < SilenceDecibels ? SilenceDecibels : decibels;
    }
}
