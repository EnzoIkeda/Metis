using Metis.Simulator;
using Metis.Simulator.Policies;

namespace Metis.Simulator.Tests;

public class AbilityTests
{
    private static SimCard Search(float cost = 0f) => new SimCard { Id = "Busca", Cost = cost, Ability = CardAbility.SearchDeck };
    private static SimCard Reveal(float cost = 0f) => new SimCard { Id = "Revela", Cost = cost, Ability = CardAbility.RevealHand };

    [Test]
    public void Search_IsAFreeActionFollowedByTheTurnPlay()
    {
        // Tudo na mao e busca ou carta fraca; a forte so vem pela busca.
        var weak = TestData.Card("Fraca", (CityParameterType.Saude, 1f));
        var strong = TestData.Card("Forte", (CityParameterType.Saude, 20f));
        // Parametros perto do critico, senao o teto de margem da nota da gulosa empata as duas cartas.
        var low = TestData.NeutralParameters().Select(config =>
        {
            config.InitialValue = 20f;
            return config;
        }).ToList();
        var data = TestData.Build(cards: new[] { Search(), weak, strong }, parameters: low, victoryTurnCount: 6);
        var setup = new PhaseSetup { Data = data, Archetype = CardArchetype.Geral };

        var game = PhaseSimulator.Run(setup, new GreedyPolicy(), new GameSeeds(4, 0));

        var turnsWithSearch = game.Turns.Where(turn => turn.FreeActionIds.Contains("Busca")).ToList();
        Assume.That(turnsWithSearch, Is.Not.Empty, "alguma mao deveria ter a busca sem a forte");
        Assert.That(turnsWithSearch, Has.All.Property(nameof(TurnRecord.CardId)).Not.Empty, "a busca nao gasta a jogada do turno");
        Assert.That(turnsWithSearch.SelectMany(turn => turn.SearchedIds), Has.All.EqualTo("Forte"));
        Assert.That(turnsWithSearch, Has.All.Property(nameof(TurnRecord.CardId)).EqualTo("Forte"));
    }

    [TestCase("gulosa")]
    [TestCase("equilibrada")]
    [TestCase("mcts")]
    public void InformedPolicies_NeverPayForReveal(string policyName)
    {
        var cards = new[] { Reveal(cost: 3f), TestData.Card("A", (CityParameterType.Saude, 2f)), TestData.Card("B", (CityParameterType.Mobilidade, 3f)) };
        var setup = new PhaseSetup { Data = TestData.Build(cards: cards), Archetype = CardArchetype.Geral, VictoryTurnCountOverride = 5 };
        var policy = SimulationRunner.CreatePolicy(policyName, new SimulationOptions { MctsIterations = 30 });

        var game = PhaseSimulator.Run(setup, policy, new GameSeeds(2, 0));

        Assert.That(game.Turns.SelectMany(turn => turn.FreeActionIds), Has.No.Member("Revela"));
    }

    [Test]
    public void UninformedRevealingMcts_RevealsWheneverItCan()
    {
        var cards = new[] { Reveal(), TestData.Card("A", (CityParameterType.Saude, 2f)), TestData.Card("B", (CityParameterType.Mobilidade, 3f)) };
        var setup = new PhaseSetup { Data = TestData.Build(cards: cards), Archetype = CardArchetype.Geral, VictoryTurnCountOverride = 5 };
        var policy = SimulationRunner.CreatePolicy("mcts_desinformado_revela", new SimulationOptions { MctsIterations = 30 });

        var game = PhaseSimulator.Run(setup, policy, new GameSeeds(2, 0));

        var handsWithReveal = game.Turns.Where(turn => turn.PlayableIds.Contains("Revela")).ToList();
        Assume.That(handsWithReveal, Is.Not.Empty);
        Assert.That(handsWithReveal, Has.All.Matches<TurnRecord>(turn => turn.FreeActionIds.Contains("Revela")));
    }

    [Test]
    public void FiniteDeck_FirstTurnsDrawEveryCardOnce()
    {
        var cards = Enumerable.Range(0, 10).Select(i => TestData.Card($"C{i}")).ToArray();
        var setup = new PhaseSetup { Data = TestData.Build(cards: cards), Archetype = CardArchetype.Geral, FiniteDeck = true, VictoryTurnCountOverride = 2 };

        var game = PhaseSimulator.Run(setup, new RandomPolicy(), new GameSeeds(3, 0));

        var seen = game.Turns.SelectMany(turn => turn.PlayableIds).ToList();
        Assert.That(seen, Is.Unique);
        Assert.That(seen, Has.Count.EqualTo(10));
    }

    [Test]
    public void SearchWithNothingToBring_IsNotPlayable()
    {
        var data = TestData.Build(cards: new[] { Search() });
        var hand = new CardHand<SimCard>(data.Cards, new Random(0));
        var stats = new CityStats(data.BuildParameterConfigs(null));
        hand.Draw(3, stats);

        Assert.That(PhaseSimulator.Playable(hand, stats), Is.Empty);
    }

    [TestCase("gulosa")]
    [TestCase("equilibrada")]
    public void HandWithOnlyAbilityCards_StillMakesAChoiceAndPassesTheTurn(string policyName)
    {
        var setup = new PhaseSetup { Data = TestData.Build(cards: new[] { Reveal() }), Archetype = CardArchetype.Geral, VictoryTurnCountOverride = 2 };

        var game = PhaseSimulator.Run(setup, SimulationRunner.CreatePolicy(policyName), new GameSeeds(1, 0));

        Assert.That(game.Turns, Has.All.Property(nameof(TurnRecord.CardId)).Empty);
        Assert.That(game.Turns[0].FreeActionIds, Has.Member("Revela"));
    }

    [Test]
    public void PermanentReveal_IsPlayedOnlyOncePerPhase()
    {
        var cards = new[]
        {
            new SimCard { Id = "Revela", Ability = CardAbility.RevealHand, Copies = 2 },
            TestData.Card("A", (CityParameterType.Saude, 2f)),
            TestData.Card("B", (CityParameterType.Mobilidade, 3f)),
        };
        var data = TestData.Build(cards: cards);
        var policy = SimulationRunner.CreatePolicy("mcts_desinformado_revela", new SimulationOptions { MctsIterations = 20 });
        PhaseSetup Setup(bool permanent) => new PhaseSetup
        {
            Data = data,
            Archetype = CardArchetype.Geral,
            FiniteDeck = true,
            VictoryTurnCountOverride = 6,
            HandSizeOverride = 2,
            PermanentReveal = permanent,
        };

        var permanent = PhaseSimulator.Run(Setup(true), policy, new GameSeeds(3, 0));
        var perTurn = PhaseSimulator.Run(Setup(false), policy, new GameSeeds(3, 0));

        Assert.That(permanent.Turns.Sum(turn => turn.FreeActionIds.Count(id => id == "Revela")), Is.EqualTo(1));
        Assert.That(perTurn.Turns.Sum(turn => turn.FreeActionIds.Count(id => id == "Revela")), Is.GreaterThan(1));
    }
}
