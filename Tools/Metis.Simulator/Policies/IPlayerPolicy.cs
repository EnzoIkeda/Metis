namespace Metis.Simulator.Policies;

// Tudo que uma politica enxerga na hora de escolher a carta do turno.
public sealed class DecisionContext
{
    public CityStats Stats { get; init; } = null!;
    public IReadOnlyList<SimCard> PlayableCards { get; init; } = Array.Empty<SimCard>();
    public int TurnIndex { get; init; }
    public BalanceData Data { get; init; } = null!;
    public Random Random { get; init; } = null!;

    // O que o jogador sabe do resto da fase, pra quem planeja adiante: baralho que a mao compra, eventos possiveis, regras.
    public IReadOnlyList<SimCard> Deck { get; init; } = Array.Empty<SimCard>();
    public IReadOnlyList<SimEvent> Events { get; init; } = Array.Empty<SimEvent>();
    public int HandSize { get; init; } = 5;
    public int VictoryTurnCount { get; init; } = TurnMachine.DefaultVictoryTurnCount;
}

// Jogador simulado: escolhe uma das cartas jogaveis da mao.
public interface IPlayerPolicy
{
    string Name { get; }

    SimCard Choose(DecisionContext context);
}

public static class PolicyHelpers
{
    // Aplica a carta como a mao aplicaria ao jogar: custo em Renda, depois os efeitos.
    public static void ApplyCard(CityStats stats, SimCard card)
    {
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = -card.Cost });
        stats.ApplyModifiers(card.StatEffects);
    }

    // Maior pontuacao vence; empate e sorteado com o gerador da politica, pra nao favorecer a ordem da mao.
    public static T PickBest<T>(IReadOnlyList<T> cards, Func<T, double> score, Random random)
    {
        const double tolerance = 1e-9;
        var best = new List<T>();
        var bestScore = double.NegativeInfinity;
        foreach (var card in cards)
        {
            var value = score(card);
            if (value > bestScore + tolerance)
            {
                bestScore = value;
                best.Clear();
                best.Add(card);
            }
            else if (Math.Abs(value - bestScore) <= tolerance)
            {
                best.Add(card);
            }
        }
        return best[random.Next(best.Count)];
    }

    // Cartas repetidas na mao tem o mesmo efeito, avaliar uma vez so.
    public static IReadOnlyList<SimCard> DistinctById(IReadOnlyList<SimCard> cards)
    {
        return cards.GroupBy(card => card.Id).Select(group => group.First()).ToList();
    }
}
