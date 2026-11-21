using System.Collections.Generic;

// Como a rodada terminou.
public enum RunEnding
{
    Defeat,
    BossVictory
}

// Cada linha do resumo da rodada; a tela traduz pelo tipo.
public enum RunSummaryLineKind
{
    Archetype,
    PhasesWon,
    DefeatPhase,
    DefeatAtBoss,
    BossDefeated,
    Themes,
    RewardCards,
    RewardAdvantages,
    Trades
}

public readonly struct RunSummaryLine
{
    public RunSummaryLine(RunSummaryLineKind kind, int number, IReadOnlyList<string> ids)
    {
        Kind = kind;
        Number = number;
        Ids = ids ?? new List<string>();
    }

    public RunSummaryLineKind Kind { get; }
    public int Number { get; }
    public IReadOnlyList<string> Ids { get; }
}

// Resumo do fim da rodada, so com o que o save ja guarda. Copia tudo, porque o save e apagado logo depois.
public class RunSummary
{
    private readonly List<RunSummaryLine> _lines = new List<RunSummaryLine>();

    private RunSummary()
    {
    }

    public RunEnding Ending { get; private set; }
    public CardArchetype Archetype { get; private set; }
    public int PhasesWon { get; private set; }

    // Fase em que a rodada terminou, contando a partir de 1.
    public int EndPhase { get; private set; }
    public string BossId { get; private set; } = string.Empty;
    public IReadOnlyList<string> Themes { get; private set; } = new List<string>();
    public IReadOnlyList<string> RewardCards { get; private set; } = new List<string>();
    public IReadOnlyList<string> RewardAdvantages { get; private set; } = new List<string>();
    public IReadOnlyList<string> Trades { get; private set; } = new List<string>();
    public IReadOnlyList<RunSummaryLine> Lines => _lines;

    public static RunSummary Build(MetaProgressionState state, RunEnding ending)
    {
        var summary = new RunSummary { Ending = ending };
        if (state == null)
            return summary;

        summary.Archetype = state.Archetype;
        // As fases vencidas so contam ao sair do mapa, entao a fase em jogo e a seguinte.
        summary.EndPhase = state.PhasesCompleted + 1;
        summary.PhasesWon = ending == RunEnding.BossVictory ? summary.EndPhase : state.PhasesCompleted;
        summary.BossId = state.CurrentBoss ?? string.Empty;
        summary.Themes = Copy(state.PlayedThemes);
        summary.RewardCards = Copy(state.LoadedCardNames);
        summary.RewardAdvantages = Copy(state.LoadedAdvantageNames);
        summary.Trades = Copy(state.AcceptedTrades);

        summary.Add(RunSummaryLineKind.Archetype, (int)summary.Archetype, null);
        summary.Add(RunSummaryLineKind.PhasesWon, summary.PhasesWon, null);
        var hasBoss = summary.BossId.Length > 0;
        if (ending == RunEnding.BossVictory)
            summary.Add(RunSummaryLineKind.BossDefeated, summary.EndPhase, Single(summary.BossId));
        else if (hasBoss)
            summary.Add(RunSummaryLineKind.DefeatAtBoss, summary.EndPhase, Single(summary.BossId));
        else
            summary.Add(RunSummaryLineKind.DefeatPhase, summary.EndPhase, null);

        summary.AddList(RunSummaryLineKind.Themes, summary.Themes);
        summary.AddList(RunSummaryLineKind.RewardCards, summary.RewardCards);
        summary.AddList(RunSummaryLineKind.RewardAdvantages, summary.RewardAdvantages);
        summary.AddList(RunSummaryLineKind.Trades, summary.Trades);
        return summary;
    }

    private void Add(RunSummaryLineKind kind, int number, IReadOnlyList<string> ids)
    {
        _lines.Add(new RunSummaryLine(kind, number, ids));
    }

    // Lista vazia nao vira linha.
    private void AddList(RunSummaryLineKind kind, IReadOnlyList<string> ids)
    {
        if (ids.Count > 0)
            Add(kind, ids.Count, ids);
    }

    private static List<string> Copy(List<string> source)
    {
        var copy = new List<string>();
        if (source == null)
            return copy;
        foreach (var item in source)
        {
            if (string.IsNullOrEmpty(item) == false)
                copy.Add(item);
        }
        return copy;
    }

    private static List<string> Single(string id)
    {
        return new List<string> { id };
    }
}
