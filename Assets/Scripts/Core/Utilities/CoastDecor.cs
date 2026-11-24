using System;
using System.Collections.Generic;

public enum CoastDecorKind
{
    Palm,
    Rock,
    Pier,
    Boat,
    Ship,
    Buoy
}

// Uma peca do litoral: X e Z em unidades do grid (centro da celula em numero inteiro), giro em graus no eixo Y.
public struct CoastDecorItem
{
    public CoastDecorKind Kind;
    public float X;
    public float Z;
    public float Rotation;
}

// Onde vao palmeiras, pedras, pier, barcos, navio e boias do litoral. Mesma cidade e semente, mesmo litoral.
public static class CoastDecor
{
    public const double PalmChance = 0.55;
    public const double RockChance = 0.15;
    public const double BoatChance = 0.22;
    public const int BuoyCount = 3;

    public static List<CoastDecorItem> Plan(CityPlan plan, ulong seed)
    {
        var items = new List<CoastDecorItem>();
        if (plan == null || plan.Coast == CoastSide.None)
            return items;

        var random = new SeededRandom(seed ^ 0xC0A57D3C0FFEEUL);
        Inland(plan.Coast, out var inX, out var inZ);
        // Giro que aponta para o mar (o pier e os barcos ancorados olham para fora).
        var seaward = Heading(-inX, -inZ);
        var taken = new HashSet<(int, int)>();

        // Pier onde uma rua chega a praia: na agua logo em frente a ela.
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                if (plan.CoastAt(x, z) != CoastCell.Beach || ReachesBeach(plan, x, z, inX, inZ) == false)
                    continue;
                taken.Add((x, z));
                var wx = x - inX;
                var wz = z - inZ;
                if (plan.IsInBounds(wx, wz) && plan.CoastAt(wx, wz) == CoastCell.Water)
                {
                    items.Add(new CoastDecorItem { Kind = CoastDecorKind.Pier, X = wx, Z = wz, Rotation = seaward });
                    taken.Add((wx, wz));
                }
            }
        }

        // Navio na faixa mais funda, ocupando duas celulas ao longo da costa.
        var outer = OuterWaterCells(plan);
        if (outer.Count >= 3)
        {
            var index = outer.Count / 3 + random.Next(Math.Max(1, outer.Count / 3));
            var (sx, sz) = outer[Math.Min(index, outer.Count - 2)];
            var (nx, nz) = outer[Math.Min(index, outer.Count - 2) + 1];
            if (taken.Contains((sx, sz)) == false && taken.Contains((nx, nz)) == false)
            {
                items.Add(new CoastDecorItem { Kind = CoastDecorKind.Ship, X = (sx + nx) / 2f, Z = (sz + nz) / 2f, Rotation = Heading(nx - sx, nz - sz) });
                taken.Add((sx, sz));
                taken.Add((nx, nz));
            }
        }

        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var cell = plan.CoastAt(x, z);
                if (cell == CoastCell.None || taken.Contains((x, z)))
                    continue;
                if (cell == CoastCell.Beach)
                {
                    if (random.NextDouble() < PalmChance)
                        items.Add(Scattered(CoastDecorKind.Palm, x, z, random, 0.3f));
                    if (random.NextDouble() < RockChance)
                        items.Add(Scattered(CoastDecorKind.Rock, x, z, random, 0.3f));
                }
                else if (random.NextDouble() < BoatChance)
                {
                    items.Add(Scattered(CoastDecorKind.Boat, x, z, random, 0.2f));
                    taken.Add((x, z));
                }
            }
        }

        // Boias na agua junto a praia, em celulas ainda livres.
        var inner = new List<(int X, int Z)>();
        for (var x = 0; x < plan.Width; x++)
            for (var z = 0; z < plan.Height; z++)
                if (plan.CoastAt(x, z) == CoastCell.Water && taken.Contains((x, z)) == false && IsBesideBeach(plan, x, z, inX, inZ))
                    inner.Add((x, z));
        for (var i = 0; i < BuoyCount && inner.Count > 0; i++)
        {
            var pick = random.Next(inner.Count);
            var (bx, bz) = inner[pick];
            inner.RemoveAt(pick);
            items.Add(Scattered(CoastDecorKind.Buoy, bx, bz, random, 0.3f));
        }
        return items;
    }

    // Direcao do mar para a terra.
    public static void Inland(CoastSide side, out int dx, out int dz)
    {
        dx = side == CoastSide.West ? 1 : side == CoastSide.East ? -1 : 0;
        dz = side == CoastSide.South ? 1 : side == CoastSide.North ? -1 : 0;
    }

    private static bool ReachesBeach(CityPlan plan, int x, int z, int inX, int inZ)
    {
        var rx = x + inX;
        var rz = z + inZ;
        if (plan.IsInBounds(rx, rz) == false || plan.IsRoad(rx, rz) == false)
            return false;
        var toBeach = inZ == 1 ? RoadSides.South : inZ == -1 ? RoadSides.North : inX == 1 ? RoadSides.West : RoadSides.East;
        return (plan.RoadAt(rx, rz) & toBeach) != 0;
    }

    private static bool IsBesideBeach(CityPlan plan, int x, int z, int inX, int inZ)
    {
        var bx = x + inX;
        var bz = z + inZ;
        return plan.IsInBounds(bx, bz) && plan.CoastAt(bx, bz) == CoastCell.Beach;
    }

    // Celulas de agua na borda do grid, na ordem ao longo da costa.
    private static List<(int X, int Z)> OuterWaterCells(CityPlan plan)
    {
        var cells = new List<(int X, int Z)>();
        switch (plan.Coast)
        {
            case CoastSide.South:
                for (var x = 0; x < plan.Width; x++)
                    cells.Add((x, 0));
                break;
            case CoastSide.North:
                for (var x = 0; x < plan.Width; x++)
                    cells.Add((x, plan.Height - 1));
                break;
            case CoastSide.West:
                for (var z = 0; z < plan.Height; z++)
                    cells.Add((0, z));
                break;
            case CoastSide.East:
                for (var z = 0; z < plan.Height; z++)
                    cells.Add((plan.Width - 1, z));
                break;
        }
        cells.RemoveAll(cell => plan.CoastAt(cell.X, cell.Z) != CoastCell.Water);
        return cells;
    }

    private static CoastDecorItem Scattered(CoastDecorKind kind, int x, int z, SeededRandom random, float spread)
    {
        return new CoastDecorItem
        {
            Kind = kind,
            X = x + (float)(random.NextDouble() * 2 - 1) * spread,
            Z = z + (float)(random.NextDouble() * 2 - 1) * spread,
            Rotation = random.Next(24) * 15f
        };
    }

    // Giro no eixo Y (0 olha para o norte, 90 para o leste) da direcao dada.
    private static float Heading(float dx, float dz)
    {
        var degrees = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
        return degrees < 0f ? degrees + 360f : degrees;
    }
}
