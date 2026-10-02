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

    // Mao revelada neste turno pela carta de revelacao.
    public bool IsRevealed { get; init; }

    // O que a carta de busca traria agora, vazio se nao houver busca jogavel na mao.
    public IReadOnlyList<SimCard> SearchCandidates { get; init; } = Array.Empty<SimCard>();
}

// Jogador simulado: escolhe uma das cartas jogaveis da mao.
public interface IPlayerPolicy
{
    string Name { get; }

    SimCard Choose(DecisionContext context);

    // Escolha da carta que a busca traz pra mao.
    SimCard ChooseSearch(DecisionContext context, IReadOnlyList<SimCard> candidates);
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
            // A primeira sempre entra, pra uma mao em que toda carta vale -infinito ainda ter escolha.
            var value = score(card);
            if (best.Count == 0 || value > bestScore + tolerance)
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

    public static IReadOnlyList<SimCard> PlainCards(IReadOnlyList<SimCard> cards)
    {
        return cards.Where(card => CardRules.IsFreeAction(card) == false).ToList();
    }

    // Avaliacao de jogada pra politicas que pontuam carta a carta: busca vale a melhor carta que ela traz, revelacao vale a melhor
    // carta comum da mao (pra quem ja sabe os numeros a revelacao nao acrescenta nada), ambas descontado o custo.
    public static double ScoreWithAbilities(SimCard card, DecisionContext context, Func<SimCard, double> plainScore, Func<SimCard, double> costScore)
    {
        if (card.Ability == CardAbility.SearchDeck)
            return context.SearchCandidates.Count == 0 ? double.NegativeInfinity : context.SearchCandidates.Max(plainScore) + costScore(card);
        if (card.Ability == CardAbility.RevealHand)
        {
            var plain = PlainCards(context.PlayableCards);
            return plain.Count == 0 ? double.NegativeInfinity : plain.Max(plainScore) + costScore(card);
        }
        return plainScore(card);
    }

    // Cartas repetidas na mao tem o mesmo efeito, avaliar uma vez so.
    public static IReadOnlyList<SimCard> DistinctById(IReadOnlyList<SimCard> cards)
    {
        return cards.GroupBy(card => card.Id).Select(group => group.First()).ToList();
    }
}
