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
