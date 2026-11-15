namespace Metis.Simulator.Policies;

// Equilibrada que conhece os objetivos do cenario: a nota da carta ganha um bonus pelo que ela aproxima
// cada objetivo ainda aberto. Sem objetivos, joga igual a equilibrada.
public sealed class ObjectivePolicy : IPlayerPolicy
{
    public const double DefaultWeight = 0.25;

    // Objetivo de manter: comeca a pesar alguns turnos antes do prazo e mira uma folga acima do limite.
    public const int HoldLead = 4;
    public const float HoldMargin = 4f;

    // Abaixo deste tamanho, a falta conta como se fosse dele, para o bonus nao explodir perto do limite.
    public const float MinGap = 5f;

    private readonly double _weight;

    public ObjectivePolicy(double weight = DefaultWeight)
    {
        _weight = weight;
    }

    public string Name => "equilibrada_objetivos";

    public SimCard Choose(DecisionContext context)
    {
        var weights = BalancedPolicy.BuildWeights(context.Stats, context.Data);
        var candidates = PolicyHelpers.DistinctById(context.PlayableCards);
        return PolicyHelpers.PickBest(candidates, card => PolicyHelpers.ScoreWithAbilities(
            card, context, plain => Score(plain, context, weights), ability => BalancedPolicy.CostScore(ability, weights, context.CostOffset)), context.Random);
    }

    public SimCard ChooseSearch(DecisionContext context, IReadOnlyList<SimCard> candidates)
    {
        var weights = BalancedPolicy.BuildWeights(context.Stats, context.Data);
        return PolicyHelpers.PickBest(PolicyHelpers.DistinctById(candidates), card => Score(card, context, weights), context.Random);
    }

    public int ChooseEventOption(DecisionContext context, SimEvent dilemma)
    {
        var weights = BalancedPolicy.BuildWeights(context.Stats, context.Data);
        return PolicyHelpers.PickBest(PolicyHelpers.OptionIndexes(dilemma),
            index => Score(PolicyHelpers.AsCard(dilemma.Choices[index]), context, weights), context.Random);
    }

    private double Score(SimCard card, DecisionContext context, Dictionary<CityParameterType, double> weights)
    {
        return BalancedPolicy.Score(card, weights, context.CostOffset) + _weight * Bonus(card, context);
    }

    // Soma, por objetivo aberto, a parte da falta que a carta cobre (ou a folga que ela gasta), dividida pela falta.
    public static double Bonus(SimCard card, DecisionContext context)
    {
        var bonus = 0.0;
        for (var i = 0; i < context.Objectives.Count; i++)
        {
            var objective = context.Objectives[i];
            var state = context.ObjectiveStates != null && i < context.ObjectiveStates.Count ? context.ObjectiveStates[i] : ObjectiveState.Pending;
            if (state != ObjectiveState.Pending)
                continue;

            var turn = context.TurnIndex;
            if (objective.MustHold ? turn < objective.ByTurn - HoldLead : turn > objective.ByTurn)
                continue;

            var sign = objective.Comparison == ObjectiveComparison.AtLeast ? 1f : -1f;
            var target = objective.MustHold ? objective.Threshold + sign * HoldMargin : objective.Threshold;
            var gap = sign * (target - context.Stats.GetValue(objective.Parameter));
            var change = sign * Change(card, objective.Parameter, context.CostOffset);
            var urgency = objective.MustHold ? 1.0 : 1.0 + (double)turn / Math.Max(1, objective.ByTurn);

            if (gap > 0f)
                bonus += urgency * Math.Min(change, gap) / Math.Max(gap, MinGap);
            else if (change < 0f && -change > -gap)
                bonus += urgency * (change - gap) / MinGap;
        }
        return bonus;
    }

    // Efeito direto da carta no parametro, com o custo quando o parametro e a Renda.
    private static float Change(SimCard card, CityParameterType parameter, int costOffset)
    {
        var change = 0f;
        foreach (var effect in card.StatEffects)
        {
            if (effect.Parameter == parameter)
                change += effect.Amount;
        }
        if (parameter == CityParameterType.Renda)
            change -= TradeOfferRules.EffectiveCost(card.Cost, costOffset);
        return change;
    }
}
