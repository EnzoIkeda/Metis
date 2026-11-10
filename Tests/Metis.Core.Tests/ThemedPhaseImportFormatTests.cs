namespace Metis.Core.Tests;

public class ThemedPhaseImportFormatTests
{
    private static ThemedPhaseImportFormat.ThemeDto Theme(string id = "Tema_A")
    {
        return new ThemedPhaseImportFormat.ThemeDto
        {
            Id = id,
            NamePt = "Porto Azul",
            NameEn = "Blue Harbor",
            SummaryPt = "Cidade litorânea",
            SummaryEn = "Coastal city",
            OpeningPt = "Chegou a temporada.",
            OpeningEn = "The season has arrived.",
            VictoryPt = "A cidade aguentou.",
            VictoryEn = "The city held on.",
            InspirationPt = "Inspirada em dados reais.",
            InspirationEn = "Inspired by real data.",
            Overrides = new[] { new ThemedPhaseImportFormat.OverrideDto { Parameter = "Renda", InitialValue = 40f } },
            AllowedEvents = new[] { "E1" },
        };
    }

    private static List<string> Errors(params ThemedPhaseImportFormat.ThemeDto[] themes)
    {
        return ThemedPhaseImportFormat.Validate(new ThemedPhaseImportFormat.FileDto { Themes = themes }, new[] { "E1" }, new[] { "Ficha" });
    }

    [Test]
    public void Validate_CompleteTheme_HasNoErrors()
    {
        Assert.That(Errors(Theme()), Is.Empty);
    }

    [Test]
    public void Validate_RepeatedAndInvalidIdsAreReported()
    {
        Assert.That(Errors(Theme(), Theme()).Single(), Does.Contain("repetido"));
        Assert.That(Errors(Theme("com espaco")).Single(), Does.Contain("invalido"));
    }

    [Test]
    public void Validate_MissingTranslationAndLongTextAreReported()
    {
        var theme = Theme();
        theme.NameEn = "";
        theme.SummaryPt = new string('a', ThemedPhaseImportFormat.MaxSummaryLength + 1);

        var errors = Errors(theme);

        Assert.That(errors, Has.Some.Contains("NameEn"));
        Assert.That(errors, Has.Some.Contains("SummaryPt"));
    }

    [Test]
    public void Validate_DashIsReported()
    {
        var theme = Theme();
        theme.OpeningPt = "Chegou — a temporada";

        Assert.That(Errors(theme).Single(), Does.Contain("travessao"));
    }

    [TestCase("Inexistente", 40f, "desconhecido")]
    [TestCase("BemEstar", 40f, "calculado")]
    [TestCase("Renda", 140f, "fora de 0 a 100")]
    public void Validate_BadOverridesAreReported(string parameter, float value, string message)
    {
        var theme = Theme();
        theme.Overrides = new[] { new ThemedPhaseImportFormat.OverrideDto { Parameter = parameter, InitialValue = value } };

        Assert.That(Errors(theme).Single(), Does.Contain(message));
    }

    [Test]
    public void Validate_UnknownEventAndEntryAreReported()
    {
        var theme = Theme();
        theme.AllowedEvents = new[] { "Outro" };
        theme.KnowledgeEntry = "Sumiu";

        var errors = Errors(theme);

        Assert.That(errors, Has.Some.Contains("evento desconhecido"));
        Assert.That(errors, Has.Some.Contains("ficha desconhecida"));
    }

    [Test]
    public void Warnings_FewEventsAndNoEntryAreListed()
    {
        var warnings = ThemedPhaseImportFormat.Warnings(new ThemedPhaseImportFormat.FileDto { Themes = new[] { Theme() } });

        Assert.That(warnings, Has.Some.Contains("eventos permitidos"));
        Assert.That(warnings, Has.Some.Contains("sem ficha"));
    }

    private sealed class FakeEvent : IRandomEventDefinition
    {
        public string Id { get; init; } = "";
        public IReadOnlyList<TriggerCondition> TriggerConditions => Array.Empty<TriggerCondition>();
        public IReadOnlyList<StatModifier> StatEffects => Array.Empty<StatModifier>();
        public int MinTurn => 1;
        public int MaxTurn => 20;
        public IReadOnlyList<EventChoiceDefinition> Choices => Array.Empty<EventChoiceDefinition>();
        public bool IsFollowUpOnly => false;
    }

    [Test]
    public void AllowedEvents_FiltersThePool_AndEmptyListKeepsEverything()
    {
        var pool = new[] { new FakeEvent { Id = "A" }, new FakeEvent { Id = "B" } };

        Assert.That(CityPresetRules.AllowedEvents(pool, new[] { "B" }).Single().Id, Is.EqualTo("B"));
        Assert.That(CityPresetRules.AllowedEvents(pool, Array.Empty<string>()), Is.SameAs(pool));
        Assert.That(CityPresetRules.AllowedEvents(pool, null), Is.SameAs(pool));
    }
}
