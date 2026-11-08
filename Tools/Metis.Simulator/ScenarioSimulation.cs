using System.Globalization;
using System.Text;
using Metis.Simulator.Policies;

namespace Metis.Simulator;

// Uma partida de cenario: desfecho, estado de cada objetivo e estrelas, pela mesma conta do jogo.
public sealed class ScenarioGame
{
    public GameOutcome Outcome { get; init; }
    public int TurnsPlayed { get; init; }
    public IReadOnlyList<ObjectiveState> States { get; init; } = Array.Empty<ObjectiveState>();
    public int Stars { get; init; }
}

public sealed class ScenarioJob
{
    public SimScenario Scenario { get; init; } = null!;
    public CardArchetype Archetype { get; init; }
    public string PolicyName { get; init; } = "";
}

// Modo "cenarios": taxa de vitoria e de cada objetivo por cenario, arquetipo e politica.
// As politicas nao sabem dos objetivos, entao as taxas medem o que sai jogando so para sobreviver.
public static class ScenarioSimulation
{
    public static SimPreset ThemeOf(BalanceData data, SimScenario scenario)
    {
        return data.Themes.FirstOrDefault(theme => theme.Id == scenario.ThemeId)
            ?? throw new ArgumentException($"Cenario '{scenario.Id}' usa um tema desconhecido: '{scenario.ThemeId}'.");
    }

    public static List<ScenarioJob> BuildJobs(BalanceData data, SimulationOptions options)
    {
        var scenarios = options.Setups.Count > 0
            ? options.Setups.Select(id => data.Scenarios.FirstOrDefault(scenario => scenario.Id == id) ?? throw new ArgumentException($"Cenario desconhecido: '{id}'.")).ToList()
            : data.Scenarios.ToList();

        var jobs = new List<ScenarioJob>();
        foreach (var scenario in scenarios)
        {
            var archetypes = scenario.FreeArchetype
                ? options.Archetypes.Select(Enum.Parse<CardArchetype>).ToList()
                : new List<CardArchetype> { scenario.Archetype };
            foreach (var archetype in archetypes)
            {
                foreach (var policyName in options.Policies)
                    jobs.Add(new ScenarioJob { Scenario = scenario, Archetype = archetype, PolicyName = policyName });
            }
        }
        return jobs;
    }

    public static ScenarioGame Play(BalanceData data, SimScenario scenario, CardArchetype archetype, IPlayerPolicy policy, SimulationOptions options, int gameIndex)
    {
        var setup = new PhaseSetup
        {
            Data = data,
            Archetype = archetype,
            Preset = ThemeOf(data, scenario),
            HandSizeOverride = options.HandSize,
            VictoryTurnCountOverride = options.VictoryTurnCount,
            FiniteDeck = options.FiniteDeck,
            PermanentReveal = options.PermanentReveal,
        };

        var history = new PhaseHistory();
        var game = PhaseSimulator.Run(setup, policy, new GameSeeds(options.BaseSeed, gameIndex), history);
        var states = ScenarioObjectiveEvaluator.EvaluateAll(scenario.Objectives, history.Turns, game.Outcome);
        return new ScenarioGame
        {
            Outcome = game.Outcome,
            TurnsPlayed = game.TurnsPlayed,
            States = states,
            Stars = ScenarioObjectiveEvaluator.Stars(game.Outcome, states),
        };
    }

    public static void Run(SimulationOptions options, TextWriter log)
    {
        var data = BalanceData.Load(options.DataPath);
        var jobs = BuildJobs(data, options);
        var policies = jobs.Select(job => SimulationRunner.CreatePolicy(job.PolicyName, options)).ToList();
        var total = jobs.Count * options.GamesPerJob;
        var results = new ScenarioGame[total];

        log.WriteLine($"{jobs.Count} combinacoes x {options.GamesPerJob} partidas = {total} partidas.");
        var started = DateTime.Now;
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.MaxThreads ?? Environment.ProcessorCount };
        Parallel.For(0, total, parallel, index =>
        {
            var jobIndex = index / options.GamesPerJob;
            var job = jobs[jobIndex];
            results[index] = Play(data, job.Scenario, job.Archetype, policies[jobIndex], options, index % options.GamesPerJob);
        });

        Directory.CreateDirectory(options.OutputDirectory);
        WriteGames(Path.Combine(options.OutputDirectory, "cenarios.csv"), jobs, options.GamesPerJob, results);
        log.WriteLine($"Pronto em {(DateTime.Now - started).TotalSeconds:F1}s, saida em '{options.OutputDirectory}'.");
    }

    private static void WriteGames(string path, List<ScenarioJob> jobs, int gamesPerJob, ScenarioGame[] results)
    {
        var objectiveColumns = jobs.Count == 0 ? 0 : jobs.Max(job => job.Scenario.Objectives.Count);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        var header = new List<string> { "scenario", "archetype", "policy", "game_index", "outcome", "turns_played", "stars" };
        header.AddRange(Enumerable.Range(0, objectiveColumns).Select(index => $"objective_{index}"));
        writer.WriteLine(string.Join(',', header));

        for (var index = 0; index < results.Length; index++)
        {
            var job = jobs[index / gamesPerJob];
            var game = results[index];
            var row = new List<string>
            {
                job.Scenario.Id,
                job.Archetype.ToString(),
                job.PolicyName,
                (index % gamesPerJob).ToString(CultureInfo.InvariantCulture),
                game.Outcome.ToString(),
                game.TurnsPlayed.ToString(CultureInfo.InvariantCulture),
                game.Stars.ToString(CultureInfo.InvariantCulture),
            };
            for (var column = 0; column < objectiveColumns; column++)
                row.Add(column < game.States.Count ? game.States[column].ToString() : "");
            writer.WriteLine(string.Join(',', row));
        }
    }
}
