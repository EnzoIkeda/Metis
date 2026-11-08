using NUnit.Framework;

[TestFixture]
public class ScenarioImportFormatTests
{
    private const int TotalTurns = 20;

    private static ScenarioImportFormat.ObjectiveDto Objective(string parameter = "Saude")
    {
        return new ScenarioImportFormat.ObjectiveDto { Parameter = parameter, Comparison = "AtLeast", Threshold = 50f, ByTurn = 10 };
    }

    private static ScenarioImportFormat.FileDto File(ScenarioImportFormat.ScenarioDto scenario)
    {
        return new ScenarioImportFormat.FileDto { Scenarios = new[] { scenario } };
    }

    private static ScenarioImportFormat.ScenarioDto Valid()
    {
        return new ScenarioImportFormat.ScenarioDto { Id = "Cenario_A", Theme = "Tema_A", Objectives = new[] { Objective() } };
    }

    [Test]
    public void Validate_AcceptsAValidScenario()
    {
        Assert.That(ScenarioImportFormat.Validate(File(Valid()), TotalTurns, new[] { "Tema_A" }), Is.Empty);
    }

    [Test]
    public void Validate_RejectsUnknownTheme()
    {
        Assert.That(ScenarioImportFormat.Validate(File(Valid()), TotalTurns, new[] { "Outro" }), Has.Some.Contains("tema desconhecido"));
    }

    [Test]
    public void Validate_RejectsARepeatedId()
    {
        var file = new ScenarioImportFormat.FileDto { Scenarios = new[] { Valid(), Valid() } };
        Assert.That(ScenarioImportFormat.Validate(file, TotalTurns), Has.Some.Contains("Id repetido"));
    }

    [Test]
    public void Validate_RequiresAConcreteArchetypeWhenFixed()
    {
        var scenario = Valid();
        scenario.FreeArchetype = false;
        scenario.Archetype = "Geral";
        Assert.That(ScenarioImportFormat.Validate(File(scenario), TotalTurns), Has.Some.Contains("arquetipo invalido"));

        scenario.Archetype = "Industria";
        Assert.That(ScenarioImportFormat.Validate(File(scenario), TotalTurns), Is.Empty);
    }

    [Test]
    public void Validate_LimitsTheNumberOfObjectives()
    {
        var scenario = Valid();
        scenario.Objectives = new ScenarioImportFormat.ObjectiveDto[0];
        Assert.That(ScenarioImportFormat.Validate(File(scenario), TotalTurns), Has.Some.Contains("objetivos"));

        scenario.Objectives = new[] { Objective("Saude"), Objective("Renda"), Objective("Energia"), Objective("Seguranca") };
        Assert.That(ScenarioImportFormat.Validate(File(scenario), TotalTurns), Has.Some.Contains("objetivos"));
    }

    [TestCase(-1f, 10, "limite")]
    [TestCase(101f, 10, "limite")]
    [TestCase(50f, 0, "turno")]
    [TestCase(50f, 21, "turno")]
    public void Validate_RejectsValuesOutOfRange(float threshold, int byTurn, string expected)
    {
        var scenario = Valid();
        scenario.Objectives[0].Threshold = threshold;
        scenario.Objectives[0].ByTurn = byTurn;
        Assert.That(ScenarioImportFormat.Validate(File(scenario), TotalTurns), Has.Some.Contains(expected));
    }

    [TestCase("Inexistente", "AtLeast", "parametro")]
    [TestCase("3", "AtLeast", "parametro")]
    [TestCase("Saude", "Maior", "comparacao")]
    public void Validate_RejectsUnknownNames(string parameter, string comparison, string expected)
    {
        var scenario = Valid();
        scenario.Objectives[0].Parameter = parameter;
        scenario.Objectives[0].Comparison = comparison;
        Assert.That(ScenarioImportFormat.Validate(File(scenario), TotalTurns), Has.Some.Contains(expected));
    }

    [Test]
    public void Validate_RejectsARepeatedObjective()
    {
        var scenario = Valid();
        scenario.Objectives = new[] { Objective(), Objective() };
        Assert.That(ScenarioImportFormat.Validate(File(scenario), TotalTurns), Has.Some.Contains("repetido"));
    }
}
