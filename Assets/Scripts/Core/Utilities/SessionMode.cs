// Diz se a sessao e a rodada normal ou um cenario, e com isso qual arquivo de save esta em uso.
public static class SessionMode
{
    public const string RunFileName = "run_save.json";

    public static string ScenarioId { get; private set; } = string.Empty;

    // Fase tematica em que o cenario acontece.
    public static string ThemeName { get; private set; } = string.Empty;

    public static bool IsScenario => ScenarioId.Length > 0;

    // Cada cenario tem o proprio arquivo, entao comecar um nao apaga outro nem a rodada.
    public static string SaveFileName => IsScenario ? $"scenario_save_{ScenarioId}.json" : RunFileName;

    public static void EnterScenario(string scenarioId, string themeName)
    {
        ScenarioId = scenarioId ?? string.Empty;
        ThemeName = themeName ?? string.Empty;
    }

    public static void EnterRoguelike()
    {
        ScenarioId = string.Empty;
        ThemeName = string.Empty;
    }
}
