namespace Metis.Simulator.Policies;

// Sem simular nada: soma os efeitos da carta pesando mais o parametro que esta mais perto do proprio critico.
public sealed class BalancedPolicy : IPlayerPolicy
{
    // Pesquisa nao tem critico, entra como se estivesse sempre a essa distancia dele.
    public const float PesquisaVirtualMargin = 40f;

    public string Name => "equilibrada";

    public SimCard Choose(DecisionContext context)
    {
        var weights = BuildWeights(context.Stats, context.Data);
        var candidates = PolicyHelpers.DistinctById(context.PlayableCards);
        return PolicyHelpers.PickBest(candidates, card =>
        {
            double score = -card.Cost * Weight(weights, CityParameterType.Renda);
            foreach (var effect in card.StatEffects)
                score += effect.Amount * Weight(weights, effect.Parameter);
            return score;
        }, context.Random);
    }

    // Bem-estar fica de fora: e recalculado a partir dos outros na resolucao, efeito direto nele nao dura.
    public static Dictionary<CityParameterType, double> BuildWeights(CityStats stats, BalanceData data)
    {
        var weights = new Dictionary<CityParameterType, double>();
        foreach (var config in data.Parameters)
        {
            if (config.Parameter == CityStats.AnchorParameter)
                continue;

            var margin = config.CriticalLevel < 0f
                ? PesquisaVirtualMargin
                : stats.GetValue(config.Parameter) - config.CriticalLevel;
            weights[config.Parameter] = 1.0 / Math.Max(margin, 1f);
        }
        return weights;
    }

    private static double Weight(Dictionary<CityParameterType, double> weights, CityParameterType parameter)
    {
        return weights.TryGetValue(parameter, out var weight) ? weight : 0.0;
    }
}
