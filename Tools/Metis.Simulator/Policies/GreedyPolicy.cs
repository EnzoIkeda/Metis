namespace Metis.Simulator.Policies;

// Olha um turno a frente: simula jogar cada carta e resolver o turno, fica com a de melhor estado resultante. Nao preve o evento.
public sealed class GreedyPolicy : IPlayerPolicy
{
    public string Name => "gulosa";

    public SimCard Choose(DecisionContext context)
    {
        var candidates = PolicyHelpers.DistinctById(context.PlayableCards);
        return PolicyHelpers.PickBest(candidates, card =>
        {
            var future = context.Stats.Clone();
            PolicyHelpers.ApplyCard(future, card);
            future.ResolveTurn();
            return StateScore.Evaluate(future, context.Data);
        }, context.Random);
    }
}
