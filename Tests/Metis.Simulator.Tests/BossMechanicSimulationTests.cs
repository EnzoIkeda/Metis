using Metis.Simulator;
using Metis.Simulator.Policies;

namespace Metis.Simulator.Tests;

// Regras especiais da segunda leva no simulador: o avanco de turno do planejamento repete a fase turno a turno.
public class BossMechanicSimulationTests
{
    private const string ExtraId = "Termos";

    private static SimBoss Boss(BossMechanic mechanic, float amount, CityParameterType parameter)
    {
        return new SimBoss
        {
            Id = mechanic.ToString(),
            TargetArchetype = CardArchetype.Industria,
            MomentumPerTurn = 2,
            Mechanic = mechanic,
            MechanicAmount = amount,
            MechanicCardId = mechanic == BossMechanic.ExtraCards ? ExtraId : "",
            BossCards = Enumerable.Range(0, 6).Select(i => new SimBossCard
            {
                Id = $"Golpe{i}",
                MomentumCost = 2 + i % 2,
                Effects = new[] { new StatModifier { Parameter = parameter, Amount = -3f } },
            }).ToList(),
        };
    }

    private static BalanceData Data(SimBoss boss)
    {
        var cards = Enumerable.Range(0, 10)
            .Select(i => TestData.Card($"C{i}", (CityParameterType.Saude, i - 4), (CityParameterType.Mobilidade, 4 - i)))
            .Select((card, i) => new SimCard { Id = card.Id, StatEffects = card.StatEffects, Cost = i % 3 })
            .Append(new SimCard { Id = ExtraId, IsExtraOnly = true })
            .ToList();
        var events = Enumerable.Range(0, 6)
            .Select(i => new SimEvent { Id = $"E{i}", StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Energia, Amount = i - 3 } } })
            .ToList();
        var basis = TestData.Build(cards, events);
        return new BalanceData
        {
            Rules = basis.Rules,
            Parameters = basis.Parameters,
            Interaction = basis.Interaction,
            Cards = basis.Cards,
            Events = basis.Events,
            Bosses = new[] { boss },
        };
    }

    private static IEnumerable<TestCaseData> Mechanics()
    {
        yield return new TestCaseData(BossMechanic.CascadeSpread, 0.5f, CityParameterType.Energia).SetName("Cascata");
        yield return new TestCaseData(BossMechanic.ExtraCards, 2f, CityParameterType.Seguranca).SetName("CartasExtras");
        yield return new TestCaseData(BossMechanic.CostIncrease, 0.5f, CityParameterType.Renda).SetName("Encarecimento");
    }

    [TestCaseSource(nameof(Mechanics))]
    public void ForwardModel_ReproducesThePhaseSimulatorTurnByTurn(BossMechanic mechanic, float amount, CityParameterType parameter)
    {
        var data = Data(Boss(mechanic, amount, parameter));
        var setup = new PhaseSetup { Data = data, Archetype = CardArchetype.Industria, Boss = data.Bosses[0], FiniteDeck = true };
        var seeds = new GameSeeds(3, 11);
        var policy = new BalancedPolicy();
        var expected = PhaseSimulator.Run(setup, policy, seeds);

        var stats = new CityStats(data.BuildParameterConfigs(null), data.BuildInteractionMatrix(), data.Interaction.PenalidadeColapso);
        var deck = DeckBuilder.Build(data.Cards, setup.Archetype, setup.LoadedCardIds);
        var hand = new CardHand<SimCard>(deck, new Random(seeds.Hand), finiteDeck: true);
        var pool = new RandomEventPool<SimEvent>(data.Events, new Random(seeds.Events));
        var boss = new BossEncounter(data.Bosses[0], new Random(seeds.Boss));
        var extra = PhaseSimulator.BossExtraCard(data, data.Bosses[0], 0);
        var policyRandom = new Random(seeds.Policy);
        var outcome = GameOutcome.None;
        var turn = 1;
        while (true)
        {
            var playable = ForwardModel.DrawPlayable(hand, setup.HandSize, stats);
            Assert.That(playable.Select(card => card.Id), Is.EqualTo(expected.Turns[turn - 1].PlayableIds), $"jogaveis no turno {turn}");
            var card = playable.Count > 0
                ? policy.Choose(new DecisionContext { Stats = stats, PlayableCards = playable, Data = data, Random = policyRandom, TurnIndex = turn, CostOffset = hand.CostOffset })
                : null;
            Assert.That(card?.Id ?? "", Is.EqualTo(expected.Turns[turn - 1].CardId), $"turno {turn}");
            if (card != null)
                hand.TryPlay(card, CityStatsForCheck(stats));
            outcome = ForwardModel.StepTurn(stats, card, turn, setup.VictoryTurnCount, pool, boss: boss, hand: hand, bossExtraCard: extra);
            Assert.That(PhaseSimulator.Parameters.Select(stats.GetValue), Is.EqualTo(expected.Turns[turn - 1].Values), $"valores no turno {turn}");
            if (outcome != GameOutcome.None)
                break;
            turn++;
        }

        Assert.That(outcome, Is.EqualTo(expected.Outcome));
        Assert.That(expected.Turns.Any(record => record.BossCardId.Length > 0), Is.True);
    }

    // A jogada tira a carta da mao numa copia dos parametros, porque o avanco de turno ja cobra a carta nos parametros reais.
    private static CityStats CityStatsForCheck(CityStats stats) => stats.Clone();

    [Test]
    public void ExtraCards_EndUpInTheHandsTheSimulatorDraws()
    {
        var data = Data(Boss(BossMechanic.ExtraCards, 2f, CityParameterType.Seguranca));
        var setup = new PhaseSetup { Data = data, Archetype = CardArchetype.Industria, Boss = data.Bosses[0], FiniteDeck = true };

        var game = PhaseSimulator.Run(setup, new BalancedPolicy(), new GameSeeds(4, 1));

        Assert.That(game.Turns.Any(record => record.PlayableIds.Contains(ExtraId)), Is.True);
    }

    [Test]
    public void CostIncrease_DrainsMoreIncomeThanWithoutTheMechanic()
    {
        var raising = Data(Boss(BossMechanic.CostIncrease, 1f, CityParameterType.Saude));
        var plain = Data(Boss(BossMechanic.None, 0f, CityParameterType.Saude));
        var seeds = new GameSeeds(6, 2);

        var withIncrease = PhaseSimulator.Run(new PhaseSetup { Data = raising, Archetype = CardArchetype.Industria, Boss = raising.Bosses[0], FiniteDeck = true }, new GreedyPolicy(), seeds);
        var without = PhaseSimulator.Run(new PhaseSetup { Data = plain, Archetype = CardArchetype.Industria, Boss = plain.Bosses[0], FiniteDeck = true }, new GreedyPolicy(), seeds);

        var renda = Array.IndexOf(PhaseSimulator.Parameters, CityParameterType.Renda);
        var turns = Math.Min(withIncrease.Turns.Count, without.Turns.Count);
        Assert.That(withIncrease.Turns[turns - 1].Values[renda], Is.LessThan(without.Turns[turns - 1].Values[renda]));
    }

    [TestCaseSource(nameof(Mechanics))]
    public void Mcts_SameSeedsProduceTheSameGame(BossMechanic mechanic, float amount, CityParameterType parameter)
    {
        var data = Data(Boss(mechanic, amount, parameter));
        var setup = new PhaseSetup { Data = data, Archetype = CardArchetype.Industria, Boss = data.Bosses[0], FiniteDeck = true, VictoryTurnCountOverride = 6 };
        var policy = new MctsPolicy(new MctsOptions { Iterations = 60 });

        var first = PhaseSimulator.Run(setup, policy, new GameSeeds(5, 2));
        var second = PhaseSimulator.Run(setup, policy, new GameSeeds(5, 2));

        Assert.That(second.Turns.Select(turn => turn.CardId), Is.EqualTo(first.Turns.Select(turn => turn.CardId)));
    }
}
