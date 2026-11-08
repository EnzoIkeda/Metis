using Metis.Simulator;

namespace Metis.Simulator.Tests;

public class ThemedPhaseSimTests
{
    private static BalanceData Data()
    {
        var events = Enumerable.Range(0, 6).Select(i => new SimEvent { Id = $"E{i}", MinTurn = 1, MaxTurn = 99 }).ToList();
        return TestData.Build(events: events);
    }

    [Test]
    public void Run_WithAllowedEvents_OnlyDrawsThose()
    {
        var data = Data();
        var setup = new PhaseSetup { Data = data, Preset = new SimPreset { Id = "Tema", AllowedEventIds = new[] { "E2", "E4" } } };

        var game = PhaseSimulator.Run(setup, SimulationRunner.CreatePolicy("gulosa"), new GameSeeds(1, 1));

        Assert.That(game.Turns.Select(turn => turn.EventId).Distinct(), Is.SubsetOf(new[] { "E2", "E4" }));
    }

    [Test]
    public void Parse_ReadsThemes_AndOldFilesHaveNone()
    {
        const string json = """
        { "Parameters": [], "Cards": [], "Events": [], "Advantages": [], "Presets": [],
          "Themes": [ { "Id": "Tema", "Overrides": [ { "Parameter": "Renda", "InitialValue": 30 } ], "DerivaMultiplier": 1.5, "AllowedEvents": [ "E1" ] } ] }
        """;

        var theme = BalanceData.Parse(json).Themes.Single();

        Assert.That(theme.Overrides[CityParameterType.Renda], Is.EqualTo(30f));
        Assert.That(theme.AllowedEventIds, Is.EqualTo(new[] { "E1" }));
        Assert.That(BalanceData.Parse("""{ "Parameters": [], "Cards": [], "Events": [], "Advantages": [], "Presets": [] }""").Themes, Is.Empty);
    }

    [Test]
    public void BuildJobs_AcceptsThemeAsSetup()
    {
        var data = new BalanceData { Themes = new[] { new SimPreset { Id = "Tema" } } };
        var options = new SimulationOptions { Setups = new List<string> { "Tema" }, Archetypes = new List<string> { "Industria" }, Policies = new List<string> { "gulosa" } };

        Assert.That(SimulationRunner.BuildJobs(data, options).Single().Setup.Preset.Id, Is.EqualTo("Tema"));
    }
}
