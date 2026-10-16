using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class TurnBreakdownTests
{
    private const float Tolerance = 0.001f;

    private static readonly CityParameterType[] Parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));

    // Mesmos numeros da cena: matriz calibrada, deriva de manutencao e penalidade de colapso.
    private static CityStats BuildCalibrated(Random random)
    {
        var interactions = new InteractionMatrix(50f, 1f, 25f, 75f, 1.6f, 0.5f, 0.6f, 0.1f);
        return CityStatsTestFactory.Build(interactions, colapsoPenaltyPerParameter: 12f, customize: configs =>
        {
            foreach (var parameter in Parameters)
            {
                var config = configs[parameter];
                config.InitialValue = (float)(random.NextDouble() * 100.0);
                config.CriticalLevel = parameter == CityParameterType.Pesquisa ? -1f : 10f;
                config.Deriva = parameter == CityParameterType.Renda ? 1f : -1.1f;
                configs[parameter] = config;
            }
        });
    }

    private static List<StatModifier> RandomEffects(Random random)
    {
        var effects = new List<StatModifier>();
        var count = random.Next(1, 5);
        for (var i = 0; i < count; i++)
        {
            effects.Add(new StatModifier
            {
                Parameter = Parameters[random.Next(Parameters.Length)],
                Amount = (float)(random.NextDouble() * 30.0 - 15.0),
            });
        }
        return effects;
    }

    // Um turno inteiro do jogo: carta, resolucao e evento, com ou sem registro.
    private static TurnBreakdown PlayTurn(CityStats stats, List<StatModifier> card, List<StatModifier> evento, bool record)
    {
        var breakdown = record ? new TurnBreakdown(1) : null;
        stats.Recorder = breakdown;
        breakdown?.CaptureStart(stats);

        stats.ApplyModifiers(card);
        stats.ResolveTurn();
        if (breakdown != null)
            breakdown.CurrentSource = ContributionSource.Event;
        stats.ApplyModifiers(evento);

        breakdown?.CaptureEnd(stats);
        stats.Recorder = null;
        return breakdown;
    }

    [Test]
    public void Recording_DoesNotChangeAnyValue()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var recorded = BuildCalibrated(new Random(seed));
            var plain = BuildCalibrated(new Random(seed));
            var effects = new Random(seed + 1000);
            var card = RandomEffects(effects);
            var evento = RandomEffects(effects);

            PlayTurn(recorded, card, evento, record: true);
            PlayTurn(plain, card, evento, record: false);

            foreach (var parameter in Parameters)
                Assert.That(recorded.GetValue(parameter), Is.EqualTo(plain.GetValue(parameter)), $"semente {seed}, {parameter}");
            Assert.That(recorded.AnchorBonus, Is.EqualTo(plain.AnchorBonus), $"semente {seed}");
        }
    }

    [Test]
    public void Contributions_AddUpToTheRealChange_ForEveryParameterButWellbeing()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var stats = BuildCalibrated(new Random(seed));
            var effects = new Random(seed + 1000);
            var breakdown = PlayTurn(stats, RandomEffects(effects), RandomEffects(effects), record: true);

            foreach (var parameter in Parameters.Where(p => p != CityStats.AnchorParameter))
                Assert.That(breakdown.Sum(parameter), Is.EqualTo(breakdown.GetDelta(parameter)).Within(Tolerance), $"semente {seed}, {parameter}");
        }
    }

    [Test]
    public void WellbeingComposition_MatchesTheValueAfterResolution()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var stats = BuildCalibrated(new Random(seed));
            var effects = new Random(seed + 1000);
            var breakdown = new TurnBreakdown(1);
            stats.Recorder = breakdown;
            stats.ApplyModifiers(RandomEffects(effects));
            stats.ResolveTurn();

            Assert.That(breakdown.HasWellbeing, Is.True);
            Assert.That(breakdown.Wellbeing.Total, Is.EqualTo(stats.GetValue(CityParameterType.BemEstar)).Within(Tolerance), $"semente {seed}");
        }
    }

    [Test]
    public void WellbeingComposition_SeparatesAverageCrowdingBonusAndCollapse()
    {
        var stats = CityStatsTestFactory.Build(colapsoPenaltyPerParameter: 12f, customize: configs =>
        {
            SetInitial(configs, CityParameterType.Saude, 70f);
            SetInitial(configs, CityParameterType.Populacao, 60f);
            var energia = configs[CityParameterType.Energia];
            energia.InitialValue = 5f;
            energia.CriticalLevel = 10f;
            configs[CityParameterType.Energia] = energia;
        });
        var breakdown = new TurnBreakdown(1);
        stats.Recorder = breakdown;

        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.BemEstar, Amount = 3f });
        stats.ResolveTurn();

        var composition = breakdown.Wellbeing;
        Assert.That(composition.PositiveAverage, Is.EqualTo(55f).Within(Tolerance));
        Assert.That(composition.Crowding, Is.EqualTo(10f).Within(Tolerance));
        Assert.That(composition.AnchorBonus, Is.EqualTo(3f).Within(Tolerance));
        Assert.That(composition.CollapsedCount, Is.EqualTo(1));
        Assert.That(composition.CollapsePenalty, Is.EqualTo(12f).Within(Tolerance));
        Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(36f).Within(Tolerance));
        Assert.That(breakdown.Sum(CityParameterType.BemEstar, ContributionSource.CollapsePenalty), Is.EqualTo(-12f).Within(Tolerance));
    }

    [Test]
    public void CardAndEvent_AreRecordedUnderTheCurrentSource()
    {
        var stats = CityStatsTestFactory.Build();
        var breakdown = new TurnBreakdown(1);
        stats.Recorder = breakdown;

        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = -4f });
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Saude, Amount = 6f });
        breakdown.CurrentSource = ContributionSource.Event;
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Saude, Amount = -2f });

        Assert.That(breakdown.Sum(CityParameterType.Renda, ContributionSource.Card), Is.EqualTo(-4f));
        Assert.That(breakdown.Sum(CityParameterType.Saude, ContributionSource.Card), Is.EqualTo(6f));
        Assert.That(breakdown.Sum(CityParameterType.Saude, ContributionSource.Event), Is.EqualTo(-2f));
    }

    [Test]
    public void PlayingACard_RecordsCostSeparatelyFromEffect()
    {
        var stats = CityStatsTestFactory.Build();
        var card = new FakeCard
        {
            Cost = 2f,
            StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Renda, Amount = 2f } },
        };
        var hand = new CardHand<FakeCard>(new List<FakeCard> { card });
        hand.Draw(1, stats);
        var breakdown = new TurnBreakdown(1);
        stats.Recorder = breakdown;

        Assert.That(hand.TryPlay(card, stats), Is.True);

        Assert.That(breakdown.Sum(CityParameterType.Renda, ContributionSource.CardCost), Is.EqualTo(-2f));
        Assert.That(breakdown.Sum(CityParameterType.Renda, ContributionSource.Card), Is.EqualTo(2f));
        Assert.That(breakdown.CurrentSource, Is.EqualTo(ContributionSource.Card), "a origem volta a ser a da jogada");
    }

    [Test]
    public void ValuePastTheLimit_RecordsTheClampedPart()
    {
        var stats = CityStatsTestFactory.Build(customize: configs => SetInitial(configs, CityParameterType.Saude, 95f));
        var breakdown = new TurnBreakdown(1);
        stats.Recorder = breakdown;
        breakdown.CaptureStart(stats);

        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Saude, Amount = 10f });
        breakdown.CaptureEnd(stats);

        Assert.That(breakdown.Sum(CityParameterType.Saude, ContributionSource.Clamp), Is.EqualTo(-5f).Within(Tolerance));
        Assert.That(breakdown.Sum(CityParameterType.Saude), Is.EqualTo(5f).Within(Tolerance));
        Assert.That(breakdown.GetDelta(CityParameterType.Saude), Is.EqualTo(5f).Within(Tolerance));
    }

    [Test]
    public void Interactions_NameTheParameterThatExertedTheInfluence()
    {
        var interactions = new InteractionMatrix(50f, 1f, 25f, 75f, 1.6f, 0.5f, 0.6f, 0.1f);
        var stats = CityStatsTestFactory.Build(interactions, customize: configs => SetInitial(configs, CityParameterType.Populacao, 70f));
        var breakdown = new TurnBreakdown(1);
        stats.Recorder = breakdown;

        stats.ResolveTurn();

        // Populacao acima do neutro pressiona Energia (coeficiente negativo) e apoia Renda.
        var energia = breakdown.For(CityParameterType.Energia).Single(c => c.Source == ContributionSource.Interaction && c.From == CityParameterType.Populacao);
        var renda = breakdown.For(CityParameterType.Renda).Single(c => c.Source == ContributionSource.Interaction && c.From == CityParameterType.Populacao);
        Assert.That(energia.Amount, Is.LessThan(0f));
        Assert.That(renda.Amount, Is.GreaterThan(0f));
    }

    [Test]
    public void ExcessCorrectionAndDeriva_AreSeparateContributions()
    {
        var interactions = new InteractionMatrix(50f, 1f, 25f, 75f, 1.6f, 0.5f, 0.6f, 0.1f);
        var stats = CityStatsTestFactory.Build(interactions, customize: configs =>
        {
            var saude = configs[CityParameterType.Saude];
            saude.InitialValue = 85f;
            saude.Deriva = -1.1f;
            configs[CityParameterType.Saude] = saude;
        });
        var breakdown = new TurnBreakdown(1);
        stats.Recorder = breakdown;

        stats.ResolveTurn();

        Assert.That(breakdown.Sum(CityParameterType.Saude, ContributionSource.Deriva), Is.EqualTo(-1.1f).Within(Tolerance));
        Assert.That(breakdown.Sum(CityParameterType.Saude, ContributionSource.ExcessCorrection), Is.EqualTo(-1f).Within(Tolerance));
    }

    [Test]
    public void Clone_DoesNotCarryTheRecorder()
    {
        var stats = CityStatsTestFactory.Build();
        stats.Recorder = new TurnBreakdown(1);

        var clone = stats.Clone();
        clone.ApplyModifier(new StatModifier { Parameter = CityParameterType.Saude, Amount = 6f });

        Assert.That(clone.Recorder, Is.Null);
        Assert.That(stats.Recorder.Contributions, Is.Empty);
    }

    [Test]
    public void ZeroAmounts_AreNotRecorded()
    {
        var breakdown = new TurnBreakdown(1);
        breakdown.Add(CityParameterType.Saude, ContributionSource.Deriva, 0f);

        Assert.That(breakdown.Contributions, Is.Empty);
    }

    private static void SetInitial(Dictionary<CityParameterType, CityParameterConfig> configs, CityParameterType parameter, float value)
    {
        var config = configs[parameter];
        config.InitialValue = value;
        configs[parameter] = config;
    }
}
