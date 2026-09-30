namespace Metis.Simulator.Policies;

// Joga qualquer carta jogavel, com chance igual. Piso de referencia: o quanto o jogo e ganhavel sem decisao nenhuma.
public sealed class RandomPolicy : IPlayerPolicy
{
    public string Name => "aleatoria";

    public SimCard Choose(DecisionContext context)
    {
        return context.PlayableCards[context.Random.Next(context.PlayableCards.Count)];
    }
}
