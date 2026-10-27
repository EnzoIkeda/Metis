using System;
using System.Collections.Generic;

// O que a nota do estado precisa saber da configuracao: o nivel critico de cada parametro e o ultimo limiar de Pesquisa.
public sealed class ScoreRules
{
    public ScoreRules(IReadOnlyList<CityParameterConfig> parameters, float pesquisaCap)
    {
        Parameters = parameters;
        PesquisaCap = pesquisaCap;
    }

    public IReadOnlyList<CityParameterConfig> Parameters { get; }

    // Pesquisa acima disso nao libera mais nada.
    public float PesquisaCap { get; }

    // O limiar mais alto entre as cartas, zero quando nenhuma pede Pesquisa.
    public static ScoreRules FromCards<TCard>(IReadOnlyList<CityParameterConfig> parameters, IEnumerable<TCard> cards)
        where TCard : ICardDefinition
    {
        var cap = 0f;
        foreach (var card in cards)
        {
            if (card != null && card.RequiredPesquisa > cap)
                cap = card.RequiredPesquisa;
        }
        return new ScoreRules(parameters, cap);
    }
}

// Nota de um estado da cidade, usada pra comparar jogadas: quanto maior, mais longe do fim de jogo.
public static class StateScore
{
    // Margem acima do critico que deixa de valer mais, pra nao premiar inflar um parametro ja seguro.
    public const double MarginCap = 30.0;

    // Bem-estar e o unico que termina o jogo, pesa mais que os outros.
    public const double AnchorWeight = 3.0;

    // Desconto por parametro em colapso, alem da margem negativa.
    public const double CollapsePenalty = 15.0;

    // Pesquisa nao tem critico, vale pelo acesso a tiers mais fortes, ate o ultimo limiar.
    public const double PesquisaWeight = 0.3;

    public const double GameOverScore = -1000.0;

    // Nota de uma cidade com todo parametro no teto da margem e Pesquisa no ultimo limiar.
    public static double MaxScore(ScoreRules rules)
    {
        double score = 0.0;
        foreach (var config in rules.Parameters)
        {
            if (config.CriticalLevel < 0f)
                continue;
            score += config.Parameter == CityStats.AnchorParameter ? AnchorWeight * MarginCap : MarginCap;
        }

        score += PesquisaWeight * rules.PesquisaCap;
        return score;
    }

    public static double Evaluate(CityStats stats, ScoreRules rules)
    {
        if (stats.IsAnchorCritical())
            return GameOverScore;

        double score = 0.0;
        foreach (var config in rules.Parameters)
        {
            if (config.CriticalLevel < 0f)
                continue;

            var margin = Math.Min(stats.GetValue(config.Parameter) - config.CriticalLevel, MarginCap);
            if (config.Parameter == CityStats.AnchorParameter)
            {
                score += AnchorWeight * margin;
                continue;
            }

            score += margin;
            if (stats.IsInCollapse(config.Parameter))
                score -= CollapsePenalty;
        }

        score += PesquisaWeight * Math.Min(stats.GetValue(CityParameterType.Pesquisa), rules.PesquisaCap);
        return score;
    }
}

// Olha um turno a frente: joga a carta numa copia do estado, resolve o turno e da a nota. Nao preve o evento.
public static class GreedyLookahead
{
    // Aplica a carta como a mao aplicaria ao jogar: custo em Renda, depois os efeitos.
    public static void ApplyCard(CityStats stats, ICardDefinition card)
    {
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = -card.Cost });
        stats.ApplyModifiers(card.StatEffects);
    }

    // card nulo e passar a vez; paidFirst e a acao livre paga antes da carta comum, no mesmo estado.
    public static double Score(CityStats state, ICardDefinition card, ScoreRules rules, ICardDefinition paidFirst = null)
    {
        var future = state.Clone();
        if (paidFirst != null)
            ApplyCard(future, paidFirst);
        if (card != null)
            ApplyCard(future, card);
        future.ResolveTurn();
        return StateScore.Evaluate(future, rules);
    }
}
