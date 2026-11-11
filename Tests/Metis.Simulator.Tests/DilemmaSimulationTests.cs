using Metis.Simulator;
using Metis.Simulator.Policies;

namespace Metis.Simulator.Tests;

public class DilemmaSimulationTests
{
    private const CityParameterType Target = CityParameterType.Saude;

    private static StatModifier[] Change(CityParameterType parameter, float amount)
    {
        return new[] { new StatModifier { Parameter = parameter, Amount = amount } };
    }

    private static EventChoiceDefinition Option(float amount, string followUp = "", int delay = 0)
    {
        return new EventChoiceDefinition { Effects = Change(Target, amount), FollowUpEventId = followUp, FollowUpDelay = delay };
    }

    private static SimEvent Dilemma(string id, params EventChoiceDefinition[] options)
    {
        return new SimEvent { Id = id, MaxTurn = 1, Choices = options };
    }

    private static SimEvent Ruin(string id = "Ruina")
    {
        return new SimEvent
        {
            Id = id,
            IsFollowUpOnly = true,
            StatEffects = new[] { CityParameterType.Mobilidade, CityParameterType.Saude, CityParameterType.Seguranca, CityParameterType.Sustentabilidade }
                .Select(parameter => new StatModifier { Parameter = parameter, Amount = -45f }).ToArray(),
        };
    }

    private static List<SimCard> NeutralCards()
    {
        return Enumerable.Range(0, 8).Select(i => TestData.Card($"C{i}", (CityParameterType.Energia, 0f))).ToList();
    }

    private static PhaseSetup Setup(BalanceData data, int turns = 6)
    {
        return new PhaseSetup { Data = data, Archetype = CardArchetype.Geral, VictoryTurnCountOverride = turns };
    }

    private static DecisionContext Context(BalanceData data, CityStats stats, int seed = 1)
    {
        return new DecisionContext { Stats = stats, Data = data, Random = new Random(seed), TurnIndex = 1 };
    }

    private static CityStats StatsOf(BalanceData data)
    {
        return new CityStats(data.BuildParameterConfigs(null), data.BuildInteractionMatrix(), data.Interaction.PenalidadeColapso);
    }

    private sealed class FixedOption : IPlayerPolicy
    {
        private readonly int _option;

        public FixedOption(int option)
        {
            _option = option;
        }

        public string Name => "fixa";
        public SimCard Choose(DecisionContext context) => context.PlayableCards[0];
        public SimCard ChooseSearch(DecisionContext context, IReadOnlyList<SimCard> candidates) => candidates[0];
        public int ChooseEventOption(DecisionContext context, SimEvent dilemma) => _option;
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Run_AppliesTheChosenOption_AndRecordsIt(int option)
    {
        var data = TestData.Build(NeutralCards(), new[] { Dilemma("D", Option(-6f), Option(4f)) });
        var history = new PhaseHistory();

        var game = PhaseSimulator.Run(Setup(data), new FixedOption(option), new GameSeeds(1, 1), history);

        var saude = Array.IndexOf(PhaseSimulator.Parameters, Target);
        Assert.That(game.Turns[0].EventId, Is.EqualTo("D"));
        Assert.That(game.Turns[0].EventChoice, Is.EqualTo(option));
        Assert.That(game.Turns[0].Values[saude], Is.EqualTo(TestData.NeutralValue + (option == 0 ? -6f : 4f)));
        Assert.That(history.Turns[0].HasEventChoice, Is.True);
        Assert.That(history.Turns[0].EventChoice, Is.EqualTo(option));
    }

    [Test]
    public void Run_TurnWithoutDilemma_HasNoChoice()
    {
        var data = TestData.Build(NeutralCards(), new[] { new SimEvent { Id = "Comum" } });

        var game = PhaseSimulator.Run(Setup(data), new FixedOption(0), new GameSeeds(1, 1));

        Assert.That(game.Turns.Select(turn => turn.EventChoice), Is.All.EqualTo(-1));
    }

    [Test]
    public void Run_FollowUpComesOnItsTurn_EvenOutsideTheThemeList()
    {
        var followUp = new SimEvent { Id = "Depois", IsFollowUpOnly = true, StatEffects = Change(Target, -1f) };
        var data = TestData.Build(NeutralCards(), new[] { Dilemma("D", Option(0f, "Depois", 2)), followUp });
        var setup = new PhaseSetup
        {
            Data = data,
            Archetype = CardArchetype.Geral,
            VictoryTurnCountOverride = 6,
            Preset = new SimPreset { Id = "Tema", AllowedEventIds = new List<string> { "D" } },
        };

        var game = PhaseSimulator.Run(setup, new FixedOption(0), new GameSeeds(1, 1));

        Assert.That(game.Turns[2].EventId, Is.EqualTo("Depois"));
    }

    [Test]
    public void Run_ForcedChoice_WinsOverThePolicy()
    {
        var data = TestData.Build(NeutralCards(), new[] { Dilemma("D", Option(-6f), Option(4f)) });
        var setup = new PhaseSetup { Data = data, Archetype = CardArchetype.Geral, VictoryTurnCountOverride = 6, ForcedChoices = new Dictionary<string, int> { ["D"] = 1 } };

        var game = PhaseSimulator.Run(setup, new FixedOption(0), new GameSeeds(1, 1));

        Assert.That(game.Turns[0].EventChoice, Is.EqualTo(1));
    }

    [Test]
    public void Greedy_PrefersTheOptionWithTheBetterState()
    {
        // Com margem curta: longe do critico a nota satura e as opcoes empatam.
        var data = TestData.Build(NeutralCards());
        var stats = StatsOf(data);
        stats.ApplyModifiers(Change(Target, -20f));
        var dilemma = Dilemma("D", Option(-8f), Option(8f));

        Assert.That(new GreedyPolicy().ChooseEventOption(Context(data, stats), dilemma), Is.EqualTo(1));
    }

    [Test]
    public void Balanced_WeighsTheParameterClosestToItsCritical()
    {
        var data = TestData.Build(NeutralCards());
        var stats = StatsOf(data);
        stats.ApplyModifiers(Change(CityParameterType.Energia, -38f));
        var dilemma = new SimEvent
        {
            Id = "D",
            Choices = new[]
            {
                new EventChoiceDefinition { Effects = Change(Target, 6f) },
                new EventChoiceDefinition { Effects = Change(CityParameterType.Energia, 3f) },
            },
        };

        Assert.That(new BalancedPolicy().ChooseEventOption(Context(data, stats), dilemma), Is.EqualTo(1));
    }

    [Test]
    public void RandomPolicy_StaysInsideTheOptions()
    {
        var data = TestData.Build(NeutralCards());
        var dilemma = Dilemma("D", Option(1f), Option(2f), Option(3f));
        var context = Context(data, StatsOf(data), 9);

        var seen = Enumerable.Range(0, 60).Select(_ => new RandomPolicy().ChooseEventOption(context, dilemma)).Distinct().OrderBy(x => x);

        Assert.That(seen, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void ForwardModel_ReproducesThePhaseSimulator_WithChoicesAndFollowUps()
    {
        var cards = Enumerable.Range(0, 10)
            .Select(i => TestData.Card($"C{i}", (CityParameterType.Saude, i - 5), (CityParameterType.Mobilidade, 3 - i)))
            .ToList();
        var followUp = new SimEvent { Id = "F", IsFollowUpOnly = true, StatEffects = Change(CityParameterType.Energia, -2f) };
        var events = new List<SimEvent>
        {
            new SimEvent { Id = "D1", MaxTurn = 15, Choices = new[] { Option(2f, "F", 2), Option(-1f) } },
            new SimEvent { Id = "D2", MaxTurn = 15, Choices = new[] { Option(-3f, "F", 1), Option(1f, "F", 3) } },
            followUp,
        };
        events.AddRange(Enumerable.Range(0, 3).Select(i => new SimEvent { Id = $"E{i}", StatEffects = Change(CityParameterType.Energia, i - 1f) }));
        var data = TestData.Build(cards, events);
        var setup = TestData.Setup(data);
        var seeds = new GameSeeds(3, 11);
        var policy = new BalancedPolicy();

        var expected = PhaseSimulator.Run(setup, policy, seeds);
        Assert.That(expected.Turns.Count(turn => turn.EventChoice >= 0), Is.GreaterThan(0), "o caso precisa ter dilema");
        Assert.That(expected.Turns.Count(turn => turn.EventId == "F"), Is.GreaterThan(0), "o caso precisa ter consequencia");

        var stats = StatsOf(data);
        var deck = DeckBuilder.Build(data.Cards, setup.Archetype, setup.LoadedCardIds);
        var hand = new CardHand<SimCard>(deck, new Random(seeds.Hand));
        var pool = new RandomEventPool<SimEvent>(data.Events, new Random(seeds.Events), data.Events, null, setup.VictoryTurnCount);
        var policyRandom = new Random(seeds.Policy);
        var chosen = new List<int>();
        int Choose(CityStats current, SimEvent dilemma)
        {
            var option = policy.ChooseEventOption(new DecisionContext { Stats = current, Data = data, Random = policyRandom }, dilemma);
            chosen.Add(option);
            return option;
        }

        var outcome = GameOutcome.None;
        var turn = 1;
        while (true)
        {
            var playable = ForwardModel.DrawPlayable(hand, setup.HandSize, stats);
            var card = policy.Choose(new DecisionContext { Stats = stats, PlayableCards = playable, Data = data, Random = policyRandom });
            Assert.That(card.Id, Is.EqualTo(expected.Turns[turn - 1].CardId), $"turno {turn}");
            outcome = ForwardModel.StepTurn(stats, card, turn, setup.VictoryTurnCount, pool, Choose);
            Assert.That(PhaseSimulator.Parameters.Select(stats.GetValue), Is.EqualTo(expected.Turns[turn - 1].Values), $"turno {turn}");
            if (outcome != GameOutcome.None)
                break;
            turn++;
        }

        Assert.That(outcome, Is.EqualTo(expected.Outcome));
        Assert.That(chosen, Is.EqualTo(expected.Turns.Where(record => record.EventChoice >= 0).Select(record => record.EventChoice)));
    }

    [Test]
    public void ForwardModel_DilemmaWithoutAChooser_FailsLoudly()
    {
        var data = TestData.Build(NeutralCards());
        var pool = new RandomEventPool<SimEvent>(new[] { Dilemma("D", Option(1f)) }, new Random(1));

        Assert.Throws<InvalidOperationException>(() => ForwardModel.StepTurn(StatsOf(data), null, 1, 6, pool));
    }

    // A primeira opcao rende mais agora e arruina a cidade depois: so quem conhece a consequencia foge dela.
    [TestCase(true, 1)]
    [TestCase(false, 0)]
    public void Mcts_OnlyTheInformedPolicySeesTheHiddenFollowUp(bool informed, int expected)
    {
        var data = TestData.Build(NeutralCards(), new[] { Dilemma("D", Option(5f, "Ruina", 2), Option(0f)), Ruin() });
        var policy = new MctsPolicy(new MctsOptions { Iterations = 60, Informed = informed });

        var game = PhaseSimulator.Run(Setup(data), policy, new GameSeeds(5, 2));

        Assert.That(game.Turns[0].EventChoice, Is.EqualTo(expected));
    }

    [Test]
    public void Mcts_SameSeeds_SameChoices()
    {
        var data = TestData.Build(NeutralCards(), new[] { Dilemma("D", Option(5f, "Ruina", 2), Option(0f)), Ruin() });
        var policy = new MctsPolicy(new MctsOptions { Iterations = 40 });

        var first = PhaseSimulator.Run(Setup(data), policy, new GameSeeds(5, 2));
        var second = PhaseSimulator.Run(Setup(data), policy, new GameSeeds(5, 2));

        Assert.That(second.Turns.Select(turn => turn.EventChoice), Is.EqualTo(first.Turns.Select(turn => turn.EventChoice)));
    }

    private static SimulationOptions Options()
    {
        return new SimulationOptions
        {
            GamesPerJob = 4,
            BaseSeed = 7,
            Policies = new List<string> { "equilibrada" },
            Archetypes = new List<string> { "Geral" },
            Setups = new List<string> { SimulationRunner.BaseSetupName },
            VictoryTurnCount = 6,
        };
    }

    [Test]
    public void Mode_BuildsOneJobPerOptionPlusTheFreeGame()
    {
        var data = TestData.Build(NeutralCards(), new[] { Dilemma("D", Option(-6f), Option(4f)), new SimEvent { Id = "Comum" } });

        var jobs = DilemmaSimulation.BuildJobs(data, Options());

        Assert.That(jobs.Select(job => job.ForcedOption), Is.EqualTo(new[] { -1, 0, 1 }));
        Assert.That(jobs.All(job => job.Dilemma == "D"), Is.True);
    }

    [Test]
    public void Mode_ForcedGames_ReportTheForcedOption()
    {
        var data = TestData.Build(NeutralCards(), new[] { Dilemma("D", Option(-6f), Option(4f)) });
        var job = DilemmaSimulation.BuildJobs(data, Options()).Single(candidate => candidate.ForcedOption == 1);

        var game = DilemmaSimulation.Play(job, Options(), 0);

        Assert.That(game.Occurred, Is.True);
        Assert.That(game.Turn, Is.EqualTo(1));
        Assert.That(game.Chosen, Is.EqualTo(1));
    }

    [Test]
    public void Mode_Summary_SeparatesForcedWinRateFromFreeChoiceRate()
    {
        DilemmaGame Game(int forced, int chosen, GameOutcome outcome, bool occurred = true) => new DilemmaGame
        {
            Setup = "fase1",
            Archetype = "Geral",
            Policy = "p",
            Dilemma = "D",
            ForcedOption = forced,
            Chosen = chosen,
            Outcome = outcome,
            Occurred = occurred,
        };
        var games = new[]
        {
            Game(-1, 0, GameOutcome.GameOver), Game(-1, 0, GameOutcome.Victory), Game(-1, 1, GameOutcome.Victory), Game(-1, -1, GameOutcome.Victory, occurred: false),
            Game(0, 0, GameOutcome.Victory), Game(0, 0, GameOutcome.GameOver), Game(0, -1, GameOutcome.Victory, occurred: false),
            Game(1, 1, GameOutcome.Victory),
        };

        var summary = DilemmaSimulation.Summarize(games);

        Assert.That(summary.Select(row => row.Option), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(summary[0].ForcedGames, Is.EqualTo(2));
        Assert.That(summary[0].ForcedWinRate, Is.EqualTo(0.5));
        Assert.That(summary[0].FreeChoiceRate, Is.EqualTo(2.0 / 3.0).Within(1e-9));
        Assert.That(summary[1].ForcedWinRate, Is.EqualTo(1.0));
        Assert.That(summary[1].FreeChoiceRate, Is.EqualTo(1.0 / 3.0).Within(1e-9));
    }

    [Test]
    public void Mode_DataWithoutDilemmas_HasNoJobs()
    {
        Assert.That(DilemmaSimulation.BuildJobs(TestData.Build(NeutralCards(), new[] { new SimEvent { Id = "Comum" } }), Options()), Is.Empty);
    }
}
