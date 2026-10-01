using System.Globalization;
using Metis.Simulator;

// Uso: dotnet run --project Tools/Metis.Simulator -c Release -- --data <balance_data.json> --out <pasta> [opcoes]
var options = new SimulationOptions();
for (int i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Faltou valor depois de '{args[i]}'.");
    List<string> NextList() => Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    int NextInt() => int.Parse(Next(), CultureInfo.InvariantCulture);

    switch (args[i])
    {
        case "--data": options.DataPath = Next(); break;
        case "--out": options.OutputDirectory = Next(); break;
        case "--games": options.GamesPerJob = NextInt(); break;
        case "--seed": options.BaseSeed = NextInt(); break;
        case "--threads": options.MaxThreads = NextInt(); break;
        case "--policies": options.Policies = NextList(); break;
        case "--archetypes": options.Archetypes = NextList(); break;
        case "--setups": options.Setups = NextList(); break;
        case "--hand-size": options.HandSize = NextInt(); break;
        case "--turns": options.VictoryTurnCount = NextInt(); break;
        case "--no-turns": options.WriteTurns = false; break;
        case "--mode": options.Mode = Next(); break;
        case "--phases": options.PhaseCount = NextInt(); break;
        case "--reward-rollouts": options.RewardRollouts = NextInt(); break;
        case "--mcts-iterations": options.MctsIterations = NextInt(); break;
        case "--mcts-c": options.MctsExploration = double.Parse(Next(), CultureInfo.InvariantCulture); break;
        default: throw new ArgumentException($"Opcao desconhecida: '{args[i]}'.");
    }
}

if (string.IsNullOrEmpty(options.DataPath) || string.IsNullOrEmpty(options.OutputDirectory))
{
    Console.Error.WriteLine("Uso: --data <balance_data.json> --out <pasta> [--games N] [--seed S] [--threads N] [--policies a,b] [--archetypes a,b] [--setups fase1,PresetX] [--hand-size N] [--turns N] [--no-turns] [--mcts-iterations N] [--mcts-c C] [--mode fase|rodada] [--phases N] [--reward-rollouts N]");
    return 1;
}

if (options.Mode == "rodada")
    SimulationRunner.RunRuns(options, Console.Out);
else
    SimulationRunner.Run(options, Console.Out);
return 0;
