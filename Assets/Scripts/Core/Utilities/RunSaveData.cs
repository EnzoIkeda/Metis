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

    // Fase tematica escolhida no mapa, vazia na cidade desconhecida, e temas ja jogados na rodada.
    public string CurrentTheme = string.Empty;
    public List<string> PlayedThemes = new List<string>();

    // Cenario em jogo; vazio na rodada normal.
    public string ScenarioId = string.Empty;

    // Trocas aceitas no mapa, que valem ate o fim da rodada, e todas as ja oferecidas, para nao repetir.
    public List<string> AcceptedTrades = new List<string>();
    public List<string> OfferedTrades = new List<string>();

    // Troca aceita que a edicao de posse da proxima fase ainda vai noticiar.
    public string PendingTradeNews = string.Empty;

    // Chefe da fase em andamento; vazio fora da fase do chefe.
    public string CurrentBoss = string.Empty;
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

    // Consequencias de dilemas ainda por acontecer nesta fase.
    public List<ScheduledFollowUp> FollowUps = new List<ScheduledFollowUp>();

    // Historico do relatorio de fim de fase: valores iniciais, turnos fechados e o turno em andamento.
    public float[] HistoryStart = Array.Empty<float>();
    public List<PhaseTurnRecord> History = new List<PhaseTurnRecord>();
    public bool HasHistoryCurrent;
    public PhaseTurnRecord HistoryCurrent = new PhaseTurnRecord();

    // Jornal da fase: semente das edicoes, as edicoes publicadas e o ultimo turno lido.
    public ulong NewsSeed;
    public List<NewsEdition> NewsEditions = new List<NewsEdition>();
    public int NewsLastReadTurn = -1;

    // Chefe da fase: Impeto, cartas ja jogadas, pilhas, gerador e a jogada com o popup aberto.
    public bool HasBoss;
    public int BossMomentum;
    public int BossCardsPlayed;
    public string BossNext = string.Empty;
    public List<string> BossDrawPile = new List<string>();
    public List<string> BossDiscardPile = new List<string>();
    public ulong BossRandomState;
    public bool AtBoss;
    public string PendingBossCardId = string.Empty;
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

    // Destinos sorteados na parada atual, para sair e voltar ao mapa mostrar os mesmos.
    public List<PhaseMapNode> MapOffer = new List<PhaseMapNode>();

    // Oferta de troca da parada atual e se o jogador ja respondeu.
    public string TradeOffer = string.Empty;
    public bool TradeOfferDecided;

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

    // Guarda o destino escolhido e encerra a oferta da parada.
    public static void ApplyMapChoice(RunSaveData save, PhaseMapNode node)
    {
        var themed = node.Type == PhaseMapNodeType.Themed;
        save.Meta.CurrentTheme = themed ? node.Id : string.Empty;
        save.Meta.CurrentBoss = node.Type == PhaseMapNodeType.Boss ? node.Id : string.Empty;
        if (themed && save.Meta.PlayedThemes.Contains(node.Id) == false)
            save.Meta.PlayedThemes.Add(node.Id);
        save.MapOffer.Clear();
        save.TradeOffer = string.Empty;
        save.TradeOfferDecided = false;
    }

    // Oferta de troca da parada, sorteada uma vez so; nula quando ja respondida ou quando nao sobrou nenhuma.
    public static TOffer ResolveTradeOffer<TOffer>(RunSaveData save, IReadOnlyList<TOffer> offers, Random random)
        where TOffer : class, ITradeOfferDefinition
    {
        if (save.TradeOfferDecided)
            return null;

        if (string.IsNullOrEmpty(save.TradeOffer) == false)
        {
            foreach (var offer in offers)
            {
                if (offer != null && offer.Id == save.TradeOffer)
                    return offer;
            }
        }

        var drawn = TradeOfferRules.Draw(offers, save.Meta.OfferedTrades, random);
        save.TradeOffer = drawn != null ? drawn.Id : string.Empty;
        if (drawn == null)
        {
            save.TradeOfferDecided = true;
            return null;
        }
        if (save.Meta.OfferedTrades.Contains(drawn.Id) == false)
            save.Meta.OfferedTrades.Add(drawn.Id);
        return drawn;
    }

    // Resposta do jogador a oferta da parada; aceita, ela vale ate o fim da rodada e vira noticia na posse.
    public static void ApplyTradeDecision(RunSaveData save, string offerId, bool accepted)
    {
        save.TradeOfferDecided = true;
        if (accepted == false || string.IsNullOrEmpty(offerId))
            return;
        if (save.Meta.AcceptedTrades.Contains(offerId) == false)
            save.Meta.AcceptedTrades.Add(offerId);
        save.Meta.PendingTradeNews = offerId;
    }

    // Guarda o chefe da fase; pendingCardId e a jogada com o popup aberto, ja aplicada.
    public static void CaptureBoss(PhaseSaveData phase, BossEncounter boss, SeededRandom bossRandom, string pendingCardId)
    {
        phase.HasBoss = boss != null;
        if (boss == null)
            return;
        phase.BossMomentum = boss.Momentum;
        phase.BossCardsPlayed = boss.CardsPlayed;
        phase.BossNext = boss.Next != null ? boss.Next.Id : string.Empty;
        phase.BossDrawPile = new List<string>();
        phase.BossDiscardPile = new List<string>();
        foreach (var card in boss.Deck.DrawPile)
            phase.BossDrawPile.Add(card.Id);
        foreach (var card in boss.Deck.DiscardPile)
            phase.BossDiscardPile.Add(card.Id);
        phase.BossRandomState = bossRandom != null ? bossRandom.State : 0UL;
        phase.AtBoss = string.IsNullOrEmpty(pendingCardId) == false;
        phase.PendingBossCardId = pendingCardId ?? string.Empty;
    }

    // Falha quando alguma carta salva nao existe mais no chefe.
    public static bool TryRestoreBoss(PhaseSaveData phase, IBossDefinition definition, out BossEncounter boss, out SeededRandom bossRandom)
    {
        boss = null;
        bossRandom = null;
        if (phase.HasBoss == false || definition == null)
            return false;

        if (TryResolveBossCards(definition, phase.BossDrawPile, out var drawPile) == false
            || TryResolveBossCards(definition, phase.BossDiscardPile, out var discardPile) == false)
            return false;
        var next = BossRules.FindCard(definition, phase.BossNext);
        if (string.IsNullOrEmpty(phase.BossNext) == false && next == null)
            return false;
        if (phase.AtBoss && BossRules.FindCard(definition, phase.PendingBossCardId) == null)
            return false;

        bossRandom = new SeededRandom(phase.BossRandomState);
        boss = BossEncounter.FromState(definition, BossDeck.FromState(next, drawPile, discardPile, bossRandom), phase.BossMomentum, phase.BossCardsPlayed);
        return true;
    }

    private static bool TryResolveBossCards(IBossDefinition definition, IEnumerable<string> ids, out List<IBossCardDefinition> cards)
    {
        cards = new List<IBossCardDefinition>();
        if (ids == null)
            return true;
        foreach (var id in ids)
        {
            var card = BossRules.FindCard(definition, id);
            if (card == null)
                return false;
            cards.Add(card);
        }
        return true;
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

    public static void CaptureSchedule(PhaseSaveData phase, EventSchedule schedule)
    {
        phase.FollowUps = new List<ScheduledFollowUp>(schedule.FollowUps);
    }

    // O que ja saiu na fase vem do historico, entao nao precisa de campo proprio no save.
    public static EventSchedule RestoreSchedule(PhaseSaveData phase, PhaseHistory history)
    {
        var seen = new List<string>();
        foreach (var turn in history.Turns)
            seen.Add(turn.EventId);
        if (history.Current != null)
            seen.Add(history.Current.EventId);
        return new EventSchedule(phase.FollowUps, seen);
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
