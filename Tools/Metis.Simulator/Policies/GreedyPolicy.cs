namespace Metis.Simulator.Policies;

// Olha um turno a frente: simula jogar cada carta e resolver o turno, fica com a de melhor estado resultante. Nao preve o evento.
public sealed class GreedyPolicy : IPlayerPolicy
{
    public string Name => "gulosa";

    public SimCard Choose(DecisionContext context)
    {
        var candidates = PolicyHelpers.DistinctById(context.PlayableCards);
        return PolicyHelpers.PickBest(candidates, card => CardRules.IsFreeAction(card)
            ? PolicyHelpers.ScoreWithAbilities(card, context, plain => Evaluate(context, card, plain), _ => 0.0)
            : Evaluate(context, null, card), context.Random);
    }

    public SimCard ChooseSearch(DecisionContext context, IReadOnlyList<SimCard> candidates)
    {
        return PolicyHelpers.PickBest(PolicyHelpers.DistinctById(candidates), card => Evaluate(context, null, card), context.Random);
    }

    // Busca e revelacao entram pelo custo pago antes da carta comum, por isso o custo e aplicado no mesmo estado simulado.
    private static double Evaluate(DecisionContext context, SimCard paidFirst, SimCard card)
    {
        return GreedyLookahead.Score(context.Stats, card, context.Data.ScoreRules, paidFirst);
    }
}
