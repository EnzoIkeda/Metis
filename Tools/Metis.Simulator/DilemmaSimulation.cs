using System.Globalization;
using System.Text;

namespace Metis.Simulator;

// Uma partida vista por um dilema: se ele saiu, em que turno, qual opcao valeu e como a partida acabou.
public sealed class DilemmaGame
{
    public string Setup { get; init; } = "";
    public string Archetype { get; init; } = "";
    public string Policy { get; init; } = "";
    public string Dilemma { get; init; } = "";

    // Opcao imposta; -1 quando a politica escolheu livremente.
    public int ForcedOption { get; init; }
    public int GameIndex { get; init; }
    public bool Occurred { get; init; }
    public int Turn { get; init; }
    public int Chosen { get; init; } = -1;
    public GameOutcome Outcome { get; init; }
    public int TurnsPlayed { get; init; }
}

public sealed class DilemmaSummary
{
    public string Setup { get; init; } = "";
    public string Archetype { get; init; } = "";
    public string Policy { get; init; } = "";
    public string Dilemma { get; init; } = "";
    public int Option { get; init; }

    // Partidas em que o dilema saiu com esta opcao imposta, e a vitoria nelas.
    public int ForcedGames { get; init; }
    public double ForcedWinRate { get; init; }

    // Entre as partidas livres em que o dilema saiu, quantas ficaram com esta opcao.
    public double FreeChoiceRate { get; init; }
}

// Uma combinacao medida: opcao imposta em um dilema, ou -1 para o jogo livre.
public sealed class DilemmaJob
{
    public string SetupName { get; init; } = "";
    public string Dilemma { get; init; } = "";
    public int ForcedOption { get; init; }
    public SimulationJob Base { get; init; } = null!;
    public PhaseSetup Setup { get; init; } = null!;
}

// Modo "dilemas": cada opcao imposta nas mesmas sementes, porque comparar pela escolha livre teria vies: cada politica escolhe conforme o estado da cidade.
public static class DilemmaSimulation
{
    public static List<SimEvent> DilemmasOf(BalanceData data, PhaseSetup setup)
    {
        return CityPresetRules.AllowedEvents(data.Events, setup.Preset?.AllowedEventIds as IReadOnlyCollection<string>)
            .Where(candidate => candidate.Choices.Count > 0).ToList();
    }

    public static List<DilemmaJob> BuildJobs(BalanceData data, SimulationOptions options)
    {
        var jobs = new List<DilemmaJob>();
        foreach (var job in SimulationRunner.BuildJobs(data, options))
        {
            foreach (var dilemma in DilemmasOf(data, job.Setup))
            {
                jobs.Add(new DilemmaJob { SetupName = job.SetupName, Dilemma = dilemma.Id, ForcedOption = -1, Base = job, Setup = job.Setup });
                for (var option = 0; option < dilemma.Choices.Count; option++)
                    jobs.Add(new DilemmaJob { SetupName = job.SetupName, Dilemma = dilemma.Id, ForcedOption = option, Base = job, Setup = WithForced(job.Setup, dilemma.Id, option) });
            }
        }
        return jobs;
    }

    private static PhaseSetup WithForced(PhaseSetup setup, string dilemmaId, int option)
    {
        return new PhaseSetup
        {
            Data = setup.Data,
            Archetype = setup.Archetype,
            Preset = setup.Preset,
            LoadedCardIds = setup.LoadedCardIds,
            LoadedAdvantageIds = setup.LoadedAdvantageIds,
            HandSizeOverride = setup.HandSizeOverride,
            VictoryTurnCountOverride = setup.VictoryTurnCountOverride,
            FiniteDeck = setup.FiniteDeck,
            PermanentReveal = setup.PermanentReveal,
            ForcedChoices = new Dictionary<string, int> { [dilemmaId] = option },
        };
    }

    public static DilemmaGame Play(DilemmaJob job, SimulationOptions options, int gameIndex)
    {
        var game = PhaseSimulator.Run(job.Setup, job.Base.Policy, new GameSeeds(options.BaseSeed, gameIndex));
        var turn = game.Turns.FirstOrDefault(candidate => candidate.EventId == job.Dilemma);
        return new DilemmaGame
        {
            Setup = job.SetupName,
            Archetype = job.Setup.Archetype.ToString(),
            Policy = job.Base.Policy.Name,
            Dilemma = job.Dilemma,
            ForcedOption = job.ForcedOption,
            GameIndex = gameIndex,
            Occurred = turn != null,
            Turn = turn?.Turn ?? 0,
            Chosen = turn?.EventChoice ?? -1,
            Outcome = game.Outcome,
            TurnsPlayed = game.TurnsPlayed,
        };
    }

    public static List<DilemmaSummary> Summarize(IEnumerable<DilemmaGame> games)
    {
        var summaries = new List<DilemmaSummary>();
        foreach (var group in games.GroupBy(game => (game.Setup, game.Archetype, game.Policy, game.Dilemma)))
        {
            var free = group.Where(game => game.ForcedOption < 0 && game.Occurred).ToList();
            foreach (var forced in group.Where(game => game.ForcedOption >= 0).GroupBy(game => game.ForcedOption).OrderBy(options => options.Key))
            {
                var occurred = forced.Where(game => game.Occurred).ToList();
                summaries.Add(new DilemmaSummary
                {
                    Setup = group.Key.Setup,
                    Archetype = group.Key.Archetype,
                    Policy = group.Key.Policy,
                    Dilemma = group.Key.Dilemma,
                    Option = forced.Key,
                    ForcedGames = occurred.Count,
                    ForcedWinRate = occurred.Count == 0 ? 0.0 : occurred.Count(game => game.Outcome == GameOutcome.Victory) / (double)occurred.Count,
                    FreeChoiceRate = free.Count == 0 ? 0.0 : free.Count(game => game.Chosen == forced.Key) / (double)free.Count,
                });
            }
        }
        return summaries;
    }

    public static void Run(SimulationOptions options, TextWriter log)
    {
        var data = BalanceData.Load(options.DataPath);
        var jobs = BuildJobs(data, options);
        if (jobs.Count == 0)
        {
            log.WriteLine("Nenhum dilema nos dados exportados.");
            return;
        }

        var total = jobs.Count * options.GamesPerJob;
        var results = new DilemmaGame[total];
        log.WriteLine($"{jobs.Count} combinacoes x {options.GamesPerJob} partidas = {total} partidas.");
        var started = DateTime.Now;
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.MaxThreads ?? Environment.ProcessorCount };
        Parallel.For(0, total, parallel, index =>
        {
            results[index] = Play(jobs[index / options.GamesPerJob], options, index % options.GamesPerJob);
        });

        Directory.CreateDirectory(options.OutputDirectory);
        WriteGames(Path.Combine(options.OutputDirectory, "dilemas.csv"), results);
        WriteSummary(Path.Combine(options.OutputDirectory, "dilemas_resumo.csv"), data, Summarize(results));
        log.WriteLine($"Pronto em {(DateTime.Now - started).TotalSeconds:F1}s, saida em '{options.OutputDirectory}'.");
    }

    private static string Number(double value) => value.ToString("F4", CultureInfo.InvariantCulture);

    private static void WriteGames(string path, DilemmaGame[] games)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("setup,archetype,policy,dilemma,forced_option,game_index,occurred,turn,chosen,outcome,turns_played");
        foreach (var game in games)
            writer.WriteLine(string.Join(',', game.Setup, game.Archetype, game.Policy, game.Dilemma, game.ForcedOption, game.GameIndex, game.Occurred ? 1 : 0, game.Turn, game.Chosen, game.Outcome, game.TurnsPlayed));
    }

    private static void WriteSummary(string path, BalanceData data, List<DilemmaSummary> summaries)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("setup,archetype,policy,dilemma,option,label,forced_games,forced_win_rate,free_choice_rate");
        foreach (var summary in summaries)
        {
            var labels = data.Events.First(candidate => candidate.Id == summary.Dilemma).ChoiceLabels;
            var label = summary.Option < labels.Count ? labels[summary.Option].Replace("\"", "\"\"") : "";
            writer.WriteLine(string.Join(',', summary.Setup, summary.Archetype, summary.Policy, summary.Dilemma, summary.Option, $"\"{label}\"", summary.ForcedGames, Number(summary.ForcedWinRate), Number(summary.FreeChoiceRate)));
        }
    }
}
