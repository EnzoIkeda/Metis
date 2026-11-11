using Metis.Simulator.Policies;

namespace Metis.Simulator;

// Avanco de turno sem a maquina de turno, pra quem planeja simular futuros a partir de qualquer turno da fase.
public static class ForwardModel
{
    // Mesma ordem do jogo a partir da fase de acao: carta (ou nenhuma), resolucao, evento, checagem de fim.
    // chooseOption responde os dilemas que sairem no caminho.
    public static GameOutcome StepTurn(CityStats stats, SimCard card, int turnIndex, int victoryTurnCount, RandomEventPool<SimEvent> events,
        Func<CityStats, SimEvent, int> chooseOption = null)
    {
        if (card != null)
            PolicyHelpers.ApplyCard(stats, card);

        return Resolve(stats, turnIndex, victoryTurnCount, events, chooseOption);
    }

    // Continua um turno parado no dilema: aplica a opcao e faz a checagem de fim.
    public static GameOutcome FinishDilemma(CityStats stats, SimEvent dilemma, int option, int turnIndex, int victoryTurnCount, RandomEventPool<SimEvent> events)
    {
        events.ApplyChoice(stats, dilemma, option, turnIndex);
        return CheckEnd(stats, turnIndex, victoryTurnCount);
    }

    // Acao livre simplificada: paga o custo e segue com a melhor carta comum pela politica equilibrada.
    public static GameOutcome StepTurnWithAbilities(CityStats stats, SimCard card, CardHand<SimCard> hand, int turnIndex, int victoryTurnCount,
        RandomEventPool<SimEvent> events, BalanceData data, Func<CityStats, SimEvent, int> chooseOption = null)
    {
        if (card == null || CardRules.IsFreeAction(card) == false)
            return StepTurn(stats, card, turnIndex, victoryTurnCount, events, chooseOption);

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
        return StepTurn(stats, follow, turnIndex, victoryTurnCount, events, chooseOption);
    }

    private static GameOutcome Resolve(CityStats stats, int turnIndex, int victoryTurnCount, RandomEventPool<SimEvent> events, Func<CityStats, SimEvent, int> chooseOption)
    {
        stats.ResolveTurn();
        var triggered = events.TryTriggerEvent(stats, turnIndex);
        if (triggered != null && triggered.Choices.Count > 0)
        {
            if (chooseOption == null)
                throw new InvalidOperationException($"Dilema '{triggered.Id}' sem quem escolha a opcao.");
            events.ApplyChoice(stats, triggered, chooseOption(stats, triggered), turnIndex);
        }

        return CheckEnd(stats, turnIndex, victoryTurnCount);
    }

    private static GameOutcome CheckEnd(CityStats stats, int turnIndex, int victoryTurnCount)
    {
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
