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
    public IReadOnlyList<string> AcceptedTradeIds { get; init; } = Array.Empty<string>();
    public Random Random { get; init; } = null!;
}

// O que quem responde a oferta de troca enxerga na parada do mapa.
public sealed class TradeContext
{
    public SimTradeOffer Offer { get; init; } = null!;
    public RunSetup Setup { get; init; } = null!;
    public IReadOnlyList<string> LoadedCardIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> LoadedAdvantageIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AcceptedTradeIds { get; init; } = Array.Empty<string>();
    public Random Random { get; init; } = null!;
}

public interface ITradePolicy
{
    string Name { get; }

    bool Accept(TradeContext context);
}

// Aceita ou recusa na sorte, como quem joga sem decidir.
public sealed class RandomTradePolicy : ITradePolicy
{
    public string Name => "aleatoria";

    public bool Accept(TradeContext context)
    {
        return context.Random.Next(2) == 0;
    }
}

// Joga a proxima fase varias vezes com e sem a troca, nas mesmas sementes, e aceita se ganhar mais.
public sealed class RolloutTradePolicy : ITradePolicy
{
    private readonly int _rollouts;
    private readonly BalancedPolicy _evaluator = new BalancedPolicy();

    public RolloutTradePolicy(int rollouts = 40)
    {
        _rollouts = rollouts;
    }

    public string Name => "simulada";

    public bool Accept(TradeContext context)
    {
        var seed = context.Random.Next();
        var with = context.AcceptedTradeIds.Append(context.Offer.Id).ToList();
        return Rollouts.Score(context.Setup, context.LoadedCardIds, context.LoadedAdvantageIds, with, _evaluator, _rollouts, seed)
            > Rollouts.Score(context.Setup, context.LoadedCardIds, context.LoadedAdvantageIds, context.AcceptedTradeIds, _evaluator, _rollouts, seed);
    }
}

// Nota media da proxima fase, com preset sorteado, para comparar escolhas da rodada nas mesmas sementes.
public static class Rollouts
{
    public static double Score(RunSetup setup, IReadOnlyList<string> cards, IReadOnlyList<string> advantages, IReadOnlyList<string> trades,
        IPlayerPolicy evaluator, int rollouts, int seed)
    {
        var data = setup.Data;
        var random = new Random(seed);
        double score = 0.0;
        for (int i = 0; i < rollouts; i++)
        {
            var phase = new PhaseSetup
            {
                Data = data,
                Archetype = setup.Archetype,
                Preset = data.Presets.Count > 0 ? data.Presets[random.Next(data.Presets.Count)] : null,
                LoadedCardIds = cards,
                LoadedAdvantageIds = advantages,
                AcceptedTradeIds = trades,
                FiniteDeck = setup.FiniteDeck,
                HandSizeOverride = setup.HandSizeOverride,
                VictoryTurnCountOverride = setup.VictoryTurnCountOverride,
            };
            var game = PhaseSimulator.Run(phase, evaluator, new GameSeeds(random.Next(), i));
            score += game.Outcome == GameOutcome.Victory ? 1.0 : (double)game.TurnsPlayed / (phase.VictoryTurnCount * 10);
        }
        return score / rollouts;
    }
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
        var seed = context.Random.Next();
        return PolicyHelpers.PickBest(context.Options, option =>
        {
            var cards = option.IsCard ? context.LoadedCardIds.Append(option.Id).ToList() : context.LoadedCardIds;
            var advantages = option.IsCard ? context.LoadedAdvantageIds : context.LoadedAdvantageIds.Append(option.Id).ToList();

            // Mesma sequencia de sementes pra toda opcao, a diferenca entre elas vem so da recompensa.
            return Rollouts.Score(context.Setup, cards, advantages, context.AcceptedTradeIds, _evaluator, _rollouts, seed);
        }, context.Random);
    }
}

public sealed class RunSetup
{
    public BalanceData Data { get; init; } = null!;
    public CardArchetype Archetype { get; init; }
    public int PhaseCount { get; init; } = 3;
    public bool FiniteDeck { get; init; }
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

    // Oferta de troca na parada depois desta fase; vazia quando nao houve parada ou oferta.
    public string TradeOfferId { get; init; } = "";
    public bool TradeAccepted { get; init; }
}

public sealed class RunRecord
{
    public int PhasesWon { get; init; }
    public IReadOnlyList<PhaseRunRecord> Phases { get; init; } = Array.Empty<PhaseRunRecord>();
}

// Rodada inteira: fase 1 na cidade padrao, fases seguintes com preset sorteado e recompensas carregadas, ate perder ou completar.
public static class RunSimulator
{
    // Escolha sorteada entre os destinos do mapa; sem temas, e o preset sorteado de sempre.
    private static SimPreset DrawDestination(BalanceData data, List<string> playedThemes, Random random)
    {
        var offer = PhaseMapProvider.BuildOffer(data.Themes.Select(theme => theme.Id).ToList(), playedThemes, random);
        var node = offer.Count > 1 ? offer[random.Next(offer.Count)] : offer[0];
        if (node.Type == PhaseMapNodeType.Themed)
        {
            playedThemes.Add(node.Id);
            return data.Themes.First(theme => theme.Id == node.Id);
        }
        return data.Presets.Count > 0 ? data.Presets[random.Next(data.Presets.Count)] : null;
    }

    public static RunRecord Run(RunSetup setup, IPlayerPolicy policy, IRewardPolicy rewardPolicy, int baseSeed, int runIndex, ITradePolicy tradePolicy = null)
    {
        var data = setup.Data;
        var runRandom = new Random(new GameSeeds(baseSeed ^ 0x5BD1E995, runIndex).Hand);

        // Gerador proprio da troca: com todas recusadas, destinos, recompensas e partidas saem iguais a uma rodada sem troca.
        var tradeRandom = new Random(new GameSeeds(baseSeed ^ 0x2545F491, runIndex).Hand);
        var loadedCards = new List<string>();
        var loadedAdvantages = new List<string>();
        var acceptedTrades = new List<string>();
        var offeredTrades = new List<string>();
        var phases = new List<PhaseRunRecord>();
        var playedThemes = new List<string>();
        var won = 0;

        for (int phase = 1; phase <= setup.PhaseCount; phase++)
        {
            SimPreset preset = null;
            if (phase > 1)
                preset = DrawDestination(data, playedThemes, runRandom);

            var phaseSetup = new PhaseSetup
            {
                Data = data,
                Archetype = setup.Archetype,
                Preset = preset,
                LoadedCardIds = loadedCards.ToList(),
                LoadedAdvantageIds = loadedAdvantages.ToList(),
                AcceptedTradeIds = acceptedTrades.ToList(),
                FiniteDeck = setup.FiniteDeck,
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
            var trades = TradeOfferRules.Resolve(data.TradeOffers, acceptedTrades);
            var deck = DeckBuilder.Build(data.Cards, setup.Archetype, loadedCards, TradeOfferRules.ExtraCardIds(trades));
            var options = DrawOptions(data, deck, loadedAdvantages, runRandom, TradeOfferRules.RewardOptionCount(data.Rules.RewardOptionCount, trades));

            // Parada do mapa antes da proxima fase: uma oferta de troca, como no jogo.
            string tradeId = "";
            var tradeAccepted = false;
            if (phase < setup.PhaseCount && tradePolicy != null)
            {
                var offer = TradeOfferRules.Draw(data.TradeOffers, offeredTrades, tradeRandom);
                if (offer != null)
                {
                    offeredTrades.Add(offer.Id);
                    tradeId = offer.Id;
                }
            }

            // Tudo ja pego: o jogo pula a recompensa e segue pra proxima fase.
            if (options.Count == 0)
            {
                tradeAccepted = DecideTrade(tradeId, tradePolicy, setup, loadedCards, loadedAdvantages, acceptedTrades, tradeRandom);
                phases.Add(new PhaseRunRecord { Phase = phase, PresetId = presetId, Game = game, TradeOfferId = tradeId, TradeAccepted = tradeAccepted });
                continue;
            }

            bool IsRedundant(RewardOption option) => option.IsCard
                ? deck.Any(card => card.Id == option.Id)
                : loadedAdvantages.Contains(option.Id);

            var chosen = rewardPolicy.Choose(new RewardContext
            {
                Options = options,
                Setup = setup,
                LoadedCardIds = loadedCards.ToList(),
                LoadedAdvantageIds = loadedAdvantages.ToList(),
                AcceptedTradeIds = acceptedTrades.ToList(),
                Random = runRandom,
            });

            var chosenWasRedundant = IsRedundant(chosen);
            var redundantOffered = options.Count(IsRedundant);

            // Mesma regra da meta-progressao do jogo: repetido nao entra de novo.
            var list = chosen.IsCard ? loadedCards : loadedAdvantages;
            if (list.Contains(chosen.Id) == false)
                list.Add(chosen.Id);

            // No jogo a oferta aparece no mapa, depois da recompensa.
            tradeAccepted = DecideTrade(tradeId, tradePolicy, setup, loadedCards, loadedAdvantages, acceptedTrades, tradeRandom);

            phases.Add(new PhaseRunRecord
            {
                Phase = phase,
                PresetId = presetId,
                Game = game,
                OfferedIds = options.Select(option => option.Id).ToList(),
                ChosenId = chosen.Id,
                ChosenWasRedundant = chosenWasRedundant,
                RedundantOffered = redundantOffered,
                TradeOfferId = tradeId,
                TradeAccepted = tradeAccepted,
            });
        }

        return new RunRecord { PhasesWon = won, Phases = phases };
    }

    private static bool DecideTrade(string tradeId, ITradePolicy tradePolicy, RunSetup setup, List<string> loadedCards, List<string> loadedAdvantages,
        List<string> acceptedTrades, Random tradeRandom)
    {
        if (string.IsNullOrEmpty(tradeId) || tradePolicy == null)
            return false;
        var accepted = tradePolicy.Accept(new TradeContext
        {
            Offer = setup.Data.TradeOffers.First(offer => offer.Id == tradeId),
            Setup = setup,
            LoadedCardIds = loadedCards.ToList(),
            LoadedAdvantageIds = loadedAdvantages.ToList(),
            AcceptedTradeIds = acceptedTrades.ToList(),
            Random = tradeRandom,
        });
        if (accepted)
            acceptedTrades.Add(tradeId);
        return accepted;
    }

    // Mesmo pool do popup de recompensa do jogo: cartas fora do baralho atual e vantagens ainda nao carregadas (D8).
    public static List<RewardOption> DrawOptions(BalanceData data, IReadOnlyList<SimCard> deck, IReadOnlyList<string> loadedAdvantageIds, Random random, int? count = null)
    {
        var pool = RewardOptionPicker.CardPool(data.Cards, deck).Select(card => new RewardOption { Id = card.Id, IsCard = true })
            .Concat(RewardOptionPicker.AdvantagePool(data.Advantages, loadedAdvantageIds).Select(advantage => new RewardOption { Id = advantage.Id, IsCard = false }))
            .ToList();
        return RewardOptionPicker.Draw(pool, count ?? data.Rules.RewardOptionCount, random);
    }
}
