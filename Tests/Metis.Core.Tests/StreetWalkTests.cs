using System;
using System.Collections.Generic;

namespace Metis.Core.Tests;

public class StreetWalkTests
{
    private static readonly ulong[] Seeds = { 1, 7, 31, 202, 4096 };

    // Rua em cruz no meio de um grid 5x5: x = 2 e z = 2 sao rua, o resto e lote.
    private static CityPlan CrossPlan()
    {
        var roads = new RoadSides[5, 5];
        for (var i = 0; i < 5; i++)
        {
            roads[2, i] |= (i < 4 ? RoadSides.North : 0) | (i > 0 ? RoadSides.South : 0);
            roads[i, 2] |= (i < 4 ? RoadSides.East : 0) | (i > 0 ? RoadSides.West : 0);
        }
        return PlanFrom(roads);
    }

    private static CityPlan PlanFrom(RoadSides[,] roads)
    {
        var plan = new CityPlan(roads.GetLength(0), roads.GetLength(1));
        for (var x = 0; x < plan.Width; x++)
            for (var z = 0; z < plan.Height; z++)
                plan.SetRoad(x, z, roads[x, z]);
        return plan;
    }

    private static bool OnRoadOnly(StreetWalk walk, CityPlan plan, WalkPose pose)
    {
        // O corpo inteiro (centro e os quatro extremos do raio) fica sobre rua.
        var r = walk.Radius * 0.999f;
        foreach (var (dx, dz) in new[] { (0f, 0f), (r, 0f), (-r, 0f), (0f, r), (0f, -r) })
        {
            var x = StreetWalk.CellOf(pose.X + dx);
            var z = StreetWalk.CellOf(pose.Z + dz);
            if (plan.IsInBounds(x, z) == false || plan.IsRoad(x, z) == false)
                return false;
        }
        return true;
    }

    [Test]
    public void StartPose_IsOnAJunctionNearTheCenter_LookingDownARoad()
    {
        foreach (var seed in Seeds)
        {
            var plan = CityWfc.Generate(seed);
            var pose = StreetWalk.StartPose(plan);
            var x = (int)pose.X;
            var z = (int)pose.Z;
            Assert.That(plan.IsRoad(x, z), Is.True, $"semente {seed}");
            var side = (int)Math.Round(pose.Yaw / 90f) % 4;
            Assert.That(((int)plan.RoadAt(x, z) & (1 << side)) != 0, Is.True, $"olhar para fora da rua na semente {seed}");
            Assert.That(pose.Pitch, Is.EqualTo(0f));
        }
    }

    [Test]
    public void RandomWalks_NeverEnterALot_NorLeaveTheGrid()
    {
        foreach (var seed in Seeds)
        {
            var plan = CityWfc.Generate(seed);
            var walk = new StreetWalk(plan);
            var pose = StreetWalk.StartPose(plan);
            var random = new SeededRandom(seed);
            for (var i = 0; i < 1000; i++)
            {
                pose = StreetWalk.Look(pose, (float)(random.NextDouble() * 90 - 45), 0f);
                // Passos de ate meia celula, como um quadro muito lento.
                pose = walk.Walk(pose, 1f, (float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 0.5));
                Assert.That(OnRoadOnly(walk, plan, pose), Is.True, $"semente {seed}, passo {i}: {pose.X},{pose.Z}");
            }
        }
    }

    [Test]
    public void RandomWalks_ReachMoreThanTheStartingCell()
    {
        var plan = CityWfc.Generate(31);
        var walk = new StreetWalk(plan);
        var pose = StreetWalk.StartPose(plan);
        var random = new SeededRandom(5);
        var visited = new HashSet<(int, int)>();
        for (var i = 0; i < 2000; i++)
        {
            pose = StreetWalk.Look(pose, (float)(random.NextDouble() * 60 - 30), 0f);
            pose = walk.Walk(pose, 1f, 0f, 0.1f);
            visited.Add((StreetWalk.CellOf(pose.X), StreetWalk.CellOf(pose.Z)));
        }
        Assert.That(visited.Count, Is.GreaterThan(5));
    }

    [Test]
    public void WalkingIntoABuilding_StopsAtTheCurb()
    {
        var plan = CrossPlan();
        var walk = new StreetWalk(plan);
        // No meio da rua norte-sul, andando para o leste, contra o lote (3, 4).
        var pose = walk.Move(new WalkPose(2f, 4f, 90f, 0f), 2f, 0f);
        Assert.That(pose.X, Is.LessThanOrEqualTo(2.5f - walk.Radius + 1e-4f));
        Assert.That(pose.X, Is.GreaterThan(2.5f - walk.Radius - walk.Radius));
        Assert.That(pose.Z, Is.EqualTo(4f));
    }

    [Test]
    public void WalkingDiagonallyAlongAWall_Slides()
    {
        var plan = CrossPlan();
        var walk = new StreetWalk(plan);
        // Encostado no lote a leste, andando para nordeste: o leste trava e o norte continua.
        var start = new WalkPose(2f, 3f, 45f, 0f);
        var pose = walk.Move(start, 0.8f, 0.8f);
        Assert.That(pose.Z, Is.GreaterThan(start.Z + 0.5f));
        Assert.That(pose.X, Is.LessThan(2.5f));
    }

    [Test]
    public void CuttingTheCornerOfALot_IsBlocked()
    {
        var plan = CrossPlan();
        var walk = new StreetWalk(plan);
        // Do braco norte para o braco leste em linha reta passa pela quina do lote (3, 3).
        var start = new WalkPose(2f, 3.4f, 0f, 0f);
        Assert.That(walk.IsFree(2.4f, 2.9f), Is.False, "o caminho reto encosta no lote");
        var pose = walk.Move(start, 1.4f, -1.4f);
        Assert.That(OnRoadOnly(walk, plan, pose), Is.True);
        Assert.That(walk.IsFree(pose.X, pose.Z), Is.True);
        // Chegou ao cruzamento deslizando, sem entrar no lote da quina.
        Assert.That(pose.Z, Is.LessThan(start.Z));
    }

    [Test]
    public void TheGridBorder_IsAWall()
    {
        var plan = CrossPlan();
        var walk = new StreetWalk(plan);
        var pose = walk.Move(new WalkPose(2f, 3f, 0f, 0f), 0f, 10f);
        Assert.That(pose.Z, Is.LessThanOrEqualTo(4.5f - walk.Radius + 1e-4f));
        Assert.That(pose.Z, Is.GreaterThan(4f));
    }

    [Test]
    public void Walk_UsesTheYaw()
    {
        var plan = CrossPlan();
        var walk = new StreetWalk(plan);
        var east = walk.Walk(new WalkPose(1f, 2f, 90f, 0f), 1f, 0f, 1f);
        Assert.That(east.X, Is.EqualTo(2f).Within(1e-4f));
        Assert.That(east.Z, Is.EqualTo(2f).Within(1e-4f));

        // Olhando para o norte, "direita" e o leste.
        var strafe = walk.Walk(new WalkPose(1f, 2f, 0f, 0f), 0f, 1f, 1f);
        Assert.That(strafe.X, Is.EqualTo(2f).Within(1e-4f));
    }

    [Test]
    public void Look_WrapsTheYaw_AndClampsThePitch()
    {
        var pose = StreetWalk.Look(new WalkPose(0f, 0f, 350f, 0f), 20f, 100f);
        Assert.That(pose.Yaw, Is.EqualTo(10f).Within(1e-4f));
        Assert.That(pose.Pitch, Is.EqualTo(StreetWalk.MaxPitch));

        pose = StreetWalk.Look(pose, -30f, -500f);
        Assert.That(pose.Yaw, Is.EqualTo(340f).Within(1e-4f));
        Assert.That(pose.Pitch, Is.EqualTo(StreetWalk.MinPitch));
    }

    [Test]
    public void PushOut_MovesAwayFromACar_WithoutLeavingTheRoad()
    {
        var plan = CrossPlan();
        var walk = new StreetWalk(plan);
        var car = new List<(float, float, float)> { (2f, 1.1f, 0.2f) };
        var pose = walk.PushOut(new WalkPose(2f, 1f, 0f, 0f), car);
        var distance = Math.Sqrt(Math.Pow(pose.X - 2f, 2) + Math.Pow(pose.Z - 1.1f, 2));
        Assert.That(distance, Is.GreaterThanOrEqualTo(0.2f + walk.Radius - 1e-3f));
        Assert.That(OnRoadOnly(walk, plan, pose), Is.True);
    }

    [Test]
    public void Move_IgnoresNaN()
    {
        var plan = CrossPlan();
        var walk = new StreetWalk(plan);
        var pose = walk.Move(new WalkPose(2f, 2f, 0f, 0f), float.NaN, 1f);
        Assert.That(pose.X, Is.EqualTo(2f));
        Assert.That(pose.Z, Is.EqualTo(2f));
    }
}

public class StickInputTests
{
    [Test]
    public void InsideTheDeadZone_IsZero()
    {
        var (x, y) = StickInput.FromDrag(5f, 5f, 100f);
        Assert.That(x, Is.EqualTo(0f));
        Assert.That(y, Is.EqualTo(0f));
    }

    [Test]
    public void BeyondTheRadius_IsClampedToOne()
    {
        var (x, y) = StickInput.FromDrag(300f, 400f, 100f);
        Assert.That(Math.Sqrt(x * x + y * y), Is.EqualTo(1.0).Within(1e-4));
        Assert.That(x / y, Is.EqualTo(0.75f).Within(1e-4f));
    }

    [Test]
    public void JustOutsideTheDeadZone_StartsNearZero_AndGrows()
    {
        var (near, _) = StickInput.FromDrag(13f, 0f, 100f);
        var (far, _) = StickInput.FromDrag(60f, 0f, 100f);
        Assert.That(near, Is.GreaterThan(0f).And.LessThan(0.05f));
        Assert.That(far, Is.GreaterThan(near));
    }

    [Test]
    public void ZeroRadius_IsZero()
    {
        Assert.That(StickInput.FromDrag(10f, 10f, 0f), Is.EqualTo((0f, 0f)));
    }
}
