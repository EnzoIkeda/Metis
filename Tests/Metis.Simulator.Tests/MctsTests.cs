using Metis.Simulator;
using Metis.Simulator.Policies;

namespace Metis.Simulator.Tests;

public class MctsTests
{
    private static List<CityParameterConfig> LowParameters(float initialValue)
    {
        return TestData.NeutralParameters().Select(config =>
        {
            config.InitialValue = initialValue;
            return config;
        }).ToList();
    }

    private static DecisionContext Context(BalanceData data, params SimCard[] playable)
    {
        return new DecisionContext
        {
            Data = data,
            Stats = new CityStats(data.BuildParameterConfigs(null), data.BuildInteractionMatrix(), data.Interaction.PenalidadeColapso),
            PlayableCards = playable,
            TurnIndex = 1,
            Random = new Random(0),
            Deck = data.Cards,
            Events = data.Events,
            HandSize = data.Rules.HandSize,
            VictoryTurnCount = data.Rules.VictoryTurnCount,
        };
    }

    [Test]
    public void ForwardModel_ReproducesThePhaseSimulatorTurnByTurn()
    {
        var cards = Enumerable.Range(0, 10)
            .Select(i => TestData.Card($"C{i}", (CityParameterType.Saude, i - 5), (CityParameterType.Mobilidade, 3 - i)))
            .ToList();
        var events = Enumerable.Range(0, 6)
            .Select(i => new SimEvent { Id = $"E{i}", StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Energia, Amount = i - 3 } } })
            .ToList();
        var data = TestData.Build(cards, events);
        var setup = TestData.Setup(data);
        var seeds = new GameSeeds(3, 11);
        var policy = new BalancedPolicy();

        var expected = PhaseSimulator.Run(setup, policy, seeds);

        var stats = new CityStats(data.BuildParameterConfigs(null), data.BuildInteractionMatrix(), data.Interaction.PenalidadeColapso);
        var deck = DeckBuilder.Build(data.Cards, setup.Archetype, setup.LoadedCardIds);
        var hand = new CardHand<SimCard>(deck, new Random(seeds.Hand));
        var pool = new RandomEventPool<SimEvent>(data.Events, new Random(seeds.Events));
        var policyRandom = new Random(seeds.Policy);
        var outcome = GameOutcome.None;
        var turn = 1;
        while (true)
        {
            var playable = ForwardModel.DrawPlayable(hand, setup.HandSize, stats);
            var card = policy.Choose(new DecisionContext { Stats = stats, PlayableCards = playable, Data = data, Random = policyRandom, TurnIndex = turn });
            Assert.That(card.Id, Is.EqualTo(expected.Turns[turn - 1].CardId), $"turno {turn}");
            outcome = ForwardModel.StepTurn(stats, card, turn, setup.VictoryTurnCount, pool);
            if (outcome != GameOutcome.None)
                break;
            turn++;
        }

        Assert.That(outcome, Is.EqualTo(expected.Outcome));
        Assert.That(PhaseSimulator.Parameters.Select(stats.GetValue), Is.EqualTo(expected.Turns.Last().Values));
    }

    [Test]
    public void Mcts_SameSeeds_ProducesIdenticalGame()
    {
        var cards = Enumerable.Range(0, 8)
            .Select(i => TestData.Card($"C{i}", (CityParameterType.Saude, i - 4), (CityParameterType.Seguranca, 2 - i)))
            .ToList();
        var setup = new PhaseSetup { Data = TestData.Build(cards), Archetype = CardArchetype.Geral, VictoryTurnCountOverride = 6 };
        var policy = new MctsPolicy(new MctsOptions { Iterations = 60 });

        var first = PhaseSimulator.Run(setup, policy, new GameSeeds(5, 2));
        var second = PhaseSimulator.Run(setup, policy, new GameSeeds(5, 2));

        Assert.That(second.Turns.Select(turn => turn.CardId), Is.EqualTo(first.Turns.Select(turn => turn.CardId)));
    }

    [Test]
    public void Mcts_AvoidsTheCardThatLosesTheGame()
    {
        var fatal = TestData.Card("Fatal",
            (CityParameterType.Saude, -100f), (CityParameterType.Seguranca, -100f),
            (CityParameterType.Mobilidade, -100f), (CityParameterType.Sustentabilidade, -100f));
        var safe = TestData.Card("Segura");
        var data = TestData.Build(cards: new[] { fatal, safe }, victoryTurnCount: 3);

        var chosen = new MctsPolicy(new MctsOptions { Iterations = 50 }).Choose(Context(data, fatal, safe));

        Assert.That(chosen, Is.SameAs(safe));
    }

    [Test]
    public void Mcts_InformationLevel_ChangesWhatItCanTellApart()
    {
        // So sabendo a direcao, "Pequena" parece melhor (um efeito positivo contra um positivo e um negativo); com os numeros, "Grande" vence.
        var small = TestData.Card("Pequena", (CityParameterType.Saude, 1f));
        var big = TestData.Card("Grande", (CityParameterType.Saude, 30f), (CityParameterType.Mobilidade, -1f));
        var data = TestData.Build(cards: new[] { small, big }, parameters: LowParameters(20f), victoryTurnCount: 1);

        var informed = new MctsPolicy(new MctsOptions { Iterations = 40, Informed = true }).Choose(Context(data, small, big));
        var uninformed = new MctsPolicy(new MctsOptions { Iterations = 40, Informed = false }).Choose(Context(data, small, big));

        Assert.That(informed, Is.SameAs(big));
        Assert.That(uninformed, Is.SameAs(small));
    }

    [Test]
    public void Reward_AnyVictoryBeatsAnyDefeat()
    {
        var data = TestData.Build();
        var context = Context(data);
        var stats = context.Stats;

        var worstVictory = MctsPolicy.VictoryBase;
        var bestDefeat = MctsPolicy.Reward(GameOutcome.GameOver, stats, data.Rules.VictoryTurnCount, context);

        Assert.That(MctsPolicy.Reward(GameOutcome.Victory, stats, data.Rules.VictoryTurnCount, context), Is.GreaterThanOrEqualTo(worstVictory));
        Assert.That(bestDefeat, Is.LessThan(worstVictory));
    }
}
