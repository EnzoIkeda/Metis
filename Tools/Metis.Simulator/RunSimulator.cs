using Metis.Simulator.Policies;

namespace Metis.Simulator;

public sealed class RewardOption
{
    public string Id { get; init; } = "";
    public bool IsCard { get; init; }
}

// O que quem escolhe a recompensa enxerga no fim de uma fase vencida.
public sealed class RewardContext
{
    public IReadOnlyList<RewardOption> Options { get; init; } = Array.Empty<RewardOption>();
    public RunSetup Setup { get; init; } = null!;
    public IReadOnlyList<string> LoadedCardIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> LoadedAdvantageIds { get; init; } = Array.Empty<string>();
    public Random Random { get; init; } = null!;
}

public interface IRewardPolicy
{
    string Name { get; }

    RewardOption Choose(RewardContext context);
}

public sealed class RandomRewardPolicy : IRewardPolicy
{
    public string Name => "aleatoria";

    public RewardOption Choose(RewardContext context)
    {
        return context.Options[context.Random.Next(context.Options.Count)];
    }
}

// Avalia cada opcao jogando a proxima fase varias vezes com ela (politica equilibrada, preset sorteado) e fica com a de mais vitorias.
public sealed class RolloutRewardPolicy : IRewardPolicy
{
    private readonly int _rollouts;
    private readonly BalancedPolicy _evaluator = new BalancedPolicy();

    public RolloutRewardPolicy(int rollouts = 40)
    {
        _rollouts = rollouts;
    }

    public string Name => "simulada";

    public RewardOption Choose(RewardContext context)
    {
        var data = context.Setup.Data;
        var seed = context.Random.Next();
        return PolicyHelpers.PickBest(context.Options, option =>
        {
            var cards = option.IsCard ? context.LoadedCardIds.Append(option.Id).ToList() : context.LoadedCardIds;
            var advantages = option.IsCard ? context.LoadedAdvantageIds : context.LoadedAdvantageIds.Append(option.Id).ToList();

            // Mesma sequencia de sementes pra toda opcao, a diferenca entre elas vem so da recompensa.
            var random = new Random(seed);
            double score = 0.0;
            for (int i = 0; i < _rollouts; i++)
            {
                var phase = new PhaseSetup
                {
                    Data = data,
                    Archetype = context.Setup.Archetype,
                    Preset = data.Presets.Count > 0 ? data.Presets[random.Next(data.Presets.Count)] : null,
                    LoadedCardIds = cards,
                    LoadedAdvantageIds = advantages,
                    HandSizeOverride = context.Setup.HandSizeOverride,
                    VictoryTurnCountOverride = context.Setup.VictoryTurnCountOverride,
                };
                var game = PhaseSimulator.Run(phase, _evaluator, new GameSeeds(random.Next(), i));
                score += game.Outcome == GameOutcome.Victory ? 1.0 : (double)game.TurnsPlayed / (phase.VictoryTurnCount * 10);
            }
            return score / _rollouts;
        }, context.Random);
    }
}

public sealed class RunSetup
{
    public BalanceData Data { get; init; } = null!;
    public CardArchetype Archetype { get; init; }
    public int PhaseCount { get; init; } = 3;
    public int? HandSizeOverride { get; init; }
    public int? VictoryTurnCountOverride { get; init; }
}

public sealed class PhaseRunRecord
{
    public int Phase { get; init; }
    public string PresetId { get; init; } = "";
    public GameRecord Game { get; init; } = null!;
    public IReadOnlyList<string> OfferedIds { get; init; } = Array.Empty<string>();
    public string ChosenId { get; init; } = "";

    // Recompensa que nao muda nada: carta que o baralho ja tinha, ou vantagem ja carregada.
    public bool ChosenWasRedundant { get; init; }
    public int RedundantOffered { get; init; }
}

public sealed class RunRecord
{
    public int PhasesWon { get; init; }
    public IReadOnlyList<PhaseRunRecord> Phases { get; init; } = Array.Empty<PhaseRunRecord>();
}

// Rodada inteira: fase 1 na cidade padrao, fases seguintes com preset sorteado e recompensas carregadas, ate perder ou completar.
public static class RunSimulator
{
    public static RunRecord Run(RunSetup setup, IPlayerPolicy policy, IRewardPolicy rewardPolicy, int baseSeed, int runIndex)
    {
        var data = setup.Data;
        var runRandom = new Random(new GameSeeds(baseSeed ^ 0x5BD1E995, runIndex).Hand);
        var loadedCards = new List<string>();
        var loadedAdvantages = new List<string>();
        var phases = new List<PhaseRunRecord>();
        var won = 0;

        for (int phase = 1; phase <= setup.PhaseCount; phase++)
        {
            SimPreset preset = null;
            if (phase > 1 && data.Presets.Count > 0)
                preset = data.Presets[runRandom.Next(data.Presets.Count)];

            var phaseSetup = new PhaseSetup
            {
                Data = data,
                Archetype = setup.Archetype,
                Preset = preset,
                LoadedCardIds = loadedCards.ToList(),
                LoadedAdvantageIds = loadedAdvantages.ToList(),
                HandSizeOverride = setup.HandSizeOverride,
                VictoryTurnCountOverride = setup.VictoryTurnCountOverride,
            };
            var game = PhaseSimulator.Run(phaseSetup, policy, new GameSeeds(baseSeed, runIndex * 16 + phase));
            var presetId = preset?.Id ?? SimulationRunner.BaseSetupName;

            if (game.Outcome != GameOutcome.Victory)
            {
                phases.Add(new PhaseRunRecord { Phase = phase, PresetId = presetId, Game = game });
                break;
            }

            won++;
            var options = DrawOptions(data, runRandom);
            var deck = DeckBuilder.Build(data.Cards, setup.Archetype, loadedCards);
            bool IsRedundant(RewardOption option) => option.IsCard
                ? deck.Any(card => card.Id == option.Id)
                : loadedAdvantages.Contains(option.Id);

            var chosen = rewardPolicy.Choose(new RewardContext
            {
                Options = options,
                Setup = setup,
                LoadedCardIds = loadedCards.ToList(),
                LoadedAdvantageIds = loadedAdvantages.ToList(),
                Random = runRandom,
            });

            var chosenWasRedundant = IsRedundant(chosen);
            var redundantOffered = options.Count(IsRedundant);

            // Mesma regra da meta-progressao do jogo: repetido nao entra de novo.
            var list = chosen.IsCard ? loadedCards : loadedAdvantages;
            if (list.Contains(chosen.Id) == false)
                list.Add(chosen.Id);

            phases.Add(new PhaseRunRecord
            {
                Phase = phase,
                PresetId = presetId,
                Game = game,
                OfferedIds = options.Select(option => option.Id).ToList(),
                ChosenId = chosen.Id,
                ChosenWasRedundant = chosenWasRedundant,
                RedundantOffered = redundantOffered,
            });
        }

        return new RunRecord { PhasesWon = won, Phases = phases };
    }

    // Mesmo pool do popup de recompensa do jogo: todas as cartas cadastradas e todas as vantagens.
    public static List<RewardOption> DrawOptions(BalanceData data, Random random)
    {
        var pool = data.Cards.Select(card => new RewardOption { Id = card.Id, IsCard = true })
            .Concat(data.Advantages.Select(advantage => new RewardOption { Id = advantage.Id, IsCard = false }))
            .ToList();
        return RewardOptionPicker.Draw(pool, data.Rules.RewardOptionCount, random);
    }
}
