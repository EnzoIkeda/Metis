using System;
using System.Collections.Generic;

// Trecho reto de luz sobre a rua, em unidades de celula (o centro da celula x, z fica em x, z).
public struct RoadGlowSegment
{
    public float X1;
    public float Z1;
    public float X2;
    public float Z2;

    public RoadGlowSegment(float x1, float z1, float x2, float z2)
    {
        X1 = x1;
        Z1 = z1;
        X2 = x2;
        Z2 = z2;
    }

    public float Length => (float)Math.Sqrt((X2 - X1) * (X2 - X1) + (Z2 - Z1) * (Z2 - Z1));
}

// Luzes das ruas que crescem com a Pesquisa, seguindo os degraus das cartas:
// Cidade Digital acende o meio-fio, Cidade Conectada poe pulsos de dados correndo pelas ruas
// e Smart City acende a faixa central e os cruzamentos. So aparencia: nenhum numero muda.
public static class RoadTechGlow
{
    // Pesquisa minima de cada degrau, a mesma das cartas.
    public const float DigitalPesquisa = 15f;
    public const float ConnectedPesquisa = 30f;
    public const float SmartPesquisa = 45f;

    // Onde a luz chega ao maximo.
    public const float FullPesquisa = 70f;

    // Linha do meio-fio, a partir do centro da celula: ainda no asfalto escuro, onde a luz aparece mais.
    public const float CurbOffset = 0.3f;

    // Brilho do meio-fio: aparece fraco na Cidade Digital e cresce ate o maximo.
    public static float CurbIntensity(float pesquisa)
    {
        if (pesquisa < DigitalPesquisa)
            return 0f;
        return 0.5f + 0.5f * Clamp01((pesquisa - DigitalPesquisa) / (FullPesquisa - DigitalPesquisa));
    }

    // Pulsos de dados: nenhum antes da Cidade Conectada, de 30% do maximo ate o maximo.
    public static int PulseCount(float pesquisa, int max)
    {
        if (pesquisa < ConnectedPesquisa || max <= 0)
            return 0;
        var t = Clamp01((pesquisa - ConnectedPesquisa) / (FullPesquisa - ConnectedPesquisa));
        return Math.Max(1, (int)Math.Round(max * (0.3f + 0.7f * t), MidpointRounding.AwayFromZero));
    }

    // Faixa central e cruzamentos: so na Smart City.
    public static float SmartIntensity(float pesquisa)
    {
        if (pesquisa < SmartPesquisa)
            return 0f;
        return 0.5f + 0.5f * Clamp01((pesquisa - SmartPesquisa) / (FullPesquisa - SmartPesquisa));
    }

    private static readonly RoadSides[] Sides = { RoadSides.North, RoadSides.East, RoadSides.South, RoadSides.West };
    private static readonly int[] Dx = { 0, 1, 0, -1 };
    private static readonly int[] Dz = { 1, 0, -1, 0 };

    // Linhas do meio-fio: um lado sem rua vira uma linha ao longo dele, e cada canto entre
    // dois lados com rua ganha um L pequeno, para a luz contornar a esquina.
    public static List<RoadGlowSegment> CurbSegments(CityPlan plan, float offset = CurbOffset)
    {
        var segments = new List<RoadGlowSegment>();
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var sides = plan.RoadAt(x, z);
                if (sides == RoadSides.None)
                    continue;
                for (var d = 0; d < 4; d++)
                {
                    var previous = (d + 3) % 4;
                    var next = (d + 1) % 4;
                    if ((sides & Sides[d]) == 0)
                    {
                        // Ao longo do lado d, de uma ponta a outra; ponta com rua vai ate a borda.
                        var startReach = (sides & Sides[previous]) != 0 ? 0.5f : offset;
                        var endReach = (sides & Sides[next]) != 0 ? 0.5f : offset;
                        segments.Add(Along(x, z, d, previous, next, offset, startReach, endReach));
                    }
                    else if ((sides & Sides[next]) != 0)
                    {
                        AddCorner(segments, x, z, d, next, offset);
                    }
                }
            }
        }
        return segments;
    }

    // Faixa central: do centro de cada celula ate a borda de cada lado com rua.
    public static List<RoadGlowSegment> CenterSegments(CityPlan plan)
    {
        var segments = new List<RoadGlowSegment>();
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var sides = plan.RoadAt(x, z);
                for (var d = 0; d < 4; d++)
                    if ((sides & Sides[d]) != 0)
                        segments.Add(new RoadGlowSegment(x, z, x + Dx[d] * 0.5f, z + Dz[d] * 0.5f));
            }
        }
        return segments;
    }

    // Contorno de cada cruzamento e T, onde ficariam as faixas de pedestre inteligentes.
    public static List<RoadGlowSegment> CrossingSegments(CityPlan plan, float offset = CurbOffset)
    {
        var segments = new List<RoadGlowSegment>();
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var shape = CityPlan.ShapeOf(plan.RoadAt(x, z), out _);
                if (shape != RoadShape.TJunction && shape != RoadShape.Cross)
                    continue;
                var inner = offset * 0.8f;
                segments.Add(new RoadGlowSegment(x - inner, z - inner, x + inner, z - inner));
                segments.Add(new RoadGlowSegment(x + inner, z - inner, x + inner, z + inner));
                segments.Add(new RoadGlowSegment(x + inner, z + inner, x - inner, z + inner));
                segments.Add(new RoadGlowSegment(x - inner, z + inner, x - inner, z - inner));
            }
        }
        return segments;
    }

    // Linha paralela ao lado d, a "offset" do centro; as pontas vao na direcao dos lados vizinhos.
    private static RoadGlowSegment Along(int x, int z, int d, int previous, int next, float offset, float startReach, float endReach)
    {
        var baseX = x + Dx[d] * offset;
        var baseZ = z + Dz[d] * offset;
        return new RoadGlowSegment(
            baseX + Dx[previous] * startReach, baseZ + Dz[previous] * startReach,
            baseX + Dx[next] * endReach, baseZ + Dz[next] * endReach);
    }

    // L no canto entre os lados a e b, dos dois com rua: sai da borda de um, dobra e vai a borda do outro.
    private static void AddCorner(List<RoadGlowSegment> segments, int x, int z, int a, int b, float offset)
    {
        var cornerX = x + (Dx[a] + Dx[b]) * offset;
        var cornerZ = z + (Dz[a] + Dz[b]) * offset;
        // Ao longo de a, ate a borda: a linha fica a "offset" na direcao de b.
        segments.Add(new RoadGlowSegment(cornerX, cornerZ, x + Dx[b] * offset + Dx[a] * 0.5f, z + Dz[b] * offset + Dz[a] * 0.5f));
        segments.Add(new RoadGlowSegment(cornerX, cornerZ, x + Dx[a] * offset + Dx[b] * 0.5f, z + Dz[a] * offset + Dz[b] * 0.5f));
    }

    private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
