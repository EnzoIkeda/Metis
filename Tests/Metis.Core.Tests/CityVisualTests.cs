using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Metis.Core.Tests;

public class CityWfcTests
{
    private static readonly int[] Dx = { 0, 1, 0, -1 };
    private static readonly int[] Dz = { 1, 0, -1, 0 };

    private static RoadSides[,] RoadsOf(CityPlan plan)
    {
        var roads = new RoadSides[plan.Width, plan.Height];
        for (var x = 0; x < plan.Width; x++)
            for (var z = 0; z < plan.Height; z++)
                roads[x, z] = plan.RoadAt(x, z);
        return roads;
    }

    [Test]
    public void SameSeed_GivesSamePlan()
    {
        var a = CityWfc.Generate(1234);
        var b = CityWfc.Generate(1234);

        for (var x = 0; x < a.Width; x++)
        {
            for (var z = 0; z < a.Height; z++)
            {
                Assert.That(b.RoadAt(x, z), Is.EqualTo(a.RoadAt(x, z)));
                Assert.That(b.LotUseAt(x, z), Is.EqualTo(a.LotUseAt(x, z)));
                Assert.That(b.FacingAt(x, z), Is.EqualTo(a.FacingAt(x, z)));
            }
        }
    }

    [Test]
    public void DifferentSeeds_GiveDifferentStreets()
    {
        var a = CityWfc.Generate(1);
        var b = CityWfc.Generate(2);
        var differs = false;
        for (var x = 0; x < a.Width; x++)
            for (var z = 0; z < a.Height; z++)
                differs |= a.RoadAt(x, z) != b.RoadAt(x, z);
        Assert.That(differs, Is.True);
    }

    [Test]
    public void ManySeeds_AllPassTheGlobalRules_WithoutFallback()
    {
        var options = new CityWfcOptions();
        var fallbacks = 0;
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var plan = CityWfc.Generate(seed * 7919UL, options);
            if (plan.UsedFallback)
                fallbacks++;
            Assert.That(CityWfc.PassesGlobalRules(RoadsOf(plan), options), Is.True, $"semente {seed}");
        }
        Assert.That(fallbacks, Is.EqualTo(0));
    }

    [Test]
    public void NeighbourSides_AlwaysMatch_AndRoadsNeverTouchUnconnected()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var plan = CityWfc.Generate(seed);
            for (var x = 0; x < plan.Width; x++)
            {
                for (var z = 0; z < plan.Height; z++)
                {
                    for (var d = 0; d < 4; d++)
                    {
                        var nx = x + Dx[d];
                        var nz = z + Dz[d];
                        if (plan.IsInBounds(nx, nz) == false)
                            continue;
                        var open = (plan.RoadAt(x, z) & (RoadSides)(1 << d)) != 0;
                        var otherOpen = (plan.RoadAt(nx, nz) & (RoadSides)(1 << ((d + 2) % 4))) != 0;
                        Assert.That(otherOpen, Is.EqualTo(open), $"semente {seed}, celula {x},{z}, lado {d}");
                        if (plan.IsRoad(x, z) && plan.IsRoad(nx, nz))
                            Assert.That(open, Is.True, $"ruas coladas sem ligacao na semente {seed}");
                    }
                }
            }
        }
    }

    [Test]
    public void ParallelStreets_KeepAtLeastTwoLotsBetweenThem()
    {
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var plan = CityWfc.Generate(seed);
            for (var x = 0; x < plan.Width; x++)
            {
                for (var z = 0; z < plan.Height; z++)
                {
                    if (plan.IsRoad(x, z))
                        continue;
                    var northSouth = z > 0 && z < plan.Height - 1 && plan.IsRoad(x, z - 1) && plan.IsRoad(x, z + 1);
                    var eastWest = x > 0 && x < plan.Width - 1 && plan.IsRoad(x - 1, z) && plan.IsRoad(x + 1, z);
                    Assert.That(northSouth || eastWest, Is.False, $"lote {x},{z} entre duas ruas na semente {seed}");
                }
            }
        }
    }

    [Test]
    public void Plans_HaveNoDeadEndsInside_AndNoTwoByTwoRoadSquares()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var plan = CityWfc.Generate(seed);
            for (var x = 0; x < plan.Width; x++)
            {
                for (var z = 0; z < plan.Height; z++)
                {
                    Assert.That(CityPlan.ShapeOf(plan.RoadAt(x, z), out _), Is.Not.EqualTo(RoadShape.DeadEnd));
                    if (x + 1 < plan.Width && z + 1 < plan.Height)
                        Assert.That(plan.IsRoad(x, z) && plan.IsRoad(x + 1, z) && plan.IsRoad(x, z + 1) && plan.IsRoad(x + 1, z + 1), Is.False);
                }
            }
        }
    }

    [Test]
    public void Lots_FarFromTheRoadAreGreen_AndTowersFaceTheRoad_AndNeverTouchHouses()
    {
        var options = new CityWfcOptions();
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var plan = CityWfc.Generate(seed, options);
            var distance = CityWfc.DistanceToRoad(RoadsOf(plan));
            for (var x = 0; x < plan.Width; x++)
            {
                for (var z = 0; z < plan.Height; z++)
                {
                    if (plan.IsRoad(x, z))
                        continue;
                    var use = plan.LotUseAt(x, z);
                    if (distance[x, z] > options.MaxDistanceToRoad)
                        Assert.That(use, Is.EqualTo(CityLotUse.Green));
                    if (use != CityLotUse.Tower)
                        continue;
                    Assert.That(distance[x, z], Is.EqualTo(1));
                    for (var d = 0; d < 4; d++)
                    {
                        var nx = x + Dx[d];
                        var nz = z + Dz[d];
                        if (plan.IsInBounds(nx, nz) && plan.IsRoad(nx, nz) == false)
                            Assert.That(plan.LotUseAt(nx, nz), Is.Not.EqualTo(CityLotUse.House));
                    }
                }
            }
        }
    }

    [Test]
    public void Facing_PointsToAnAdjacentRoad_WhenThereIsOne()
    {
        var plan = CityWfc.Generate(77);
        float[] degrees = { 0f, 90f, 180f, 270f };
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                if (plan.IsRoad(x, z))
                    continue;
                var adjacent = new List<float>();
                for (var d = 0; d < 4; d++)
                    if (plan.IsInBounds(x + Dx[d], z + Dz[d]) && plan.IsRoad(x + Dx[d], z + Dz[d]))
                        adjacent.Add(degrees[d]);
                if (adjacent.Count > 0)
                    Assert.That(adjacent, Has.Member(plan.FacingAt(x, z)));
            }
        }
    }

    [Test]
    public void Fallback_IsTheOriginalGrid_AndPassesTheRules()
    {
        var roads = CityWfc.FallbackRoads(15, 15);
        var count = 0;
        for (var x = 0; x < 15; x++)
            for (var z = 0; z < 15; z++)
                if (roads[x, z] != RoadSides.None)
                    count++;

        Assert.That(count, Is.EqualTo(81));
        Assert.That(CityPlan.ShapeOf(roads[3, 3], out _), Is.EqualTo(RoadShape.Cross));
        Assert.That(CityPlan.ShapeOf(roads[3, 0], out var rotation), Is.EqualTo(RoadShape.Straight));
        Assert.That(rotation, Is.EqualTo(0f));
        Assert.That(CityWfc.CountReachable(roads, (3, 0)), Is.EqualTo(81));
        Assert.That(CityWfc.CountBorderExits(roads), Is.EqualTo(12));
    }

    [Test]
    public void NoAttemptsLeft_UsesTheFallback()
    {
        var options = new CityWfcOptions { MaxAttempts = 0 };
        var plan = CityWfc.Generate(5, options);

        Assert.That(plan.UsedFallback, Is.True);
        Assert.That(plan.RoadCount, Is.EqualTo(81));
    }

    [Test]
    public void KeepLargestNetwork_RemovesLooseStreets()
    {
        var roads = new RoadSides[5, 5];
        // Rua de 3 celulas no alto, ligada pelas bordas, e uma de 2 celulas embaixo.
        roads[0, 4] = RoadSides.East | RoadSides.West;
        roads[1, 4] = RoadSides.East | RoadSides.West;
        roads[2, 4] = RoadSides.East | RoadSides.West;
        roads[0, 0] = RoadSides.East | RoadSides.West;
        roads[1, 0] = RoadSides.East | RoadSides.West;

        CityWfc.KeepLargestNetwork(roads);

        Assert.That(roads[1, 4], Is.Not.EqualTo(RoadSides.None));
        Assert.That(roads[0, 0], Is.EqualTo(RoadSides.None));
        Assert.That(roads[1, 0], Is.EqualTo(RoadSides.None));
    }

    [TestCase(RoadSides.North | RoadSides.South, RoadShape.Straight, 0f)]
    [TestCase(RoadSides.East | RoadSides.West, RoadShape.Straight, 90f)]
    [TestCase(RoadSides.North | RoadSides.East, RoadShape.Corner, 0f)]
    [TestCase(RoadSides.East | RoadSides.South, RoadShape.Corner, 90f)]
    [TestCase(RoadSides.South | RoadSides.West, RoadShape.Corner, 180f)]
    [TestCase(RoadSides.West | RoadSides.North, RoadShape.Corner, 270f)]
    [TestCase(RoadSides.North | RoadSides.East | RoadSides.South, RoadShape.TJunction, 0f)]
    [TestCase(RoadSides.East | RoadSides.South | RoadSides.West, RoadShape.TJunction, 90f)]
    [TestCase(RoadSides.South | RoadSides.West | RoadSides.North, RoadShape.TJunction, 180f)]
    [TestCase(RoadSides.West | RoadSides.North | RoadSides.East, RoadShape.TJunction, 270f)]
    [TestCase(RoadSides.All, RoadShape.Cross, 0f)]
    [TestCase(RoadSides.East, RoadShape.DeadEnd, 90f)]
    [TestCase(RoadSides.None, RoadShape.None, 0f)]
    public void ShapeOf_GivesShapeAndClockwiseRotation(RoadSides sides, RoadShape shape, float rotation)
    {
        Assert.That(CityPlan.ShapeOf(sides, out var actual), Is.EqualTo(shape));
        Assert.That(actual, Is.EqualTo(rotation));
    }

    [Test]
    public void Generation_IsFastEnough()
    {
        var watch = Stopwatch.StartNew();
        for (ulong seed = 1; seed <= 200; seed++)
            CityWfc.Generate(seed);
        watch.Stop();

        // Folga larga para maquina lenta de CI; no PC fica perto de 3 ms por cidade.
        Assert.That(watch.Elapsed.TotalMilliseconds / 200, Is.LessThan(40));
    }
}

public class TrafficDensityTests
{
    [Test]
    public void ActorCount_GoesFromMinToMax()
    {
        Assert.That(TrafficDensity.ActorCount(0f, 4, 14), Is.EqualTo(4));
        Assert.That(TrafficDensity.ActorCount(50f, 4, 14), Is.EqualTo(9));
        Assert.That(TrafficDensity.ActorCount(100f, 4, 14), Is.EqualTo(14));
        Assert.That(TrafficDensity.ActorCount(150f, 4, 14), Is.EqualTo(14));
        Assert.That(TrafficDensity.ActorCount(-10f, 4, 14), Is.EqualTo(4));
    }

    [Test]
    public void ActorCount_IsMonotonic()
    {
        var last = 0;
        for (var population = 0; population <= 100; population++)
        {
            var count = TrafficDensity.ActorCount(population, 4, 14);
            Assert.That(count, Is.GreaterThanOrEqualTo(last));
            last = count;
        }
    }

    [Test]
    public void SpeedFactor_SlowsDownBelowFluidMobility()
    {
        Assert.That(TrafficDensity.SpeedFactor(0f), Is.EqualTo(0.5f));
        Assert.That(TrafficDensity.SpeedFactor(30f), Is.EqualTo(0.75f).Within(1e-5));
        Assert.That(TrafficDensity.SpeedFactor(60f), Is.EqualTo(1f));
        Assert.That(TrafficDensity.SpeedFactor(100f), Is.EqualTo(1f));
    }

    [Test]
    public void PedestrianCount_GainsUpTo30PercentWithHighMobility()
    {
        Assert.That(TrafficDensity.PedestrianCount(100f, 60f, 4, 18), Is.EqualTo(18));
        Assert.That(TrafficDensity.PedestrianCount(100f, 100f, 4, 18), Is.EqualTo(23));
        Assert.That(TrafficDensity.PedestrianCount(0f, 100f, 4, 18), Is.EqualTo(5));
    }
}

public class CardMarkPlannerTests
{
    private static readonly CardMarkRule Park = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, ResultIsGreen = true };
    private static readonly CardMarkRule Factory = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Placement = CardMarkPlacement.Edge };
    private static readonly CardMarkRule Solar = new CardMarkRule { Kind = CardMarkKind.RoofProp };
    private static readonly CardMarkRule BusStop = new CardMarkRule { Kind = CardMarkKind.StreetProp, Placement = CardMarkPlacement.NearCrossing };

    private static List<(string Card, CardMarkRule Rule, int Turn)> Plays()
    {
        return new List<(string, CardMarkRule, int)>
        {
            ("CardParqueUrbano", Park, 1),
            ("CardRedeSolarDistribuida", Solar, 2),
            ("CardLinhaDeOnibus", BusStop, 3),
            ("CardUsinaTermica", Factory, 4),
            ("CardParqueUrbano", Park, 5),
            ("CardRedeSolarDistribuida", Solar, 6),
        };
    }

    [Test]
    public void ReplayingThePlays_GivesTheSameMarks()
    {
        var plan = CityWfc.Generate(99);
        var first = new CardMarkPlanner(plan, 99);
        var second = new CardMarkPlanner(plan, 99);
        foreach (var play in Plays())
            first.TryMark(play.Card, play.Rule, play.Turn, out _);
        foreach (var play in Plays())
            second.TryMark(play.Card, play.Rule, play.Turn, out _);

        Assert.That(second.Marks.Count, Is.EqualTo(first.Marks.Count));
        for (var i = 0; i < first.Marks.Count; i++)
        {
            Assert.That(second.Marks[i].X, Is.EqualTo(first.Marks[i].X));
            Assert.That(second.Marks[i].Z, Is.EqualTo(first.Marks[i].Z));
            Assert.That(second.Marks[i].Side, Is.EqualTo(first.Marks[i].Side));
        }
    }

    [Test]
    public void Marks_LandOnTheRightKindOfCell()
    {
        var plan = CityWfc.Generate(99);
        var planner = new CardMarkPlanner(plan, 99);
        foreach (var play in Plays())
            Assert.That(planner.TryMark(play.Card, play.Rule, play.Turn, out _), Is.True);

        foreach (var mark in planner.Marks)
        {
            if (mark.Kind == CardMarkKind.StreetProp)
            {
                Assert.That(CityPlan.ShapeOf(plan.RoadAt(mark.X, mark.Z), out _), Is.EqualTo(RoadShape.Straight));
                Assert.That(mark.Side, Is.InRange(0, 3));
                Assert.That((plan.RoadAt(mark.X, mark.Z) & (RoadSides)(1 << mark.Side)), Is.EqualTo(RoadSides.None));
            }
            else
            {
                Assert.That(plan.IsRoad(mark.X, mark.Z), Is.False);
                Assert.That(mark.Side, Is.EqualTo(-1));
            }
        }
    }

    [Test]
    public void SameCard_StopsAtTheLimit()
    {
        var plan = CityWfc.Generate(3);
        var planner = new CardMarkPlanner(plan, 3);
        var rule = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Limit = 2 };

        Assert.That(planner.TryMark("CardZonaComercial", rule, 1, out _), Is.True);
        Assert.That(planner.TryMark("CardZonaComercial", rule, 2, out _), Is.True);
        Assert.That(planner.TryMark("CardZonaComercial", rule, 3, out _), Is.False);
        Assert.That(planner.TryMark("CardConjuntoHabitacional", rule, 4, out _), Is.True);
    }

    [Test]
    public void DefaultLimit_IsThree()
    {
        var plan = CityWfc.Generate(3);
        var planner = new CardMarkPlanner(plan, 3);
        var marked = 0;
        for (var turn = 1; turn <= 6; turn++)
            if (planner.TryMark("CardParqueUrbano", Park, turn, out _))
                marked++;
        Assert.That(marked, Is.EqualTo(CardMarkPlanner.DefaultLimit));
    }

    [Test]
    public void TwoMarks_NeverShareAPlace()
    {
        var plan = CityWfc.Generate(11);
        var planner = new CardMarkPlanner(plan, 11);
        var rule = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Limit = 1000 };
        var roof = new CardMarkRule { Kind = CardMarkKind.RoofProp, Limit = 1000 };
        for (var turn = 1; turn <= 60; turn++)
        {
            planner.TryMark("CardParqueUrbano", rule, turn, out _);
            planner.TryMark("CardRedeSolarDistribuida", roof, turn, out _);
        }

        // Predio novo de uma troca pode ganhar peca no telhado depois; o contrario nao.
        var replaced = new HashSet<(int, int)>();
        var roofs = new HashSet<(int, int)>();
        foreach (var mark in planner.Marks)
        {
            foreach (var spot in mark.Spots)
            {
                if (mark.Kind == CardMarkKind.ReplaceLot)
                {
                    Assert.That(replaced.Add((spot.X, spot.Z)), Is.True, $"troca repetida em {spot.X},{spot.Z}");
                    Assert.That(roofs.Contains((spot.X, spot.Z)), Is.False, $"troca sobre telhado marcado em {spot.X},{spot.Z}");
                }
                else
                {
                    Assert.That(roofs.Add((spot.X, spot.Z)), Is.True, $"telhado repetido em {spot.X},{spot.Z}");
                }
            }
        }
    }

    [Test]
    public void RoofProp_SkipsLotsThatBecameGreen()
    {
        var plan = CityWfc.Generate(21);
        var planner = new CardMarkPlanner(plan, 21);
        var park = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, ResultIsGreen = true, Limit = 1000 };
        var roof = new CardMarkRule { Kind = CardMarkKind.RoofProp, Limit = 1000 };
        for (var turn = 1; turn <= 40; turn++)
            planner.TryMark("CardParqueUrbano", park, turn, out _);
        for (var turn = 41; turn <= 200; turn++)
            planner.TryMark("CardRedeSolarDistribuida", roof, turn, out _);

        foreach (var mark in planner.Marks)
        {
            if (mark.Kind != CardMarkKind.RoofProp)
                continue;
            Assert.That(planner.IsReplaced(mark.X, mark.Z, out var green) && green, Is.False);
            Assert.That(plan.LotUseAt(mark.X, mark.Z), Is.Not.EqualTo(CityLotUse.Green));
        }
    }

    [Test]
    public void RunningOutOfPlaces_ReturnsFalse()
    {
        var plan = CityWfc.Generate(8);
        var planner = new CardMarkPlanner(plan, 8);
        var rule = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Limit = 100000 };
        var lots = plan.Width * plan.Height - plan.RoadCount;
        for (var turn = 1; turn <= lots; turn++)
            Assert.That(planner.TryMark("CardParqueUrbano", rule, turn, out _), Is.True);

        Assert.That(planner.TryMark("CardParqueUrbano", rule, lots + 1, out _), Is.False);
    }

    [Test]
    public void NoneKind_NeverMarks()
    {
        var planner = new CardMarkPlanner(CityWfc.Generate(1), 1);
        Assert.That(planner.TryMark("CardAumentoDeImpostos", new CardMarkRule { Kind = CardMarkKind.None }, 1, out _), Is.False);
        Assert.That(planner.Marks.Count, Is.EqualTo(0));
    }

    [Test]
    public void EdgePlacement_PrefersTheBorder()
    {
        var plan = CityWfc.Generate(5);
        var planner = new CardMarkPlanner(plan, 5);
        for (var turn = 1; turn <= 3; turn++)
            planner.TryMark("CardUsinaTermica", Factory, turn, out _);

        foreach (var mark in planner.Marks)
            Assert.That(mark.X <= 1 || mark.Z <= 1 || mark.X >= plan.Width - 2 || mark.Z >= plan.Height - 2, Is.True);
    }
}

public class CardMarkSizeTests
{
    private static readonly CardMarkRule Commerce = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Size = 4, Limit = 1000 };
    private static readonly CardMarkRule Stadium = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Merge = true, Limit = 1000 };
    private static readonly CardMarkRule Solar = new CardMarkRule { Kind = CardMarkKind.RoofProp, Size = 4, Limit = 1000 };
    private static readonly CardMarkRule Bins = new CardMarkRule { Kind = CardMarkKind.StreetProp, Size = 4, Limit = 1000 };

    [Test]
    public void FirstSpot_IsTheMarkAnchor()
    {
        var planner = new CardMarkPlanner(CityWfc.Generate(4), 4);
        foreach (var rule in new[] { Commerce, Stadium, Solar, Bins })
        {
            Assert.That(planner.TryMark("Card" + rule.Kind + rule.Merge, rule, 1, out var mark), Is.True);
            Assert.That(mark.Spots.Length, Is.GreaterThan(0));
            Assert.That((mark.Spots[0].X, mark.Spots[0].Z, mark.Spots[0].Side), Is.EqualTo((mark.X, mark.Z, mark.Side)));
        }
    }

    [Test]
    public void ReplaceLot_WithSize_TakesThatManyNeighbouringLots()
    {
        for (var seed = 1UL; seed <= 50; seed++)
        {
            var plan = CityWfc.Generate(seed);
            var planner = new CardMarkPlanner(plan, seed);
            Assert.That(planner.TryMark("CardZonaComercial", Commerce, 1, out var mark), Is.True);
            Assert.That(mark.Spots.Length, Is.EqualTo(4), $"semente {seed}: cidade nova tem quadra com 4 lotes juntos");
            var cells = new HashSet<(int, int)>();
            foreach (var spot in mark.Spots)
            {
                Assert.That(plan.IsRoad(spot.X, spot.Z), Is.False);
                Assert.That(cells.Add((spot.X, spot.Z)), Is.True);
            }
            // Conexo: todo lote encosta em outro da marca.
            foreach (var spot in mark.Spots)
            {
                var touches = false;
                foreach (var other in mark.Spots)
                    touches |= Math.Abs(other.X - spot.X) + Math.Abs(other.Z - spot.Z) == 1;
                Assert.That(touches, Is.True, $"semente {seed}");
            }
        }
    }

    [Test]
    public void Merge_TakesAFreeTwoByTwoSquare()
    {
        for (var seed = 1UL; seed <= 50; seed++)
        {
            var plan = CityWfc.Generate(seed);
            var planner = new CardMarkPlanner(plan, seed);
            Assert.That(planner.TryMark("CardHospitalRegional", Stadium, 1, out var mark), Is.True);
            Assert.That(mark.Merged, Is.True, $"semente {seed}");
            Assert.That(mark.Spots.Length, Is.EqualTo(4));
            foreach (var spot in mark.Spots)
            {
                Assert.That(spot.X - mark.X, Is.InRange(0, 1));
                Assert.That(spot.Z - mark.Z, Is.InRange(0, 1));
                Assert.That(plan.IsRoad(spot.X, spot.Z), Is.False);
            }
        }
    }

    [Test]
    public void Merge_WithoutASquare_FallsBackToSingleLots()
    {
        var plan = CityWfc.Generate(9);
        var planner = new CardMarkPlanner(plan, 9);
        var marks = 0;
        var merged = 0;
        while (planner.TryMark("CardHospitalRegional", Stadium, marks + 1, out var mark))
        {
            marks++;
            if (mark.Merged)
                merged++;
            else
                Assert.That(mark.Spots.Length, Is.EqualTo(1), "sem tamanho pedido, sai um lote so");
        }
        Assert.That(merged, Is.GreaterThan(0));
        Assert.That(marks, Is.GreaterThan(merged), "acabados os quadrados, continua lote a lote");
    }

    [Test]
    public void MergedLots_NeverGetARoofProp()
    {
        var plan = CityWfc.Generate(13);
        var planner = new CardMarkPlanner(plan, 13);
        var merged = new HashSet<(int, int)>();
        for (var turn = 1; turn <= 6; turn++)
            if (planner.TryMark("CardUniversidadePublica", Stadium, turn, out var mark) && mark.Merged)
                foreach (var spot in mark.Spots)
                    merged.Add((spot.X, spot.Z));
        for (var turn = 7; turn <= 200; turn++)
            if (planner.TryMark("CardRedeSolarDistribuida", Solar, turn, out var mark))
                foreach (var spot in mark.Spots)
                    Assert.That(merged.Contains((spot.X, spot.Z)), Is.False);
    }

    [Test]
    public void RoofAndStreetSpots_StayCloseToTheFirst()
    {
        for (var seed = 1UL; seed <= 30; seed++)
        {
            var plan = CityWfc.Generate(seed);
            var planner = new CardMarkPlanner(plan, seed);
            foreach (var rule in new[] { Solar, Bins })
            {
                Assert.That(planner.TryMark("Card" + rule.Kind, rule, 1, out var mark), Is.True);
                // Telhado plano e mais raro: pode vir com menos lugares, nunca vazio.
                if (rule.Kind == CardMarkKind.StreetProp)
                    Assert.That(mark.Spots.Length, Is.EqualTo(4), $"semente {seed}");
                else
                    Assert.That(mark.Spots.Length, Is.InRange(1, 4), $"semente {seed}");
                var places = new HashSet<(int, int, int)>();
                foreach (var spot in mark.Spots)
                {
                    Assert.That(Math.Abs(spot.X - mark.X) + Math.Abs(spot.Z - mark.Z), Is.LessThanOrEqualTo(3));
                    Assert.That(places.Add((spot.X, spot.Z, spot.Side)), Is.True);
                    if (rule.Kind == CardMarkKind.StreetProp)
                        Assert.That(CityPlan.ShapeOf(plan.RoadAt(spot.X, spot.Z), out _), Is.EqualTo(RoadShape.Straight));
                }
            }
        }
    }

    [Test]
    public void RoofProps_OnlyLandOnFlatRoofs()
    {
        var plan = CityWfc.Generate(17);
        var planner = new CardMarkPlanner(plan, 17);
        var flat = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, ResultHasFlatRoof = true, Size = 4, Limit = 1000 };
        var houses = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Size = 4, Limit = 1000 };
        var flatCells = new HashSet<(int, int)>();
        var houseCells = new HashSet<(int, int)>();
        for (var turn = 1; turn <= 4; turn++)
        {
            if (planner.TryMark("CardIncubadoraDeStartups", flat, turn, out var mark))
                foreach (var spot in mark.Spots)
                    flatCells.Add((spot.X, spot.Z));
            if (planner.TryMark("CardMutiraoHabitacional", houses, turn + 100, out mark))
                foreach (var spot in mark.Spots)
                    houseCells.Add((spot.X, spot.Z));
        }
        var roofs = 0;
        for (var turn = 200; turn <= 400; turn++)
        {
            if (planner.TryMark("CardRedeSolarDistribuida", Solar, turn, out var mark) == false)
                continue;
            foreach (var spot in mark.Spots)
            {
                roofs++;
                var cell = (spot.X, spot.Z);
                Assert.That(houseCells.Contains(cell), Is.False, "casa nova tem telhado inclinado");
                var use = plan.LotUseAt(spot.X, spot.Z);
                Assert.That(flatCells.Contains(cell) || use == CityLotUse.Apartment || use == CityLotUse.Tower, Is.True, $"{use} em {cell}");
            }
        }
        Assert.That(roofs, Is.GreaterThan(0));
    }

    [Test]
    public void Size_IsClampedToTheMaximum()
    {
        var planner = new CardMarkPlanner(CityWfc.Generate(2), 2);
        var rule = new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Size = 50 };
        Assert.That(planner.TryMark("CardZonaComercial", rule, 1, out var mark), Is.True);
        Assert.That(mark.Spots.Length, Is.LessThanOrEqualTo(CardMarkPlanner.MaxSize));
    }
}

public class CardMarkMotionTests
{
    [Test]
    public void Drop_StartsHigh_AndLandsAtZero()
    {
        Assert.That(CardMarkMotion.Height(0f), Is.EqualTo(CardMarkMotion.StartHeight));
        Assert.That(CardMarkMotion.Height(CardMarkMotion.DropDuration), Is.EqualTo(0f));
        Assert.That(CardMarkMotion.HasLanded(CardMarkMotion.DropDuration), Is.True);
        Assert.That(CardMarkMotion.HasLanded(CardMarkMotion.DropDuration - 0.01f), Is.False);
    }

    [Test]
    public void Drop_SpeedsUp()
    {
        var steps = 10;
        var last = 0f;
        for (var i = 1; i <= steps; i++)
        {
            var fall = CardMarkMotion.Height((i - 1) * CardMarkMotion.DropDuration / steps) - CardMarkMotion.Height(i * CardMarkMotion.DropDuration / steps);
            Assert.That(fall, Is.GreaterThan(last));
            last = fall;
        }
    }

    [Test]
    public void Landing_SquashesThenRecovers()
    {
        Assert.That(CardMarkMotion.Scale(0.1f), Is.EqualTo((1f, 1f)));
        var middle = CardMarkMotion.Scale(CardMarkMotion.DropDuration + CardMarkMotion.SettleDuration / 2f);
        Assert.That(middle.Vertical, Is.EqualTo(1f - CardMarkMotion.Squash).Within(1e-4));
        Assert.That(middle.Horizontal, Is.EqualTo(1f + CardMarkMotion.Spread).Within(1e-4));
        Assert.That(CardMarkMotion.Scale(CardMarkMotion.Total), Is.EqualTo((1f, 1f)));
    }

    [Test]
    public void OldPiece_SinksBeforeTheFirstDrop()
    {
        Assert.That(CardMarkMotion.SinkScale(0f), Is.EqualTo(1f));
        Assert.That(CardMarkMotion.SinkScale(CardMarkMotion.SinkDuration), Is.EqualTo(0f));
        Assert.That(CardMarkMotion.StartOf(0), Is.EqualTo(CardMarkMotion.SinkDuration));
        Assert.That(CardMarkMotion.StartOf(2) - CardMarkMotion.StartOf(1), Is.EqualTo(CardMarkMotion.Stagger).Within(1e-6));
        Assert.That(CardMarkMotion.Duration(4), Is.EqualTo(CardMarkMotion.StartOf(3) + CardMarkMotion.Total).Within(1e-6));
    }

    [Test]
    public void FourPieces_FinishBeforeTheCardImpact()
    {
        // O impacto da carta dura 1,6 s; a marca precisa terminar antes do popup do evento.
        Assert.That(CardMarkMotion.Duration(4), Is.LessThan(1.6f));
    }
}

public class CityDecorTests
{
    [Test]
    public void StreetLights_StandOnTheSidewalkOfStraightRoads()
    {
        var plan = CityWfc.Generate(31);
        foreach (var item in CityDecor.Plan(plan))
        {
            if (item.Kind != CityDecorKind.StreetLight)
                continue;
            var shape = CityPlan.ShapeOf(plan.RoadAt(item.X, item.Z), out var rotation);
            Assert.That(shape, Is.EqualTo(RoadShape.Straight));
            // Na reta norte-sul o poste sai para leste ou oeste; na leste-oeste, para norte ou sul.
            if (rotation == 0f)
            {
                Assert.That(Math.Abs(item.OffsetX), Is.EqualTo(CityDecor.SidewalkOffset));
                Assert.That(item.OffsetZ, Is.EqualTo(0f));
            }
            else
            {
                Assert.That(item.OffsetX, Is.EqualTo(0f));
                Assert.That(Math.Abs(item.OffsetZ), Is.EqualTo(CityDecor.SidewalkOffset));
            }
        }
    }

    [Test]
    public void EveryJunction_GetsOneTrafficSignal_OnACorner()
    {
        var plan = CityWfc.Generate(31);
        var signals = new HashSet<(int, int)>();
        foreach (var item in CityDecor.Plan(plan))
        {
            if (item.Kind != CityDecorKind.TrafficSignal)
                continue;
            Assert.That(signals.Add((item.X, item.Z)), Is.True);
            Assert.That(Math.Abs(item.OffsetX), Is.EqualTo(CityDecor.SidewalkOffset));
            Assert.That(Math.Abs(item.OffsetZ), Is.EqualTo(CityDecor.SidewalkOffset));
        }

        var junctions = 0;
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var shape = CityPlan.ShapeOf(plan.RoadAt(x, z), out _);
                if (shape == RoadShape.TJunction || shape == RoadShape.Cross)
                    junctions++;
            }
        }
        Assert.That(signals.Count, Is.EqualTo(junctions));
    }

    [Test]
    public void Decor_IsTheSameForTheSamePlan()
    {
        var a = CityDecor.Plan(CityWfc.Generate(8));
        var b = CityDecor.Plan(CityWfc.Generate(8));
        Assert.That(b.Count, Is.EqualTo(a.Count));
        for (var i = 0; i < a.Count; i++)
        {
            Assert.That(b[i].X, Is.EqualTo(a[i].X));
            Assert.That(b[i].Z, Is.EqualTo(a[i].Z));
            Assert.That(b[i].Rotation, Is.EqualTo(a[i].Rotation));
        }
    }
}

public class RoadTechGlowTests
{
    [Test]
    public void BelowDigital_TheStreetsStayDark()
    {
        Assert.That(RoadTechGlow.CurbIntensity(14.9f), Is.EqualTo(0f));
        Assert.That(RoadTechGlow.PulseCount(14.9f, 12), Is.EqualTo(0));
        Assert.That(RoadTechGlow.SmartIntensity(14.9f), Is.EqualTo(0f));
    }

    [Test]
    public void EachTier_LightsOneMoreLayer()
    {
        Assert.That(RoadTechGlow.CurbIntensity(RoadTechGlow.DigitalPesquisa), Is.GreaterThan(0f));
        Assert.That(RoadTechGlow.PulseCount(RoadTechGlow.DigitalPesquisa, 12), Is.EqualTo(0));

        Assert.That(RoadTechGlow.PulseCount(RoadTechGlow.ConnectedPesquisa, 12), Is.GreaterThan(0));
        Assert.That(RoadTechGlow.SmartIntensity(RoadTechGlow.ConnectedPesquisa), Is.EqualTo(0f));

        Assert.That(RoadTechGlow.SmartIntensity(RoadTechGlow.SmartPesquisa), Is.GreaterThan(0f));
    }

    [Test]
    public void Glow_GrowsWithPesquisa_UpToTheMaximum()
    {
        var lastCurb = 0f;
        var lastPulses = 0;
        var lastSmart = 0f;
        for (var p = 0f; p <= 100f; p += 0.5f)
        {
            var curb = RoadTechGlow.CurbIntensity(p);
            var pulses = RoadTechGlow.PulseCount(p, 12);
            var smart = RoadTechGlow.SmartIntensity(p);
            Assert.That(curb, Is.GreaterThanOrEqualTo(lastCurb));
            Assert.That(pulses, Is.GreaterThanOrEqualTo(lastPulses));
            Assert.That(smart, Is.GreaterThanOrEqualTo(lastSmart));
            Assert.That(curb, Is.LessThanOrEqualTo(1f));
            Assert.That(smart, Is.LessThanOrEqualTo(1f));
            Assert.That(pulses, Is.LessThanOrEqualTo(12));
            (lastCurb, lastPulses, lastSmart) = (curb, pulses, smart);
        }
        Assert.That(RoadTechGlow.CurbIntensity(RoadTechGlow.FullPesquisa), Is.EqualTo(1f));
        Assert.That(RoadTechGlow.SmartIntensity(RoadTechGlow.FullPesquisa), Is.EqualTo(1f));
        Assert.That(RoadTechGlow.PulseCount(RoadTechGlow.FullPesquisa, 12), Is.EqualTo(12));
        Assert.That(RoadTechGlow.PulseCount(100f, 0), Is.EqualTo(0));
    }

    [TestCase(31UL)]
    [TestCase(7UL)]
    [TestCase(2024UL)]
    public void CurbLines_StayOnTheRoad_AndCloseEveryContour(ulong seed)
    {
        var plan = CityWfc.Generate(seed);
        var segments = RoadTechGlow.CurbSegments(plan);
        Assert.That(segments, Is.Not.Empty);

        var ends = new Dictionary<(long, long), int>();
        foreach (var segment in segments)
        {
            // O meio do trecho fica numa celula de rua, nunca num lote.
            var cellX = (int)Math.Round((segment.X1 + segment.X2) / 2f);
            var cellZ = (int)Math.Round((segment.Z1 + segment.Z2) / 2f);
            Assert.That(plan.IsRoad(cellX, cellZ), Is.True, $"trecho fora da rua em {cellX},{cellZ}");
            Assert.That(segment.Length, Is.GreaterThan(0f));
            foreach (var (x, z) in new[] { (segment.X1, segment.Z1), (segment.X2, segment.Z2) })
            {
                var key = ((long)Math.Round(x * 1000f), (long)Math.Round(z * 1000f));
                ends[key] = ends.TryGetValue(key, out var count) ? count + 1 : 1;
            }
        }

        // Toda ponta dentro da cidade emenda em outra; so a borda do grid deixa ponta solta.
        foreach (var pair in ends)
        {
            var x = pair.Key.Item1 / 1000f;
            var z = pair.Key.Item2 / 1000f;
            var onBorder = x <= -0.5f + 1e-3f || z <= -0.5f + 1e-3f || x >= plan.Width - 0.5f - 1e-3f || z >= plan.Height - 0.5f - 1e-3f;
            if (onBorder == false)
                Assert.That(pair.Value % 2, Is.EqualTo(0), $"ponta solta em {x},{z}");
        }
    }

    [Test]
    public void CenterLines_FollowEveryConnectedSide_AndCrossingsOutlineEveryJunction()
    {
        var plan = CityWfc.Generate(31);
        var sides = 0;
        var junctions = 0;
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var road = plan.RoadAt(x, z);
                for (var bit = 1; bit <= 8; bit <<= 1)
                    if ((road & (RoadSides)bit) != 0)
                        sides++;
                var shape = CityPlan.ShapeOf(road, out _);
                if (shape == RoadShape.TJunction || shape == RoadShape.Cross)
                    junctions++;
            }
        }
        Assert.That(RoadTechGlow.CenterSegments(plan).Count, Is.EqualTo(sides));
        Assert.That(RoadTechGlow.CrossingSegments(plan).Count, Is.EqualTo(junctions * 4));
        Assert.That(junctions, Is.GreaterThan(0));
    }
}

public class CityCoastTests
{
    private static CityWfcOptions Coastal(CoastSide side) => new CityWfcOptions { Coast = side };

    [Test]
    public void SouthCoast_HasWaterThenBeach_AndTheCityAbove()
    {
        var plan = CityWfc.Generate(77, Coastal(CoastSide.South));
        Assert.That(plan.Coast, Is.EqualTo(CoastSide.South));
        for (var x = 0; x < plan.Width; x++)
        {
            Assert.That(plan.CoastAt(x, 0), Is.EqualTo(CoastCell.Water));
            Assert.That(plan.CoastAt(x, 1), Is.EqualTo(CoastCell.Water));
            Assert.That(plan.CoastAt(x, 2), Is.EqualTo(CoastCell.Beach));
            for (var z = 0; z < 3; z++)
            {
                Assert.That(plan.IsRoad(x, z), Is.False, $"rua no litoral em {x},{z}");
                Assert.That(plan.IsLot(x, z), Is.False);
            }
            for (var z = 3; z < plan.Height; z++)
                Assert.That(plan.IsCoast(x, z), Is.False);
        }
    }

    [Test]
    public void TheCity_IsTheSameAsOneGeneratedInTheSmallerArea()
    {
        var plan = CityWfc.Generate(77, Coastal(CoastSide.South));
        var inner = CityWfc.Generate(77, new CityWfcOptions { Height = 12 });
        for (var x = 0; x < inner.Width; x++)
        {
            for (var z = 0; z < inner.Height; z++)
            {
                Assert.That(plan.RoadAt(x, z + 3), Is.EqualTo(inner.RoadAt(x, z)));
                Assert.That(plan.LotUseAt(x, z + 3), Is.EqualTo(inner.LotUseAt(x, z)));
                Assert.That(plan.FacingAt(x, z + 3), Is.EqualTo(inner.FacingAt(x, z)));
            }
        }
    }

    [Test]
    public void WithoutCoast_NothingChanges()
    {
        var plain = CityWfc.Generate(4242);
        var none = CityWfc.Generate(4242, new CityWfcOptions { Coast = CoastSide.None });
        for (var x = 0; x < plain.Width; x++)
        {
            for (var z = 0; z < plain.Height; z++)
            {
                Assert.That(none.RoadAt(x, z), Is.EqualTo(plain.RoadAt(x, z)));
                Assert.That(none.LotUseAt(x, z), Is.EqualTo(plain.LotUseAt(x, z)));
                Assert.That(plain.IsCoast(x, z), Is.False);
            }
        }
        Assert.That(plain.Coast, Is.EqualTo(CoastSide.None));
    }

    [TestCase(CoastSide.North)]
    [TestCase(CoastSide.East)]
    [TestCase(CoastSide.West)]
    public void EverySide_PutsTheWaterOnTheBorder(CoastSide side)
    {
        var plan = CityWfc.Generate(9, Coastal(side));
        var water = 0;
        var beach = 0;
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var onBorder = side switch
                {
                    CoastSide.North => z == plan.Height - 1,
                    CoastSide.East => x == plan.Width - 1,
                    _ => x == 0
                };
                if (onBorder)
                    Assert.That(plan.CoastAt(x, z), Is.EqualTo(CoastCell.Water));
                if (plan.CoastAt(x, z) == CoastCell.Water)
                    water++;
                if (plan.CoastAt(x, z) == CoastCell.Beach)
                    beach++;
            }
        }
        Assert.That(water, Is.EqualTo(2 * 15));
        Assert.That(beach, Is.EqualTo(15));
    }

    [Test]
    public void ManySeeds_KeepTheCityRules_AboveTheCoast()
    {
        var inner = new CityWfcOptions { Height = 12 };
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var plan = CityWfc.Generate(seed, Coastal(CoastSide.South));
            Assert.That(plan.UsedFallback, Is.False, $"semente {seed}");
            var roads = new RoadSides[15, 12];
            for (var x = 0; x < 15; x++)
                for (var z = 0; z < 12; z++)
                    roads[x, z] = plan.RoadAt(x, z + 3);
            Assert.That(CityWfc.PassesGlobalRules(roads, inner), Is.True, $"semente {seed}");
        }
    }

    [Test]
    public void CardMarks_NeverLandOnTheCoast()
    {
        var plan = CityWfc.Generate(77, Coastal(CoastSide.South));
        var planner = new CardMarkPlanner(plan, 77);
        var rules = new[]
        {
            new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Placement = CardMarkPlacement.Edge, Size = 4, Limit = 50 },
            new CardMarkRule { Kind = CardMarkKind.ReplaceLot, Size = 4, Merge = true, Limit = 50 },
            new CardMarkRule { Kind = CardMarkKind.StreetProp, Size = 4, Limit = 50 },
        };
        for (var turn = 1; turn <= 40; turn++)
        {
            var rule = rules[turn % rules.Length];
            if (planner.TryMark("Card" + (turn % rules.Length), rule, turn, out var mark) == false || rule.Kind == CardMarkKind.StreetProp)
                continue;
            foreach (var spot in mark.Spots)
                Assert.That(plan.IsCoast(spot.X, spot.Z), Is.False, $"marca no litoral em {spot.X},{spot.Z}");
        }
    }
}

public class CoastDecorTests
{
    private static CityPlan Coastal(ulong seed) => CityWfc.Generate(seed, new CityWfcOptions { Coast = CoastSide.South });

    [Test]
    public void NoCoast_NoDecor()
    {
        Assert.That(CoastDecor.Plan(CityWfc.Generate(5), 5), Is.Empty);
    }

    [Test]
    public void SameCityAndSeed_SameDecor()
    {
        var a = CoastDecor.Plan(Coastal(11), 11);
        var b = CoastDecor.Plan(Coastal(11), 11);
        Assert.That(b.Count, Is.EqualTo(a.Count));
        for (var i = 0; i < a.Count; i++)
        {
            Assert.That(b[i].Kind, Is.EqualTo(a[i].Kind));
            Assert.That(b[i].X, Is.EqualTo(a[i].X));
            Assert.That(b[i].Z, Is.EqualTo(a[i].Z));
        }
    }

    [Test]
    public void EachPiece_StaysOnItsKindOfCell()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var plan = Coastal(seed);
            var items = CoastDecor.Plan(plan, seed);
            foreach (var item in items)
            {
                var cell = plan.CoastAt(StreetWalk.CellOf(item.X), StreetWalk.CellOf(item.Z));
                var expected = item.Kind == CoastDecorKind.Palm || item.Kind == CoastDecorKind.Rock ? CoastCell.Beach : CoastCell.Water;
                if (item.Kind == CoastDecorKind.Ship)
                    Assert.That(item.Z, Is.EqualTo(0f), "navio na faixa mais funda");
                else
                    Assert.That(cell, Is.EqualTo(expected), $"{item.Kind} em {item.X},{item.Z}, semente {seed}");
            }
            Assert.That(items.FindAll(i => i.Kind == CoastDecorKind.Ship).Count, Is.LessThanOrEqualTo(1));
            Assert.That(items.FindAll(i => i.Kind == CoastDecorKind.Buoy).Count, Is.LessThanOrEqualTo(CoastDecor.BuoyCount));
            Assert.That(items.FindAll(i => i.Kind == CoastDecorKind.Palm).Count, Is.GreaterThan(0), $"semente {seed}");
        }
    }

    [Test]
    public void APier_StandsInFrontOfEveryStreetThatReachesTheBeach()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var plan = Coastal(seed);
            var piers = CoastDecor.Plan(plan, seed).FindAll(i => i.Kind == CoastDecorKind.Pier);
            var accesses = 0;
            for (var x = 0; x < plan.Width; x++)
            {
                if ((plan.RoadAt(x, 3) & RoadSides.South) == 0)
                    continue;
                accesses++;
                Assert.That(piers.Exists(p => p.X == x && p.Z == 1f), Is.True, $"sem pier em {x}, semente {seed}");
            }
            Assert.That(piers.Count, Is.EqualTo(accesses));
            foreach (var pier in piers)
                Assert.That(pier.Rotation, Is.EqualTo(180f), "o pier aponta para o mar (sul)");
        }
    }
}
