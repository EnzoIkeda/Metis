using System.Collections.Generic;

namespace Metis.Core.Tests;

public class AlmanacStateTests
{
    [Test]
    public void Unlock_NewId_ReturnsTrueOnlyOnce()
    {
        var state = new AlmanacState();

        Assert.That(state.Unlock("SmartGrid"), Is.True);
        Assert.That(state.Unlock("SmartGrid"), Is.False, "desbloquear de novo nao conta");
        Assert.That(state.UnlockedCount, Is.EqualTo(1));
        Assert.That(state.IsUnlocked("SmartGrid"), Is.True);
    }

    [Test]
    public void Unlock_BlankId_IsIgnored()
    {
        var state = new AlmanacState();

        Assert.That(state.Unlock(""), Is.False);
        Assert.That(state.Unlock("   "), Is.False);
        Assert.That(state.Unlock(null), Is.False);
        Assert.That(state.UnlockedCount, Is.Zero);
        Assert.That(state.IsUnlocked(null), Is.False);
    }

    [Test]
    public void SerializeAndParse_RoundTrip_KeepsTheSameIds()
    {
        var state = new AlmanacState();
        state.Unlock("Conceito_Colapso");
        state.Unlock("CardVLT");
        state.Unlock("EventEnchente");

        var restored = AlmanacState.Parse(state.Serialize());

        Assert.That(restored.UnlockedCount, Is.EqualTo(3));
        Assert.That(restored.IsUnlocked("Conceito_Colapso"), Is.True);
        Assert.That(restored.IsUnlocked("CardVLT"), Is.True);
        Assert.That(restored.IsUnlocked("EventEnchente"), Is.True);
    }

    [Test]
    public void Serialize_SameSetInAnyOrder_GivesTheSameText()
    {
        var first = new AlmanacState();
        first.Unlock("B");
        first.Unlock("A");
        var second = new AlmanacState();
        second.Unlock("A");
        second.Unlock("B");

        Assert.That(first.Serialize(), Is.EqualTo(second.Serialize()));
    }

    [Test]
    public void Parse_EmptyOrNullOrBlankLines_GivesEmptyOrSkipsThem()
    {
        Assert.That(AlmanacState.Parse(null).UnlockedCount, Is.Zero);
        Assert.That(AlmanacState.Parse("").UnlockedCount, Is.Zero);
        Assert.That(AlmanacState.Parse("A\n\n  \nB\r").UnlockedCount, Is.EqualTo(2));
        Assert.That(AlmanacState.Parse("A\n\n  \nB\r").IsUnlocked("B"), Is.True, "\\r de fim de linha e descartado");
    }

    [Test]
    public void Clear_RemovesEverything()
    {
        var state = AlmanacState.Parse("A\nB");

        state.Clear();

        Assert.That(state.UnlockedCount, Is.Zero);
    }
}

public class AlmanacTriggersTests
{
    private static InteractionMatrix Calibrated() => new InteractionMatrix(50f, 1f, 25f, 75f, 1.6f, 0.5f, 0.6f, 0.1f);

    private static readonly FakeCard[] Pool =
    {
        new FakeCard { Id = "Basica", Tier = CardTier.Basica, RequiredPesquisa = 0f },
        new FakeCard { Id = "Digital1", Tier = CardTier.CidadeDigital, RequiredPesquisa = 18f },
        new FakeCard { Id = "Digital2", Tier = CardTier.CidadeDigital, RequiredPesquisa = 15f },
        new FakeCard { Id = "Conectada", Tier = CardTier.CidadeConectada, RequiredPesquisa = 30f },
        new FakeCard { Id = "Smart", Tier = CardTier.SmartCity, RequiredPesquisa = 45f },
    };

    [Test]
    public void TierGates_UsesTheLowestRequirementOfEachTier_AndSkipsBasic()
    {
        var gates = AlmanacTriggers.TierGates(Pool);

        Assert.That(gates.ContainsKey(CardTier.Basica), Is.False);
        Assert.That(gates[CardTier.CidadeDigital], Is.EqualTo(15f));
        Assert.That(gates[CardTier.CidadeConectada], Is.EqualTo(30f));
        Assert.That(gates[CardTier.SmartCity], Is.EqualTo(45f));
    }

    [Test]
    public void TierGates_NullPool_IsEmpty()
    {
        Assert.That(AlmanacTriggers.TierGates(null), Is.Empty);
    }

    [Test]
    public void ForParameter_HealthyValue_TriggersNothing()
    {
        var stats = CityStatsTestFactory.Build(Calibrated());

        Assert.That(AlmanacTriggers.ForParameter(stats, CityParameterType.Saude, null), Is.Empty);
    }

    [Test]
    public void ForParameter_LowValue_TriggersCrisis()
    {
        var stats = CityStatsTestFactory.Build(Calibrated(), customize: configs => SetInitial(configs, CityParameterType.Saude, 20f));

        Assert.That(AlmanacTriggers.ForParameter(stats, CityParameterType.Saude, null), Has.Member(KnowledgeUnlockTrigger.FirstCrisis));
    }

    [Test]
    public void ForParameter_HighValue_TriggersExcess()
    {
        var stats = CityStatsTestFactory.Build(Calibrated(), customize: configs => SetInitial(configs, CityParameterType.Renda, 80f));

        var triggers = AlmanacTriggers.ForParameter(stats, CityParameterType.Renda, null);

        Assert.That(triggers, Has.Member(KnowledgeUnlockTrigger.FirstExcess));
        Assert.That(triggers, Has.No.Member(KnowledgeUnlockTrigger.FirstCrisis));
    }

    [Test]
    public void ForParameter_BelowCriticalLevel_TriggersCollapse()
    {
        var stats = CityStatsTestFactory.Build(Calibrated(), customize: configs =>
        {
            SetInitial(configs, CityParameterType.Energia, 10f);
            var config = configs[CityParameterType.Energia];
            config.CriticalLevel = 15f;
            configs[CityParameterType.Energia] = config;
        });

        Assert.That(AlmanacTriggers.ForParameter(stats, CityParameterType.Energia, null), Has.Member(KnowledgeUnlockTrigger.FirstCollapse));
    }

    [Test]
    public void ForParameter_Pesquisa_TriggersEveryTierAlreadyReached()
    {
        var gates = AlmanacTriggers.TierGates(Pool);
        var stats = CityStatsTestFactory.Build(Calibrated(), customize: configs => SetInitial(configs, CityParameterType.Pesquisa, 31f));

        var triggers = AlmanacTriggers.ForParameter(stats, CityParameterType.Pesquisa, gates);

        Assert.That(triggers, Has.Member(KnowledgeUnlockTrigger.TierCidadeDigital));
        Assert.That(triggers, Has.Member(KnowledgeUnlockTrigger.TierCidadeConectada));
        Assert.That(triggers, Has.No.Member(KnowledgeUnlockTrigger.TierSmartCity));
    }

    [Test]
    public void ForParameter_OtherParameterHigh_DoesNotTriggerTiers()
    {
        var gates = AlmanacTriggers.TierGates(Pool);
        var stats = CityStatsTestFactory.Build(Calibrated(), customize: configs => SetInitial(configs, CityParameterType.Renda, 60f));

        var triggers = AlmanacTriggers.ForParameter(stats, CityParameterType.Renda, gates);

        Assert.That(triggers, Has.No.Member(KnowledgeUnlockTrigger.TierCidadeDigital));
    }

    [Test]
    public void ForParameter_NullStats_IsEmpty()
    {
        Assert.That(AlmanacTriggers.ForParameter(null, CityParameterType.Renda, null), Is.Empty);
    }

    [Test]
    public void ForParameter_DoesNotChangeAnyValue()
    {
        var stats = CityStatsTestFactory.Build(Calibrated(), customize: configs => SetInitial(configs, CityParameterType.Pesquisa, 50f));
        var before = new Dictionary<CityParameterType, float>();
        foreach (CityParameterType parameter in System.Enum.GetValues(typeof(CityParameterType)))
            before[parameter] = stats.GetValue(parameter);

        foreach (CityParameterType parameter in System.Enum.GetValues(typeof(CityParameterType)))
            AlmanacTriggers.ForParameter(stats, parameter, AlmanacTriggers.TierGates(Pool));

        foreach (var pair in before)
            Assert.That(stats.GetValue(pair.Key), Is.EqualTo(pair.Value), pair.Key.ToString());
    }

    [TestCase(CardTier.Basica, KnowledgeUnlockTrigger.None)]
    [TestCase(CardTier.CidadeDigital, KnowledgeUnlockTrigger.TierCidadeDigital)]
    [TestCase(CardTier.CidadeConectada, KnowledgeUnlockTrigger.TierCidadeConectada)]
    [TestCase(CardTier.SmartCity, KnowledgeUnlockTrigger.TierSmartCity)]
    public void ForTier_MapsEachTier(CardTier tier, KnowledgeUnlockTrigger expected)
    {
        Assert.That(AlmanacTriggers.ForTier(tier), Is.EqualTo(expected));
    }

    [TestCase(GameOutcome.None, KnowledgeUnlockTrigger.None)]
    [TestCase(GameOutcome.Victory, KnowledgeUnlockTrigger.FirstVictory)]
    [TestCase(GameOutcome.GameOver, KnowledgeUnlockTrigger.FirstDefeat)]
    public void ForOutcome_MapsEachOutcome(GameOutcome outcome, KnowledgeUnlockTrigger expected)
    {
        Assert.That(AlmanacTriggers.ForOutcome(outcome), Is.EqualTo(expected));
    }

    private static void SetInitial(Dictionary<CityParameterType, CityParameterConfig> configs, CityParameterType parameter, float value)
    {
        var config = configs[parameter];
        config.InitialValue = value;
        configs[parameter] = config;
    }
}
