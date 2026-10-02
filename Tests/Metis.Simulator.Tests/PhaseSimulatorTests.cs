using Metis.Simulator;
using Metis.Simulator.Policies;

namespace Metis.Simulator.Tests;

public class PhaseSimulatorTests
{
    private static BalanceData VariedData()
    {
        var cards = Enumerable.Range(0, 12)
            .Select(i => TestData.Card($"C{i}", (CityParameterType.Renda, i - 6), (CityParameterType.Saude, 6 - i)))
            .ToList();
        var events = Enumerable.Range(0, 8)
            .Select(i => new SimEvent
            {
                Id = $"E{i}",
                StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Mobilidade, Amount = i - 4 } },
            })
            .ToList();
        return TestData.Build(cards, events);
    }

    [TestCase("aleatoria")]
    [TestCase("gulosa")]
    [TestCase("equilibrada")]
    public void Run_SameSeeds_ProducesIdenticalGame(string policyName)
    {
        var setup = TestData.Setup(VariedData());
        var policy = SimulationRunner.CreatePolicy(policyName);

        var first = PhaseSimulator.Run(setup, policy, new GameSeeds(1, 7));
        var second = PhaseSimulator.Run(setup, policy, new GameSeeds(1, 7));

        Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
        Assert.That(second.Turns.Select(turn => turn.CardId), Is.EqualTo(first.Turns.Select(turn => turn.CardId)));
        Assert.That(second.Turns.Select(turn => turn.EventId), Is.EqualTo(first.Turns.Select(turn => turn.EventId)));
        Assert.That(second.Turns.Last().Values, Is.EqualTo(first.Turns.Last().Values));
    }

    [Test]
    public void Run_DifferentGameIndexes_ProduceDifferentGames()
    {
        var setup = TestData.Setup(VariedData());
        var policy = new RandomPolicy();

        var plays = Enumerable.Range(0, 5)
            .Select(index => string.Join(",", PhaseSimulator.Run(setup, policy, new GameSeeds(1, index)).Turns.Select(turn => turn.CardId)))
            .ToList();

        Assert.That(plays.Distinct().Count(), Is.GreaterThan(1));
    }

    [Test]
    public void Run_NeutralCity_SurvivesExactlyTheVictoryTurnCount()
    {
        var setup = new PhaseSetup { Data = TestData.Build(), Archetype = CardArchetype.Industria, VictoryTurnCountOverride = 7 };

        var game = PhaseSimulator.Run(setup, new RandomPolicy(), new GameSeeds(1, 0));

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Victory));
        Assert.That(game.TurnsPlayed, Is.EqualTo(7));
        Assert.That(game.Turns.Select(turn => turn.CardId), Has.All.EqualTo("Neutra"));
        Assert.That(game.Turns[0].PlayableIds, Is.EqualTo(Enumerable.Repeat("Neutra", 5)));
    }

    [Test]
    public void Run_EventThatCrashesWellBeing_EndsInGameOverOnThatTurn()
    {
        var crash = new SimEvent
        {
            Id = "Colapso",
            MinTurn = 3,
            StatEffects = new[] { new StatModifier { Parameter = CityParameterType.BemEstar, Amount = -100f } },
        };
        var setup = TestData.Setup(TestData.Build(events: new[] { crash }));

        var game = PhaseSimulator.Run(setup, new RandomPolicy(), new GameSeeds(1, 0));

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.GameOver));
        Assert.That(game.TurnsPlayed, Is.EqualTo(3));
        Assert.That(game.Turns[0].EventId, Is.Empty);
        Assert.That(game.Turns[2].EventId, Is.EqualTo("Colapso"));
    }

    [Test]
    public void Run_WellBeingEffectOfCard_PersistsThroughResolution()
    {
        var card = TestData.Card("SoBemEstar", (CityParameterType.BemEstar, 10f));
        var setup = new PhaseSetup { Data = TestData.Build(cards: new[] { card }), Archetype = CardArchetype.Geral, VictoryTurnCountOverride = 2 };

        var game = PhaseSimulator.Run(setup, new RandomPolicy(), new GameSeeds(1, 0));

        Assert.That(game.Turns[0].Values[(int)CityParameterType.BemEstar], Is.EqualTo(TestData.NeutralValue + 10f));
        Assert.That(game.Turns[1].Values[(int)CityParameterType.BemEstar], Is.EqualTo(TestData.NeutralValue + 20f));
    }

    [Test]
    public void Run_NoPlayableCard_PassesTheTurnAndCountsIt()
    {
        var locked = new SimCard { Id = "Travada", RequiredPesquisa = 90f };
        var setup = new PhaseSetup { Data = TestData.Build(cards: new[] { locked }), VictoryTurnCountOverride = 3 };

        var game = PhaseSimulator.Run(setup, new GreedyPolicy(), new GameSeeds(1, 0));

        Assert.That(game.Turns, Has.All.Property(nameof(TurnRecord.PlayableCount)).EqualTo(0));
        Assert.That(game.Turns, Has.All.Property(nameof(TurnRecord.CardId)).Empty);
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Victory));
    }

    [Test]
    public void Run_RecordsFirstTurnEachResearchTierIsReached()
    {
        var research = TestData.Card("Pesquisa", (CityParameterType.Pesquisa, 10f));
        var locked = new SimCard { Id = "Tier", RequiredPesquisa = 65f };
        var setup = new PhaseSetup { Data = TestData.Build(cards: new[] { research, locked }), VictoryTurnCountOverride = 3 };

        var game = PhaseSimulator.Run(setup, new GreedyPolicy(), new GameSeeds(1, 0));

        Assert.That(game.TierReachedTurn, Is.EqualTo(new[] { 2 }), "Pesquisa comeca em 50 e sobe 10 por turno, passa de 65 no turno 2");
    }

    [Test]
    public void Run_LoadedAdvantageIsAppliedBeforeFirstTurn()
    {
        var data = new BalanceData
        {
            Parameters = TestData.NeutralParameters(),
            Interaction = new SimInteraction { EscalaGlobal = 0f },
            Cards = new[] { TestData.Card("Neutra") },
            Advantages = new[] { new SimAdvantage { Id = "Bonus", StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Renda, Amount = 8f } } } },
        };
        var setup = new PhaseSetup { Data = data, LoadedAdvantageIds = new[] { "Bonus" }, VictoryTurnCountOverride = 1 };

        var game = PhaseSimulator.Run(setup, new RandomPolicy(), new GameSeeds(1, 0));

        Assert.That(game.Turns[0].Values[(int)CityParameterType.Renda], Is.EqualTo(TestData.NeutralValue + 8f));
    }
}
