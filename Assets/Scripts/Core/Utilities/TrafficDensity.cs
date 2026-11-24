using System;

// Quantos atores circulam e em que ritmo, a partir dos indices de 0 a 100 da cidade.
// Populacao da a quantidade; Mobilidade da a velocidade e puxa mais gente a pe.
public static class TrafficDensity
{
    // Abaixo disso a cidade anda devagar; acima, na velocidade normal.
    public const float FluidMobility = 60f;
    public const float SlowestSpeedFactor = 0.5f;
    public const float MaxPedestrianBonus = 0.3f;

    public static int ActorCount(float population, int min, int max)
    {
        if (max < min)
            max = min;
        var t = Clamp01(population / 100f);
        return (int)Math.Round(min + (max - min) * t, MidpointRounding.AwayFromZero);
    }

    // Fator sobre a velocidade sorteada de cada carro: de 0,5 com Mobilidade 0 a 1 a partir de 60.
    public static float SpeedFactor(float mobility)
    {
        var t = Clamp01(mobility / FluidMobility);
        return SlowestSpeedFactor + (1f - SlowestSpeedFactor) * t;
    }

    // Pedestres a mais quando a Mobilidade passa do ponto fluido, ate 30% com 100.
    public static int PedestrianCount(float population, float mobility, int min, int max)
    {
        var bonus = 1f + MaxPedestrianBonus * Clamp01((mobility - FluidMobility) / (100f - FluidMobility));
        var count = ActorCount(population, min, max) * bonus;
        return Math.Min((int)Math.Round(count, MidpointRounding.AwayFromZero), (int)Math.Round(max * (1f + MaxPedestrianBonus)));
    }

    private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
