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
            if (mark.Kind == CardMarkKind.ReplaceLot)
            {
                Assert.That(replaced.Add((mark.X, mark.Z)), Is.True, $"troca repetida em {mark.X},{mark.Z}");
                Assert.That(roofs.Contains((mark.X, mark.Z)), Is.False, $"troca sobre telhado marcado em {mark.X},{mark.Z}");
            }
            else
            {
                Assert.That(roofs.Add((mark.X, mark.Z)), Is.True, $"telhado repetido em {mark.X},{mark.Z}");
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
