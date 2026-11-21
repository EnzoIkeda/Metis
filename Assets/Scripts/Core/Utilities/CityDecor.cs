using System.Collections.Generic;

public enum CityDecorKind
{
    StreetLight,
    TrafficSignal
}

// Peca de decoracao da rua: celula, deslocamento dentro dela (em fracao de celula) e rotacao no eixo Y.
public struct CityDecorItem
{
    public CityDecorKind Kind;
    public int X;
    public int Z;
    public float OffsetX;
    public float OffsetZ;
    public float Rotation;
}

// Decoracao fora do colapso, tirada so do plano: postes nas calcadas das retas, um a cada duas celulas
// e alternando o lado, e um semaforo num canto de cada T e cruzamento.
public static class CityDecor
{
    // Distancia do centro da celula ate a calcada.
    public const float SidewalkOffset = 0.42f;

    private static readonly int[] Dx = { 0, 1, 0, -1 };
    private static readonly int[] Dz = { 1, 0, -1, 0 };

    public static List<CityDecorItem> Plan(CityPlan plan)
    {
        var items = new List<CityDecorItem>();
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var sides = plan.RoadAt(x, z);
                var shape = CityPlan.ShapeOf(sides, out var rotation);
                if (shape == RoadShape.Straight)
                    AddStreetLight(items, x, z, rotation);
                else if (shape == RoadShape.TJunction || shape == RoadShape.Cross)
                    AddTrafficSignal(items, plan, x, z, sides);
            }
        }
        return items;
    }

    private static void AddStreetLight(List<CityDecorItem> items, int x, int z, float rotation)
    {
        var vertical = rotation == 0f;
        var along = vertical ? z : x;
        if (along % 2 != 0)
            return;

        // Alterna o lado a cada poste, para a rua nao ficar com todos do mesmo lado.
        var side = (along / 2 + (vertical ? x : z)) % 2 == 0 ? 1f : -1f;
        items.Add(new CityDecorItem
        {
            Kind = CityDecorKind.StreetLight,
            X = x,
            Z = z,
            OffsetX = vertical ? side * SidewalkOffset : 0f,
            OffsetZ = vertical ? 0f : side * SidewalkOffset,
            // O poste olha para a rua.
            Rotation = vertical ? (side > 0 ? 270f : 90f) : (side > 0 ? 180f : 0f)
        });
    }

    // Semaforo no canto entre dois lados ligados, preferindo um canto que encoste num lote.
    private static void AddTrafficSignal(List<CityDecorItem> items, CityPlan plan, int x, int z, RoadSides sides)
    {
        var chosen = -1;
        for (var d = 0; d < 4; d++)
        {
            var next = (d + 1) % 4;
            if ((sides & (RoadSides)(1 << d)) == 0 || (sides & (RoadSides)(1 << next)) == 0)
                continue;
            var cornerX = x + Dx[d] + Dx[next];
            var cornerZ = z + Dz[d] + Dz[next];
            if (chosen < 0)
                chosen = d;
            if (plan.IsInBounds(cornerX, cornerZ) && plan.IsRoad(cornerX, cornerZ) == false)
            {
                chosen = d;
                break;
            }
        }
        if (chosen < 0)
            return;

        var second = (chosen + 1) % 4;
        items.Add(new CityDecorItem
        {
            Kind = CityDecorKind.TrafficSignal,
            X = x,
            Z = z,
            OffsetX = (Dx[chosen] + Dx[second]) * SidewalkOffset,
            OffsetZ = (Dz[chosen] + Dz[second]) * SidewalkOffset,
            Rotation = chosen * 90f
        });
    }
}
