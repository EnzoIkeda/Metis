using System;
using System.Collections.Generic;

// Estado salvo de uma rodada (baralho escolhido, recompensas carregadas, fases completadas).
[Serializable]
public class MetaProgressionState
{
    public CardArchetype Archetype;
    public List<string> LoadedCardNames = new List<string>();
    public List<string> LoadedAdvantageNames = new List<string>();
    public int PhasesCompleted;
}

[Serializable]
public struct ParameterValueSave
{
    public CityParameterType Parameter;
    public float Value;
}

// Fase em andamento, gravada nos pontos estaveis do turno: comeco da fase de acao (mao comprada, nada pendente),
// depois de cada acao livre, e com o popup de evento aberto (carta ja jogada, evento ja aplicado).
[Serializable]
public class PhaseSaveData
{
    public int TurnIndex;

    // Parou com o popup de evento aberto; o evento ja foi aplicado e so falta ser confirmado.
    public bool AtEvent;
    public string PendingEventId = string.Empty;

    // Nome do asset do preset sorteado; vazio na cidade calibrada da fase 1.
    public string PresetName = string.Empty;
    public ulong LayoutSeed;
    public int BackgroundIndex = -1;

    public List<ParameterValueSave> Parameters = new List<ParameterValueSave>();
    public float AnchorBonus;

    public List<string> Hand = new List<string>();
    public List<string> DrawPile = new List<string>();
    public List<string> DiscardPile = new List<string>();
    public bool IsRevealed;
    public bool RevealedUntilPhaseEnd;

    public ulong HandRandomState;
    public ulong EventRandomState;

    // Historico do relatorio de fim de fase: valores iniciais, turnos fechados e o turno em andamento.
    public float[] HistoryStart = Array.Empty<float>();
    public List<PhaseTurnRecord> History = new List<PhaseTurnRecord>();
    public bool HasHistoryCurrent;
    public PhaseTurnRecord HistoryCurrent = new PhaseTurnRecord();

    // Jornal da fase: semente das edicoes, as edicoes publicadas e o ultimo turno lido.
    public ulong NewsSeed;
    public List<NewsEdition> NewsEditions = new List<NewsEdition>();
    public int NewsLastReadTurn = -1;
}

// Arquivo de save inteiro: meta-progressao e, quando houver, a fase em andamento.
[Serializable]
public class RunSaveData
{
    // Subir sempre que o formato mudar de um jeito que um save antigo nao possa mais ser lido.
    public const int CurrentVersion = 1;

    public int Version = CurrentVersion;
    public MetaProgressionState Meta = new MetaProgressionState();

    // A rodada parou no mapa entre fases, e nao dentro de uma fase.
    public bool InPhaseMap;

    public bool HasPhase;
    public PhaseSaveData Phase = new PhaseSaveData();
}

// Regras puras de captura, restauracao e validacao do save, sem dependencia de Unity.
public static class RunSaveRules
{
    public static bool IsCompatible(RunSaveData save)
    {
        return save != null && save.Version == RunSaveData.CurrentVersion && save.Meta != null;
    }

    // Guarda o que a fase de acao precisa pra recomecar igual: parametros, mao, pilhas e geradores aleatorios.
    public static PhaseSaveData CapturePhase<TCard>(int turnIndex, CityStats stats, CardHand<TCard> hand, SeededRandom handRandom, SeededRandom eventRandom)
        where TCard : class, ICardDefinition
    {
        var phase = new PhaseSaveData
        {
            TurnIndex = turnIndex,
            AnchorBonus = stats.AnchorBonus,
            IsRevealed = hand.IsRevealed,
            RevealedUntilPhaseEnd = hand.RevealedUntilPhaseEnd,
            HandRandomState = handRandom.State,
            EventRandomState = eventRandom.State,
        };

        foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
            phase.Parameters.Add(new ParameterValueSave { Parameter = parameter, Value = stats.GetValue(parameter) });

        AddIds(phase.Hand, hand.Cards);
        AddIds(phase.DrawPile, hand.DrawPile);
        AddIds(phase.DiscardPile, hand.DiscardPile);
        return phase;
    }

    public static void CaptureHistory(PhaseSaveData phase, PhaseHistory history)
    {
        phase.HistoryStart = history.StartValues;
        phase.History = new List<PhaseTurnRecord>(history.Turns);
        phase.HasHistoryCurrent = history.Current != null;
        phase.HistoryCurrent = history.Current ?? new PhaseTurnRecord();
    }

    // Save sem historico, de antes do relatorio, volta vazio: o relatorio mostra so os turnos dali em diante.
    public static PhaseHistory RestoreHistory(PhaseSaveData phase)
    {
        var current = phase.HasHistoryCurrent ? phase.HistoryCurrent : null;
        return new PhaseHistory(phase.HistoryStart, phase.History, current);
    }

    public static void CaptureNews(PhaseSaveData phase, NewsArchive news)
    {
        phase.NewsSeed = news.Seed;
        phase.NewsEditions = new List<NewsEdition>(news.Editions);
        phase.NewsLastReadTurn = news.LastReadTurn;
    }

    // Save de antes do jornal vem sem semente; fresh da a semente nova nesse caso.
    public static NewsArchive RestoreNews(PhaseSaveData phase, ulong freshSeed)
    {
        var seed = phase.NewsSeed != 0UL ? phase.NewsSeed : freshSeed;
        return new NewsArchive(seed, phase.NewsEditions, phase.NewsLastReadTurn);
    }

    public static void RestoreStats(PhaseSaveData phase, CityStats stats)
    {
        var values = new Dictionary<CityParameterType, float>();
        foreach (var entry in phase.Parameters)
            values[entry.Parameter] = entry.Value;
        stats.RestoreState(values, phase.AnchorBonus);
    }

    // Remonta a mao com as pilhas na ordem salva; falha se algum id nao existir mais no jogo.
    public static bool TryRestoreHand<TCard>(PhaseSaveData phase, Func<string, TCard> findCard, Random random, out CardHand<TCard> hand)
        where TCard : class, ICardDefinition
    {
        hand = null;
        if (TryResolve(phase.Hand, findCard, out var cards) == false
            || TryResolve(phase.DrawPile, findCard, out var drawPile) == false
            || TryResolve(phase.DiscardPile, findCard, out var discardPile) == false)
            return false;

        hand = CardHand<TCard>.FromSavedState(cards, drawPile, discardPile, phase.IsRevealed, phase.RevealedUntilPhaseEnd, random);
        return true;
    }

    public static bool TryResolve<TCard>(IEnumerable<string> ids, Func<string, TCard> findCard, out List<TCard> cards)
        where TCard : class
    {
        cards = new List<TCard>();
        if (ids == null)
            return true;

        foreach (var id in ids)
        {
            var card = findCard(id);
            if (card == null)
                return false;
            cards.Add(card);
        }
        return true;
    }

    private static void AddIds<TCard>(List<string> target, IEnumerable<TCard> cards) where TCard : class, ICardDefinition
    {
        foreach (var card in cards)
            target.Add(card.Id);
    }
}
