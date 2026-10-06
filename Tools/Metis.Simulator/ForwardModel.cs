using Metis.Simulator.Policies;

namespace Metis.Simulator;

// Avanco de turno sem a maquina de turno, pra quem planeja simular futuros a partir de qualquer turno da fase.
public static class ForwardModel
{
    // Mesma ordem do jogo a partir da fase de acao: carta (ou nenhuma), resolucao, evento, checagem de fim.
    public static GameOutcome StepTurn(CityStats stats, SimCard card, int turnIndex, int victoryTurnCount, RandomEventPool<SimEvent> events)
    {
        if (card != null)
            PolicyHelpers.ApplyCard(stats, card);

        return Resolve(stats, turnIndex, victoryTurnCount, events);
    }

    // Planejamento simplificado de acao livre: busca e revelacao pagam o custo e o turno segue com a melhor carta comum
    // pela politica equilibrada (a trazida pela busca, ou a melhor da mao). Carta comum segue o passo normal.
    public static GameOutcome StepTurnWithAbilities(CityStats stats, SimCard card, CardHand<SimCard> hand, int turnIndex, int victoryTurnCount,
        RandomEventPool<SimEvent> events, BalanceData data)
    {
        if (card == null || CardRules.IsFreeAction(card) == false)
            return StepTurn(stats, card, turnIndex, victoryTurnCount, events);

        PolicyHelpers.ApplyCard(stats, card);
        var options = card.Ability == CardAbility.SearchDeck
            ? hand.SearchCandidates(stats)
            : hand.Cards.Where(other => CardRules.IsFreeAction(other) == false && hand.CanPlay(other, stats)).ToList();

        SimCard follow = null;
        if (options.Count > 0)
        {
            var weights = BalancedPolicy.BuildWeights(stats, data);
            follow = options.OrderByDescending(option => BalancedPolicy.Score(option, weights)).First();
        }
        return StepTurn(stats, follow, turnIndex, victoryTurnCount, events);
    }

    private static GameOutcome Resolve(CityStats stats, int turnIndex, int victoryTurnCount, RandomEventPool<SimEvent> events)
    {
        stats.ResolveTurn();
        events.TryTriggerEvent(stats, turnIndex);

        if (stats.IsAnchorCritical())
            return GameOutcome.GameOver;
        if (turnIndex >= victoryTurnCount)
            return GameOutcome.Victory;
        return GameOutcome.None;
    }

    // Descarta a mao anterior e compra a do turno, devolvendo so as cartas jogaveis.
    public static List<SimCard> DrawPlayable(CardHand<SimCard> hand, int handSize, CityStats stats)
    {
        hand.DiscardAll();
        hand.Draw(handSize, stats);
        return hand.Cards.Where(card => hand.CanPlay(card, stats)).ToList();
    }
}
