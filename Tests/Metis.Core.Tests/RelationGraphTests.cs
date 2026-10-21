using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class RelationGraphTests
{
    private const float Tolerance = 0.0001f;

    private static readonly CityParameterType[] Parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));

    private static InteractionMatrix Calibrated() => new InteractionMatrix(50f, 1f, 25f, 75f, 1.6f, 0.5f, 0.6f, 0.1f);

    private static CityStats RandomCity(int seed)
    {
        var random = new Random(seed);
        return CityStatsTestFactory.Build(Calibrated(), customize: configs =>
        {
            foreach (var parameter in Parameters)
            {
                var config = configs[parameter];
                config.InitialValue = (float)(random.NextDouble() * 100.0);
                configs[parameter] = config;
            }
        });
    }

    private static void SetInitial(Dictionary<CityParameterType, CityParameterConfig> configs, CityParameterType parameter, float value)
    {
        var config = configs[parameter];
        config.InitialValue = value;
        configs[parameter] = config;
    }

    [Test]
    public void ResolutionOrder_HasTheEightTargetsWithoutWellbeing()
    {
        Assert.That(InteractionMatrix.Order.Count, Is.EqualTo(8));
        Assert.That(InteractionMatrix.Order, Has.No.Member(CityParameterType.BemEstar));
        Assert.That(InteractionMatrix.Order.Distinct().Count(), Is.EqualTo(8));
    }

    [Test]
    public void StaticMap_HasOneEdgePerNonZeroCoefficientPlusTheWellbeingParts()
    {
        var edges = RelationGraph.Build(null);

        var interactions = edges.Where(e => e.Kind == RelationKind.Interaction).ToList();
        var nonZero = 0;
        foreach (var alvo in Parameters)
        {
            foreach (var fonte in Parameters)
            {
                var coefficient = InteractionMatrix.GetCoefficient(alvo, fonte);
                if (coefficient == 0f)
                    continue;
                nonZero++;
                var edge = interactions.Single(e => e.From == fonte && e.To == alvo);
                Assert.That(edge.Coefficient, Is.EqualTo(coefficient));
                Assert.That(edge.ZoneFactor, Is.EqualTo(1f));
                Assert.That(edge.HasState, Is.False);
            }
        }
        Assert.That(interactions.Count, Is.EqualTo(nonZero));
        Assert.That(edges.Count(e => e.Kind == RelationKind.WellbeingPart), Is.EqualTo(5));
        Assert.That(edges.Where(e => e.Kind == RelationKind.Interaction).Select(e => e.To), Has.No.Member(CityParameterType.BemEstar));
    }

    // A parcela mostrada no mapa e a mesma que a resolucao aplica, enquanto a fonte ainda nao mudou no turno.
    [Test]
    public void CurrentAmount_MatchesWhatTheResolutionApplies()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var stats = RandomCity(seed);
            var edges = RelationGraph.Build(stats).Where(e => e.Kind == RelationKind.Interaction).ToList();
            var breakdown = new TurnBreakdown(1);
            stats.Recorder = breakdown;
            stats.ResolveTurn();

            var order = InteractionMatrix.Order.ToList();
            foreach (var edge in edges)
            {
                // Fontes resolvidas antes do alvo ja foram atualizadas nesta passada.
                var sourceIndex = order.IndexOf(edge.From);
                if (sourceIndex >= 0 && sourceIndex < order.IndexOf(edge.To))
                    continue;

                var recorded = breakdown.For(edge.To)
                    .Where(c => c.Source == ContributionSource.Interaction && c.From == edge.From)
                    .Sum(c => c.Amount);
                Assert.That(edge.CurrentAmount, Is.EqualTo(recorded).Within(Tolerance), $"semente {seed}, {edge.From} -> {edge.To}");
            }
        }
    }

    [Test]
    public void ZoneFactor_FollowsTheSourceZone()
    {
        var stats = CityStatsTestFactory.Build(Calibrated(), customize: configs =>
        {
            SetInitial(configs, CityParameterType.Populacao, 10f);
            SetInitial(configs, CityParameterType.Renda, 90f);
        });
        var edges = RelationGraph.Build(stats);

        var popToEnergia = edges.Single(e => e.From == CityParameterType.Populacao && e.To == CityParameterType.Energia);
        Assert.That(popToEnergia.IsSupport, Is.False);
        Assert.That(popToEnergia.ZoneFactor, Is.EqualTo(1.6f), "pressao de fonte em crise e amplificada");

        var popToRenda = edges.Single(e => e.From == CityParameterType.Populacao && e.To == CityParameterType.Renda);
        Assert.That(popToRenda.ZoneFactor, Is.EqualTo(0.5f), "apoio de fonte em crise cai pela metade");

        var rendaToEnergia = edges.Single(e => e.From == CityParameterType.Renda && e.To == CityParameterType.Energia);
        Assert.That(rendaToEnergia.ZoneFactor, Is.EqualTo(0.6f), "apoio de fonte em excesso tem retorno decrescente");

        var rendaToSust = edges.Single(e => e.From == CityParameterType.Renda && e.To == CityParameterType.Sustentabilidade);
        Assert.That(rendaToSust.ZoneFactor, Is.EqualTo(1f), "pressao de fonte em excesso continua inteira");

        var matrix = stats.Interactions;
        Assert.That(matrix.GetZone(10f), Is.EqualTo(InteractionZone.Crise));
        Assert.That(matrix.GetZone(50f), Is.EqualTo(InteractionZone.Saudavel));
        Assert.That(matrix.GetZone(90f), Is.EqualTo(InteractionZone.Excesso));
    }

    [Test]
    public void WellbeingParts_AddUpToTheDerivedValue()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var stats = RandomCity(seed);
            stats.ResolveTurn();
            var parts = RelationGraph.Build(stats).Where(e => e.Kind == RelationKind.WellbeingPart).Sum(e => e.CurrentAmount);
            var expected = Math.Clamp(parts + stats.AnchorBonus, 0f, 100f);
            Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(expected).Within(Tolerance), $"semente {seed}");
        }
    }

    [Test]
    public void IncomingAndOutgoing_AreSortedByStrength()
    {
        var edges = RelationGraph.Build(null);

        var incoming = RelationGraph.Incoming(edges, CityParameterType.Renda);
        Assert.That(incoming.All(e => e.To == CityParameterType.Renda), Is.True);
        Assert.That(incoming.First().From, Is.EqualTo(CityParameterType.Populacao));
        Assert.That(incoming.Select(e => e.Strength), Is.Ordered.Descending);

        var outgoing = RelationGraph.Outgoing(edges, CityParameterType.BemEstar);
        Assert.That(outgoing.All(e => e.From == CityParameterType.BemEstar), Is.True);
        Assert.That(outgoing.First().To, Is.EqualTo(CityParameterType.Populacao));

        var pair = RelationGraph.Between(edges, CityParameterType.Energia, CityParameterType.Sustentabilidade);
        Assert.That(pair.Count, Is.EqualTo(2));
    }

    [Test]
    public void Strength_IsBucketedInThreeLevels()
    {
        Assert.That(RelationGraph.GetStrength(0.2f), Is.EqualTo(RelationStrength.Fraca));
        Assert.That(RelationGraph.GetStrength(0.5f), Is.EqualTo(RelationStrength.Media));
        Assert.That(RelationGraph.GetStrength(1.2f), Is.EqualTo(RelationStrength.Forte));
    }
}
