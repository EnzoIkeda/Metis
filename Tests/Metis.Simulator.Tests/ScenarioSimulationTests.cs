using Metis.Simulator;

namespace Metis.Simulator.Tests;

public class ScenarioSimulationTests
{
    private static BalanceData Data(params ScenarioObjective[] objectives)
    {
        var card = TestData.Card("Neutra");
        var baseData = TestData.Build(cards: Enumerable.Range(0, 12).Select(i => TestData.Card($"C{i}")).Append(card).ToList());
        return new BalanceData
        {
            Rules = baseData.Rules,
            Parameters = baseData.Parameters,
            Interaction = baseData.Interaction,
            Cards = baseData.Cards,
            Events = baseData.Events,
            Advantages = baseData.Advantages,
            Presets = baseData.Presets,
            Themes = new[] { new SimPreset { Id = "Tema" } },
            Scenarios = new[] { new SimScenario { Id = "Cenario", ThemeId = "Tema", Archetype = CardArchetype.Industria, FreeArchetype = false, Objectives = objectives } },
        };
    }

    private static ScenarioObjective Objective(float threshold)
    {
        return new ScenarioObjective { Parameter = CityParameterType.Renda, Comparison = ObjectiveComparison.AtLeast, Threshold = threshold, ByTurn = 5 };
    }

    [Test]
    public void Play_UsesTheSameRulesAsTheGameForStars()
    {
        var data = Data(Objective(10f), Objective(99f));
        var scenario = data.Scenarios.Single();

        var game = ScenarioSimulation.Play(data, scenario, scenario.Archetype, SimulationRunner.CreatePolicy("gulosa"), new SimulationOptions { FiniteDeck = true }, 1);

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Victory));
        Assert.That(game.States, Is.EqualTo(new[] { ObjectiveState.Achieved, ObjectiveState.Failed }));
        Assert.That(game.Stars, Is.EqualTo(2));
    }

    [Test]
    public void BuildJobs_FixedArchetypeRunsOnlyThatOne()
    {
        var data = Data(Objective(10f));
        var options = new SimulationOptions { Policies = new List<string> { "gulosa", "aleatoria" } };

        var jobs = ScenarioSimulation.BuildJobs(data, options);

        Assert.That(jobs.Count, Is.EqualTo(2));
        Assert.That(jobs.Select(job => job.Archetype).Distinct(), Is.EqualTo(new[] { CardArchetype.Industria }));
    }

    [Test]
    public void BuildJobs_RejectsAnUnknownScenario()
    {
        var options = new SimulationOptions { Setups = new List<string> { "Outro" } };
        Assert.Throws<ArgumentException>(() => ScenarioSimulation.BuildJobs(Data(Objective(10f)), options));
    }

    [Test]
    public void Parse_ReadsScenarios_AndOldFilesHaveNone()
    {
        const string json = """
        { "Parameters": [], "Cards": [], "Events": [], "Advantages": [], "Presets": [], "Themes": [],
          "Scenarios": [ { "Id": "C", "Theme": "T", "FreeArchetype": false, "Archetype": "Automacao",
            "Objectives": [ { "Parameter": "Saude", "Comparison": "AtMost", "Threshold": 40, "ByTurn": 7, "MustHold": true } ] } ] }
        """;

        var scenario = BalanceData.Parse(json).Scenarios.Single();

        Assert.That(scenario.ThemeId, Is.EqualTo("T"));
        Assert.That(scenario.Archetype, Is.EqualTo(CardArchetype.Automacao));
        Assert.That(scenario.Objectives.Single().Comparison, Is.EqualTo(ObjectiveComparison.AtMost));
        Assert.That(scenario.Objectives.Single().MustHold, Is.True);
        Assert.That(BalanceData.Parse("""{ "Parameters": [], "Cards": [], "Events": [], "Advantages": [], "Presets": [] }""").Scenarios, Is.Empty);
    }
}
