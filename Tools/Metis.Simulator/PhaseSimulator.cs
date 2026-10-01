using Metis.Simulator.Policies;

namespace Metis.Simulator;

// Configuracao de uma fase simulada: o que muda entre fase 1, fases com preset e rodadas com recompensa carregada.
public sealed class PhaseSetup
{
    public BalanceData Data { get; init; } = null!;
    public CardArchetype Archetype { get; init; }

    // Null usa a cidade calibrada padrao, igual a fase 1 do jogo.
    public SimPreset Preset { get; init; }
    public IReadOnlyList<string> LoadedCardIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> LoadedAdvantageIds { get; init; } = Array.Empty<string>();

    // Sobrescrevem as regras exportadas, pra varrer alavancas sem reexportar.
    public int? HandSizeOverride { get; init; }
    public int? VictoryTurnCountOverride { get; init; }

    public int HandSize => HandSizeOverride ?? Data.Rules.HandSize;
    public int VictoryTurnCount => VictoryTurnCountOverride ?? Data.Rules.VictoryTurnCount;
}

// Sementes independentes por fonte de sorte, derivadas de uma so, pra mesma partida repetir igual em qualquer politica.
public readonly struct GameSeeds
{
    public int Hand { get; }
    public int Events { get; }
    public int Policy { get; }

    public GameSeeds(int baseSeed, int gameIndex)
    {
        Hand = Derive(baseSeed, gameIndex, 1);
        Events = Derive(baseSeed, gameIndex, 2);
        Policy = Derive(baseSeed, gameIndex, 3);
    }

    // Mistura estilo SplitMix64, sementes vizinhas viram sementes sem correlacao.
    private static int Derive(int baseSeed, int gameIndex, int stream)
    {
        unchecked
        {
            ulong z = ((ulong)(uint)baseSeed << 32) ^ ((ulong)(uint)gameIndex * 0x9E3779B97F4A7C15UL) ^ ((ulong)stream * 0xBF58476D1CE4E5B9UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(z & 0x7FFFFFFF);
        }
    }
}

public sealed class TurnRecord
{
    public int Turn { get; init; }

    // Vazio quando nenhuma carta da mao era jogavel.
    public string CardId { get; init; } = "";
    public int PlayableCount { get; init; }

    // Ids das cartas jogaveis na mao naquele turno, com repeticao, pra medir taxa de escolha.
    public IReadOnlyList<string> PlayableIds { get; init; } = Array.Empty<string>();

    // Vazio quando nenhum evento era elegivel.
    public string EventId { get; init; } = "";

    // Estado no fim do turno, depois do evento, que e o que a checagem de derrota enxerga.
    public float[] Values { get; init; } = Array.Empty<float>();
    public int Collapsed { get; init; }
}

public sealed class GameRecord
{
    public GameOutcome Outcome { get; init; }
    public int TurnsPlayed { get; init; }
    public IReadOnlyList<TurnRecord> Turns { get; init; } = Array.Empty<TurnRecord>();

    // Primeiro turno em que cada limiar de tier foi alcancado, 0 se nunca.
    public IReadOnlyList<int> TierReachedTurn { get; init; } = Array.Empty<int>();
}

public static class PhaseSimulator
{
    public static readonly CityParameterType[] Parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));

    // Mesma ordem do jogo: vantagens carregadas, mao comprada no inicio do turno, jogada, resolucao, evento, checagem de fim.
    public static GameRecord Run(PhaseSetup setup, IPlayerPolicy policy, GameSeeds seeds)
    {
        var data = setup.Data;
        var stats = new CityStats(data.BuildParameterConfigs(setup.Preset), data.BuildInteractionMatrix(), data.Interaction.PenalidadeColapso);
        foreach (var advantageId in setup.LoadedAdvantageIds)
        {
            var advantage = data.Advantages.FirstOrDefault(candidate => candidate.Id == advantageId);
            if (advantage != null)
                stats.ApplyModifiers(advantage.StatEffects);
        }

        var machine = new TurnMachine(stats, setup.VictoryTurnCount);
        var deck = DeckBuilder.Build(data.Cards, setup.Archetype, setup.LoadedCardIds);
        var hand = new CardHand<SimCard>(deck, new Random(seeds.Hand));
        var events = new RandomEventPool<SimEvent>(data.Events, new Random(seeds.Events));
        var policyRandom = new Random(seeds.Policy);

        machine.OnPhaseChanged += phase =>
        {
            if (phase == TurnPhase.StartOfTurn)
                hand.Draw(setup.HandSize, stats);
            else if (phase == TurnPhase.Advance)
                hand.DiscardAll();
        };

        var thresholds = data.TierThresholds;
        var tierReached = new int[thresholds.Count];
        var turns = new List<TurnRecord>();

        machine.StartGame();
        while (machine.Outcome == GameOutcome.None)
        {
            var turn = machine.TurnIndex;
            var playable = hand.Cards.Where(card => hand.CanPlay(card, stats)).ToList();
            var cardId = "";
            if (playable.Count > 0)
            {
                var chosen = policy.Choose(new DecisionContext
                {
                    Stats = stats,
                    PlayableCards = playable,
                    TurnIndex = turn,
                    Data = data,
                    Random = policyRandom,
                    Deck = deck,
                    Events = data.Events,
                    HandSize = setup.HandSize,
                    VictoryTurnCount = setup.VictoryTurnCount,
                });
                if (chosen.PlacesStructure)
                    throw new NotSupportedException($"Carta '{chosen.Id}' coloca estrutura no grid, o simulador nao modela o grid.");
                if (hand.TryPlay(chosen, stats) == false)
                    throw new InvalidOperationException($"Politica '{policy.Name}' escolheu '{chosen.Id}', que nao era jogavel.");
                cardId = chosen.Id;
            }

            machine.EndActionPhase();
            var triggered = events.TryTriggerEvent(stats, turn);

            var values = Parameters.Select(stats.GetValue).ToArray();
            for (int i = 0; i < thresholds.Count; i++)
            {
                if (tierReached[i] == 0 && stats.GetValue(CityParameterType.Pesquisa) >= thresholds[i])
                    tierReached[i] = turn;
            }

            turns.Add(new TurnRecord
            {
                Turn = turn,
                CardId = cardId,
                PlayableCount = playable.Count,
                PlayableIds = playable.Select(card => card.Id).ToList(),
                EventId = triggered?.Id ?? "",
                Values = values,
                Collapsed = stats.CountCollapsedParameters(),
            });

            machine.AcknowledgeEvent();
        }

        return new GameRecord
        {
            Outcome = machine.Outcome,
            TurnsPlayed = turns.Count,
            Turns = turns,
            TierReachedTurn = tierReached,
        };
    }
}
