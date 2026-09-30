using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Metis.Simulator.Policies;

namespace Metis.Simulator;

// Uma combinacao de configuracao a simular N vezes.
public sealed class SimulationJob
{
    public string SetupName { get; init; } = "";
    public PhaseSetup Setup { get; init; } = null!;
    public IPlayerPolicy Policy { get; init; } = null!;
}

public sealed class SimulationOptions
{
    public string DataPath { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public int GamesPerJob { get; set; } = 1000;
    public int BaseSeed { get; set; } = 20261005;
    public int? MaxThreads { get; set; }
    public bool WriteTurns { get; set; } = true;
    public List<string> Policies { get; set; } = new() { "aleatoria", "gulosa", "equilibrada" };
    public List<string> Archetypes { get; set; } = new() { "Sustentabilidade", "Industria", "Automacao" };

    // "fase1" e a cidade calibrada padrao; os outros nomes sao ids de preset.
    public List<string> Setups { get; set; } = new();
    public int? HandSize { get; set; }
    public int? VictoryTurnCount { get; set; }
}

public static class SimulationRunner
{
    public const string BaseSetupName = "fase1";

    public static IPlayerPolicy CreatePolicy(string name)
    {
        return name switch
        {
            "aleatoria" => new RandomPolicy(),
            "gulosa" => new GreedyPolicy(),
            "equilibrada" => new BalancedPolicy(),
            _ => throw new ArgumentException($"Politica desconhecida: '{name}'."),
        };
    }

    public static List<SimulationJob> BuildJobs(BalanceData data, SimulationOptions options)
    {
        var setups = options.Setups.Count > 0
            ? options.Setups
            : new[] { BaseSetupName }.Concat(data.Presets.Select(preset => preset.Id)).ToList();

        var jobs = new List<SimulationJob>();
        foreach (var setupName in setups)
        {
            SimPreset preset = null;
            if (setupName != BaseSetupName)
                preset = data.Presets.FirstOrDefault(candidate => candidate.Id == setupName)
                    ?? throw new ArgumentException($"Preset desconhecido: '{setupName}'.");

            foreach (var archetypeName in options.Archetypes)
            {
                var archetype = Enum.Parse<CardArchetype>(archetypeName);
                foreach (var policyName in options.Policies)
                {
                    jobs.Add(new SimulationJob
                    {
                        SetupName = setupName,
                        Policy = CreatePolicy(policyName),
                        Setup = new PhaseSetup
                        {
                            Data = data,
                            Archetype = archetype,
                            Preset = preset,
                            HandSizeOverride = options.HandSize,
                            VictoryTurnCountOverride = options.VictoryTurnCount,
                        },
                    });
                }
            }
        }
        return jobs;
    }

    public static void Run(SimulationOptions options, TextWriter log)
    {
        var data = BalanceData.Load(options.DataPath);
        var jobs = BuildJobs(data, options);
        var total = jobs.Count * options.GamesPerJob;
        var results = new GameRecord[total];

        log.WriteLine($"{jobs.Count} combinacoes x {options.GamesPerJob} partidas = {total} partidas.");
        var started = DateTime.Now;
        var done = 0;
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.MaxThreads ?? Environment.ProcessorCount };
        Parallel.For(0, total, parallel, index =>
        {
            var job = jobs[index / options.GamesPerJob];
            var gameIndex = index % options.GamesPerJob;

            // Mesmo gameIndex usa as mesmas sementes em toda combinacao, pra comparar politicas e arquetipos sob a mesma sorte.
            results[index] = PhaseSimulator.Run(job.Setup, job.Policy, new GameSeeds(options.BaseSeed, gameIndex));

            var finished = Interlocked.Increment(ref done);
            if (finished % Math.Max(1, total / 10) == 0)
                log.WriteLine($"  {finished}/{total} ({(DateTime.Now - started).TotalSeconds:F1}s)");
        });

        Directory.CreateDirectory(options.OutputDirectory);
        WriteGames(Path.Combine(options.OutputDirectory, "games.csv"), data, jobs, options.GamesPerJob, results);
        if (options.WriteTurns)
            WriteTurns(Path.Combine(options.OutputDirectory, "turns.csv"), results);
        WriteRunInfo(Path.Combine(options.OutputDirectory, "run_info.json"), options, data, started);
        log.WriteLine($"Pronto em {(DateTime.Now - started).TotalSeconds:F1}s, saida em '{options.OutputDirectory}'.");
    }

    private static void WriteGames(string path, BalanceData data, List<SimulationJob> jobs, int gamesPerJob, GameRecord[] results)
    {
        var tiers = data.TierThresholds.Count;
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        var header = new List<string> { "game_id", "setup", "archetype", "policy", "game_index", "hand_size", "victory_turns", "outcome", "turns_played" };
        header.AddRange(PhaseSimulator.Parameters.Select(parameter => $"final_{parameter}"));
        header.AddRange(Enumerable.Range(1, tiers).Select(tier => $"tier{tier}_turn"));
        header.AddRange(new[] { "max_collapsed", "turns_with_collapse", "turns_no_play" });
        writer.WriteLine(string.Join(',', header));

        for (int index = 0; index < results.Length; index++)
        {
            var job = jobs[index / gamesPerJob];
            var game = results[index];
            var last = game.Turns[game.Turns.Count - 1];
            var row = new List<string>
            {
                index.ToString(CultureInfo.InvariantCulture),
                job.SetupName,
                job.Setup.Archetype.ToString(),
                job.Policy.Name,
                (index % gamesPerJob).ToString(CultureInfo.InvariantCulture),
                job.Setup.HandSize.ToString(CultureInfo.InvariantCulture),
                job.Setup.VictoryTurnCount.ToString(CultureInfo.InvariantCulture),
                game.Outcome.ToString(),
                game.TurnsPlayed.ToString(CultureInfo.InvariantCulture),
            };
            row.AddRange(last.Values.Select(Format));
            row.AddRange(game.TierReachedTurn.Select(turn => turn.ToString(CultureInfo.InvariantCulture)));
            row.Add(game.Turns.Max(turn => turn.Collapsed).ToString(CultureInfo.InvariantCulture));
            row.Add(game.Turns.Count(turn => turn.Collapsed > 0).ToString(CultureInfo.InvariantCulture));
            row.Add(game.Turns.Count(turn => turn.PlayableCount == 0).ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(string.Join(',', row));
        }
    }

    private static void WriteTurns(string path, GameRecord[] results)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        var header = new List<string> { "game_id", "turn", "card_id", "playable_count", "playable_ids", "event_id" };
        header.AddRange(PhaseSimulator.Parameters.Select(parameter => parameter.ToString()));
        header.Add("collapsed");
        writer.WriteLine(string.Join(',', header));

        for (int index = 0; index < results.Length; index++)
        {
            foreach (var turn in results[index].Turns)
            {
                var row = new List<string>
                {
                    index.ToString(CultureInfo.InvariantCulture),
                    turn.Turn.ToString(CultureInfo.InvariantCulture),
                    turn.CardId,
                    turn.PlayableCount.ToString(CultureInfo.InvariantCulture),
                    string.Join('|', turn.PlayableIds),
                    turn.EventId,
                };
                row.AddRange(turn.Values.Select(Format));
                row.Add(turn.Collapsed.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine(string.Join(',', row));
            }
        }
    }

    // Registro do que gerou a saida, pra qualquer resultado do notebook poder ser refeito.
    private static void WriteRunInfo(string path, SimulationOptions options, BalanceData data, DateTime started)
    {
        var info = new Dictionary<string, object>
        {
            ["started_at"] = started.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            ["data_path"] = Path.GetFullPath(options.DataPath),
            ["data_sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(options.DataPath))).ToLowerInvariant(),
            ["games_per_job"] = options.GamesPerJob,
            ["base_seed"] = options.BaseSeed,
            ["policies"] = options.Policies,
            ["archetypes"] = options.Archetypes,
            ["setups"] = options.Setups.Count > 0 ? options.Setups : new[] { BaseSetupName }.Concat(data.Presets.Select(preset => preset.Id)).ToList(),
            ["hand_size"] = options.HandSize ?? data.Rules.HandSize,
            ["victory_turns"] = options.VictoryTurnCount ?? data.Rules.VictoryTurnCount,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
