using Metis.Simulator;
using Metis.Simulator.Policies;

namespace Metis.Simulator.Tests;

public class RunSimulatorTests
{
    private static BalanceData DataWithRewards(IReadOnlyList<SimPreset> presets = null, int optionCount = 3)
    {
        return new BalanceData
        {
            Rules = new SimRules { HandSize = 5, VictoryTurnCount = 3, RewardOptionCount = optionCount },
            Parameters = TestData.NeutralParameters(),
            Interaction = new SimInteraction { EscalaGlobal = 0f },
            Cards = new[] { TestData.Card("Neutra") },
            Advantages = new[]
            {
                new SimAdvantage { Id = "Salva", StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Saude, Amount = 50f }, new StatModifier { Parameter = CityParameterType.Seguranca, Amount = 50f } } },
                new SimAdvantage { Id = "Inutil", StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Pesquisa, Amount = 1f } } },
            },
            Presets = presets ?? Array.Empty<SimPreset>(),
        };
    }

    [Test]
    public void Run_NeutralCity_WinsEveryPhaseAndRecordsRewards()
    {
        var setup = new RunSetup { Data = DataWithRewards(), Archetype = CardArchetype.Industria, PhaseCount = 3 };

        var run = RunSimulator.Run(setup, new RandomPolicy(), new RandomRewardPolicy(), 1, 0);

        Assert.That(run.PhasesWon, Is.EqualTo(3));
        Assert.That(run.Phases, Has.Count.EqualTo(3));
        Assert.That(run.Phases[0].PresetId, Is.EqualTo(SimulationRunner.BaseSetupName));
        Assert.That(run.Phases, Has.All.Property(nameof(PhaseRunRecord.OfferedIds)).Count.EqualTo(3));
    }

    [Test]
    public void Run_OfferedCardAlreadyInDeck_IsCountedAsRedundant()
    {
        var setup = new RunSetup { Data = DataWithRewards(), Archetype = CardArchetype.Industria, PhaseCount = 1 };

        var run = RunSimulator.Run(setup, new RandomPolicy(), new RandomRewardPolicy(), 1, 0);

        Assert.That(run.Phases[0].RedundantOffered, Is.EqualTo(1), "a carta Neutra e Basica, ja esta no baralho");
    }

    [Test]
    public void Run_StopsAtFirstDefeat()
    {
        var lethal = new SimPreset
        {
            Id = "Letal",
            Overrides = new Dictionary<CityParameterType, float>
            {
                [CityParameterType.Saude] = 0f,
                [CityParameterType.Seguranca] = 0f,
                [CityParameterType.Mobilidade] = 0f,
                [CityParameterType.Sustentabilidade] = 0f,
            },
        };
        var data = DataWithRewards(new[] { lethal }, optionCount: 1);
        data = new BalanceData
        {
            Rules = data.Rules,
            Parameters = data.Parameters,
            Interaction = data.Interaction,
            Cards = data.Cards,
            Advantages = new[] { data.Advantages[1] },
            Presets = data.Presets,
        };
        var setup = new RunSetup { Data = data, Archetype = CardArchetype.Industria, PhaseCount = 3 };

        var run = RunSimulator.Run(setup, new RandomPolicy(), new RandomRewardPolicy(), 1, 0);

        Assert.That(run.PhasesWon, Is.EqualTo(1));
        Assert.That(run.Phases, Has.Count.EqualTo(2));
        Assert.That(run.Phases[1].Game.Outcome, Is.EqualTo(GameOutcome.GameOver));
    }

    [Test]
    public void RolloutRewardPolicy_PicksTheRewardThatSavesTheNextPhase()
    {
        var lethal = new SimPreset
        {
            Id = "Letal",
            Overrides = new Dictionary<CityParameterType, float>
            {
                [CityParameterType.Saude] = 0f,
                [CityParameterType.Seguranca] = 0f,
                [CityParameterType.Mobilidade] = 0f,
                [CityParameterType.Sustentabilidade] = 0f,
            },
        };
        var setup = new RunSetup { Data = DataWithRewards(new[] { lethal }), Archetype = CardArchetype.Industria };
        var options = new[]
        {
            new RewardOption { Id = "Inutil", IsCard = false },
            new RewardOption { Id = "Salva", IsCard = false },
            new RewardOption { Id = "Neutra", IsCard = true },
        };

        var chosen = new RolloutRewardPolicy(5).Choose(new RewardContext { Options = options, Setup = setup, Random = new Random(0) });

        Assert.That(chosen.Id, Is.EqualTo("Salva"));
    }
}
