using Metis.Simulator;
using Metis.Simulator.Policies;

namespace Metis.Simulator.Tests;

// Fase do chefe no simulador: mesma ordem do jogo, o planejamento enxerga o chefe e a rodada termina nele.
public class BossSimulationTests
{
    private static SimBoss Boss(CardArchetype target = CardArchetype.Industria, float damage = -3f, float growth = 0.1f)
    {
        return new SimBoss
        {
            Id = "Enchente",
            TargetArchetype = target,
            MomentumPerTurn = 2,
            Mechanic = BossMechanic.GrowingDamage,
            MechanicAmount = growth,
            BossCards = Enumerable.Range(0, 6).Select(i => new SimBossCard
            {
                Id = $"Golpe{i}",
                MomentumCost = 2 + i % 3,
                Effects = new[] { new StatModifier { Parameter = (CityParameterType)(i % 2 == 0 ? (int)CityParameterType.Saude : (int)CityParameterType.Mobilidade), Amount = damage } },
            }).ToList(),
        };
    }

    private static BalanceData Data(int victoryTurns = 20, IReadOnlyList<SimBoss> bosses = null)
    {
        var cards = Enumerable.Range(0, 10)
            .Select(i => TestData.Card($"C{i}", (CityParameterType.Saude, i - 4), (CityParameterType.Mobilidade, 4 - i)))
            .ToList();
        var events = Enumerable.Range(0, 6)
            .Select(i => new SimEvent { Id = $"E{i}", StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Energia, Amount = i - 3 } } })
            .ToList();
        var basis = TestData.Build(cards, events, victoryTurnCount: victoryTurns);
        return new BalanceData
        {
            Rules = basis.Rules,
            Parameters = basis.Parameters,
            Interaction = basis.Interaction,
            Cards = basis.Cards,
            Events = basis.Events,
            Bosses = bosses ?? new[] { Boss() },
        };
    }

    [Test]
    public void Phase_BossPlaysAfterTheCardAndIsRecorded()
    {
        var data = Data();
        var setup = new PhaseSetup { Data = data, Archetype = CardArchetype.Industria, Boss = data.Bosses[0] };
        var history = new PhaseHistory();

        var game = PhaseSimulator.Run(setup, new BalancedPolicy(), new GameSeeds(2, 4), history);

        var played = game.Turns.Where(turn => turn.BossCardId.Length > 0).ToList();
        Assert.That(played, Is.Not.Empty);
        Assert.That(history.Turns.Select(turn => turn.BossCardId), Is.EqualTo(game.Turns.Select(turn => turn.BossCardId)));
        Assert.That(history.Turns.All(turn => turn.HasBossState), Is.True);
    }

    [Test]
    public void Phase_WithoutBoss_IsIdenticalWhetherOrNotBossesExist()
    {
        var with = PhaseSimulator.Run(new PhaseSetup { Data = Data(), Archetype = CardArchetype.Industria }, new BalancedPolicy(), new GameSeeds(7, 1));
        var without = PhaseSimulator.Run(new PhaseSetup { Data = Data(bosses: Array.Empty<SimBoss>()), Archetype = CardArchetype.Industria }, new BalancedPolicy(), new GameSeeds(7, 1));

        Assert.That(with.Turns.Select(turn => turn.CardId), Is.EqualTo(without.Turns.Select(turn => turn.CardId)));
        Assert.That(with.Turns.Last().Values, Is.EqualTo(without.Turns.Last().Values));
        Assert.That(with.Turns.All(turn => turn.BossCardId.Length == 0), Is.True);
    }

    [Test]
    public void ForwardModel_ReproducesThePhaseSimulatorTurnByTurn_WithBoss()
    {
        var data = Data();
        var setup = new PhaseSetup { Data = data, Archetype = CardArchetype.Industria, Boss = data.Bosses[0] };
        var seeds = new GameSeeds(3, 11);
        var policy = new BalancedPolicy();
        var expected = PhaseSimulator.Run(setup, policy, seeds);

        var stats = new CityStats(data.BuildParameterConfigs(null), data.BuildInteractionMatrix(), data.Interaction.PenalidadeColapso);
        var deck = DeckBuilder.Build(data.Cards, setup.Archetype, setup.LoadedCardIds);
        var hand = new CardHand<SimCard>(deck, new Random(seeds.Hand));
        var pool = new RandomEventPool<SimEvent>(data.Events, new Random(seeds.Events));
        var boss = new BossEncounter(data.Bosses[0], new Random(seeds.Boss));
        var policyRandom = new Random(seeds.Policy);
        var outcome = GameOutcome.None;
        var turn = 1;
        while (true)
        {
            var playable = ForwardModel.DrawPlayable(hand, setup.HandSize, stats);
            var card = policy.Choose(new DecisionContext { Stats = stats, PlayableCards = playable, Data = data, Random = policyRandom, TurnIndex = turn });
            Assert.That(card.Id, Is.EqualTo(expected.Turns[turn - 1].CardId), $"turno {turn}");
            var bossNext = boss.PlaysThisTurn ? boss.Next.Id : "";
            outcome = ForwardModel.StepTurn(stats, card, turn, setup.VictoryTurnCount, pool, boss: boss);
            Assert.That(bossNext, Is.EqualTo(expected.Turns[turn - 1].BossCardId), $"chefe no turno {turn}");
            Assert.That(PhaseSimulator.Parameters.Select(stats.GetValue), Is.EqualTo(expected.Turns[turn - 1].Values), $"valores no turno {turn}");
            if (outcome != GameOutcome.None)
                break;
            turn++;
        }

        Assert.That(outcome, Is.EqualTo(expected.Outcome));
    }

    [Test]
    public void Mcts_WithBoss_SameSeedsProduceTheSameGame()
    {
        var data = Data(victoryTurns: 6);
        var setup = new PhaseSetup { Data = data, Archetype = CardArchetype.Industria, Boss = data.Bosses[0], FiniteDeck = true };
        var policy = new MctsPolicy(new MctsOptions { Iterations = 60 });

        var first = PhaseSimulator.Run(setup, policy, new GameSeeds(5, 2));
        var second = PhaseSimulator.Run(setup, policy, new GameSeeds(5, 2));

        Assert.That(second.Turns.Select(turn => turn.CardId), Is.EqualTo(first.Turns.Select(turn => turn.CardId)));
        Assert.That(second.Turns.Select(turn => turn.BossCardId), Is.EqualTo(first.Turns.Select(turn => turn.BossCardId)));
    }

    [Test]
    public void Run_DeckWithBoss_PlaysTheBossAfterTheLastPhase()
    {
        var data = Data(victoryTurns: 3, bosses: new[] { Boss(damage: -0.5f) });
        var setup = new RunSetup { Data = data, Archetype = CardArchetype.Industria, PhaseCount = 3 };

        var run = RunSimulator.Run(setup, new BalancedPolicy(), new RandomRewardPolicy(), 1, 0);

        Assert.That(run.BossId, Is.EqualTo("Enchente"));
        Assert.That(run.ReachedBoss, Is.True);
        Assert.That(run.BeatBoss, Is.True);
        Assert.That(run.Phases, Has.Count.EqualTo(4));
        Assert.That(run.Phases.Last().IsBoss, Is.True);
        Assert.That(run.Phases.Last().OfferedIds, Is.Empty, "vencer o chefe nao da recompensa");
        Assert.That(run.Phases.Last().Game.Turns.Any(turn => turn.BossCardId.Length > 0), Is.True);
    }

    [Test]
    public void Run_NoBossOrDeckWithoutBoss_IsIdenticalToTheRunBefore()
    {
        var withBoss = Data(victoryTurns: 3, bosses: new[] { Boss(damage: -0.5f) });
        var noBosses = Data(victoryTurns: 3, bosses: Array.Empty<SimBoss>());

        var reference = RunSimulator.Run(new RunSetup { Data = noBosses, Archetype = CardArchetype.Industria }, new BalancedPolicy(), new RandomRewardPolicy(), 4, 2);
        var flagOff = RunSimulator.Run(new RunSetup { Data = withBoss, Archetype = CardArchetype.Industria, IncludeBoss = false }, new BalancedPolicy(), new RandomRewardPolicy(), 4, 2);
        var otherDeck = RunSimulator.Run(new RunSetup { Data = withBoss, Archetype = CardArchetype.Automacao }, new BalancedPolicy(), new RandomRewardPolicy(), 4, 2);
        var otherReference = RunSimulator.Run(new RunSetup { Data = noBosses, Archetype = CardArchetype.Automacao }, new BalancedPolicy(), new RandomRewardPolicy(), 4, 2);

        static IEnumerable<string> Summary(RunRecord run) => run.Phases.Select(phase =>
            $"{phase.Phase}|{phase.PresetId}|{phase.Game.Outcome}|{string.Join(',', phase.Game.Turns.Select(turn => turn.CardId))}|{phase.ChosenId}");

        Assert.That(Summary(flagOff), Is.EqualTo(Summary(reference)));
        Assert.That(Summary(otherDeck), Is.EqualTo(Summary(otherReference)));
        Assert.That(flagOff.ReachedBoss || otherDeck.ReachedBoss, Is.False);
    }

    [Test]
    public void Run_PhasesBeforeTheBossMatchTheRunWithoutIt()
    {
        var withBoss = Data(victoryTurns: 3, bosses: new[] { Boss(damage: -0.5f) });
        var noBosses = Data(victoryTurns: 3, bosses: Array.Empty<SimBoss>());

        var reference = RunSimulator.Run(new RunSetup { Data = noBosses, Archetype = CardArchetype.Industria }, new BalancedPolicy(), new RandomRewardPolicy(), 9, 3);
        var run = RunSimulator.Run(new RunSetup { Data = withBoss, Archetype = CardArchetype.Industria }, new BalancedPolicy(), new RandomRewardPolicy(), 9, 3);

        var before = run.Phases.Where(phase => phase.IsBoss == false).Select(phase => $"{phase.PresetId}|{phase.Game.Outcome}|{phase.ChosenId}");
        Assert.That(before, Is.EqualTo(reference.Phases.Select(phase => $"{phase.PresetId}|{phase.Game.Outcome}|{phase.ChosenId}")));
    }
}
