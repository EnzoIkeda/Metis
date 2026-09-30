namespace Metis.Simulator.Policies;

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

    public static double Evaluate(CityStats stats, BalanceData data)
    {
        if (stats.IsAnchorCritical())
            return GameOverScore;

        double score = 0.0;
        foreach (var config in data.Parameters)
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

        var thresholds = data.TierThresholds;
        var pesquisaCap = thresholds.Count > 0 ? thresholds[thresholds.Count - 1] : 0f;
        score += PesquisaWeight * Math.Min(stats.GetValue(CityParameterType.Pesquisa), pesquisaCap);
        return score;
    }
}
