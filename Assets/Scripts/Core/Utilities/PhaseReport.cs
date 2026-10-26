using System;
using System.Collections.Generic;

// Como medir o peso de uma jogada no relatorio.
public enum ImpactCriterion
{
    // Nota do estado depois do turno jogando a carta, menos a nota passando a vez, a partir do mesmo estado.
    VersusPass,

    // Soma dos efeitos diretos da carta, custo incluido, com os pesos da nota do estado.
    DirectEffect,
}

public struct SeriesPoint
{
    public int Turn;
    public float Value;
}

public struct DecisionImpact
{
    public int TurnIndex;
    public string CardId;
    public double Impact;
}

// Tudo que um parametro passou em Colapso na fase.
public struct CollapseSpan
{
    public CityParameterType Parameter;
    public int FirstTurn;

    // Turnos em que ele cobrou a penalidade do Bem-estar, e quanto tirou no total.
    public int TurnsPenalized;
    public float WellbeingPenalty;
}

// Turno em que outra carta da mao deixaria a cidade melhor, pela nota de um turno a frente.
public struct MissedOpportunity
{
    public int TurnIndex;
    public string PlayedCardId;
    public string BetterCardId;
    public double Gain;

    // A jogada levava o Bem-estar ao critico ainda antes do evento, e a alternativa nao.
    public bool AvoidsGameOver;
}

// Reconstroi o estado de antes de cada jogada pra comparar o que foi jogado com o que dava pra jogar.
public sealed class TurnEvaluator
{
    private readonly CityStats _template;
    private readonly Func<string, ICardDefinition> _findCard;

    // template so empresta a configuracao e a matriz; os valores vem do registro do turno.
    public TurnEvaluator(CityStats template, ScoreRules rules, Func<string, ICardDefinition> findCard)
    {
        _template = template;
        Rules = rules;
        _findCard = findCard;
    }

    public ScoreRules Rules { get; }

    public bool CanEvaluate(PhaseTurnRecord turn)
    {
        return turn != null && turn.HasPlayState && turn.ValuesBeforePlay.Length == PhaseHistory.Parameters.Length;
    }

    public CityStats StateBeforePlay(PhaseTurnRecord turn)
    {
        var state = _template.Clone();
        var values = new Dictionary<CityParameterType, float>();
        foreach (var parameter in PhaseHistory.Parameters)
            values[parameter] = turn.GetBeforePlay(parameter);
        state.RestoreState(values, turn.AnchorBonusBeforePlay);
        return state;
    }

    // NaN quando a carta nao existe mais no jogo.
    public double ScoreCard(PhaseTurnRecord turn, string cardId)
    {
        var card = _findCard != null ? _findCard(cardId) : null;
        if (card == null)
            return double.NaN;
        return GreedyLookahead.Score(StateBeforePlay(turn), card, Rules);
    }

    public double ScorePass(PhaseTurnRecord turn)
    {
        return GreedyLookahead.Score(StateBeforePlay(turn), null, Rules);
    }
}

// Leituras do historico pro relatorio de fim de fase, sem mudar nenhum numero do jogo.
public static class PhaseReport
{
    // Ganho minimo na nota pra apontar outra carta como melhor, abaixo disso a diferenca e ruido.
    public const double MinimumMissGain = 3.0;

    // Valor inicial no turno 0 e o fim de cada turno.
    public static List<SeriesPoint> Series(PhaseHistory history, CityParameterType parameter)
    {
        var points = new List<SeriesPoint>();
        if (history.StartValues.Length == PhaseHistory.Parameters.Length)
            points.Add(new SeriesPoint { Turn = 0, Value = PhaseHistory.Read(history.StartValues, parameter) });
        foreach (var turn in history.Turns)
        {
            if (turn.EndValues.Length == PhaseHistory.Parameters.Length)
                points.Add(new SeriesPoint { Turn = turn.TurnIndex, Value = turn.GetEnd(parameter) });
        }
        return points;
    }

    // NaN quando o turno nao tem carta comum ou nao da pra avaliar.
    public static double Impact(PhaseTurnRecord turn, ImpactCriterion criterion, TurnEvaluator evaluator)
    {
        if (turn == null || turn.PlayedCard == false || turn.HasPlayState == false)
            return double.NaN;

        if (criterion == ImpactCriterion.DirectEffect)
            return DirectEffect(turn);

        if (evaluator == null || evaluator.CanEvaluate(turn) == false)
            return double.NaN;
        return evaluator.ScoreCard(turn, turn.CardId) - evaluator.ScorePass(turn);
    }

    private static double DirectEffect(PhaseTurnRecord turn)
    {
        if (turn.HasResolution == false)
            return double.NaN;

        double sum = 0.0;
        foreach (var parameter in PhaseHistory.Parameters)
            sum += Weight(parameter) * turn.GetCardEffect(parameter);
        return sum;
    }

    private static double Weight(CityParameterType parameter)
    {
        if (parameter == CityStats.AnchorParameter)
            return StateScore.AnchorWeight;
        if (parameter == CityParameterType.Pesquisa)
            return StateScore.PesquisaWeight;
        return 1.0;
    }

    // Todas as jogadas avaliaveis, na ordem dos turnos.
    public static List<DecisionImpact> Impacts(PhaseHistory history, ImpactCriterion criterion, TurnEvaluator evaluator)
    {
        var impacts = new List<DecisionImpact>();
        foreach (var turn in history.Turns)
        {
            var impact = Impact(turn, criterion, evaluator);
            if (double.IsNaN(impact) == false)
                impacts.Add(new DecisionImpact { TurnIndex = turn.TurnIndex, CardId = turn.CardId, Impact = impact });
        }
        return impacts;
    }

    // As jogadas de maior peso, pra mais ou pra menos; empate fica com o turno mais cedo.
    public static List<DecisionImpact> TopDecisions(PhaseHistory history, int count, ImpactCriterion criterion, TurnEvaluator evaluator)
    {
        var impacts = Impacts(history, criterion, evaluator);
        impacts.Sort((a, b) =>
        {
            var byMagnitude = Math.Abs(b.Impact).CompareTo(Math.Abs(a.Impact));
            return byMagnitude != 0 ? byMagnitude : a.TurnIndex.CompareTo(b.TurnIndex);
        });
        if (impacts.Count > count)
            impacts.RemoveRange(count, impacts.Count - count);
        return impacts;
    }

    // Cada parametro que entrou em Colapso, na ordem em que entrou.
    public static List<CollapseSpan> CollapseChain(PhaseHistory history, ScoreRules rules)
    {
        var spans = new Dictionary<CityParameterType, CollapseSpan>();
        var order = new List<CityParameterType>();

        void Touch(CityParameterType parameter, int turnIndex)
        {
            if (spans.ContainsKey(parameter))
                return;
            spans[parameter] = new CollapseSpan { Parameter = parameter, FirstTurn = turnIndex };
            order.Add(parameter);
        }

        foreach (var turn in history.Turns)
        {
            if (turn.CollapsedAtResolution.Count > 0)
            {
                var perParameter = turn.CollapsePenalty / turn.CollapsedAtResolution.Count;
                foreach (var parameter in turn.CollapsedAtResolution)
                {
                    Touch(parameter, turn.TurnIndex);
                    var span = spans[parameter];
                    span.TurnsPenalized++;
                    span.WellbeingPenalty += perParameter;
                    spans[parameter] = span;
                }
            }

            // Quem entrou em Colapso no fim do turno so cobra no turno seguinte, mas ja conta a entrada.
            if (turn.EndValues.Length != PhaseHistory.Parameters.Length)
                continue;
            foreach (var config in rules.Parameters)
            {
                if (IsCollapsed(config, turn.GetEnd(config.Parameter)))
                    Touch(config.Parameter, turn.TurnIndex);
            }
        }

        var chain = new List<CollapseSpan>();
        foreach (var parameter in order)
            chain.Add(spans[parameter]);
        return chain;
    }

    // Mesma regra do Colapso: abaixo do critico, menos a ancora e Pesquisa.
    private static bool IsCollapsed(CityParameterConfig config, float value)
    {
        if (config.Parameter == CityStats.AnchorParameter || config.Parameter == CityParameterType.Pesquisa)
            return false;
        return config.CriticalLevel >= 0f && value <= config.CriticalLevel;
    }

    // Melhor carta comum da mao naquele estado, se ela superasse a jogada pela margem minima.
    public static MissedOpportunity? BestAlternative(PhaseTurnRecord turn, TurnEvaluator evaluator, double minimumGain = MinimumMissGain)
    {
        if (turn == null || turn.PlayedCard == false || evaluator == null || evaluator.CanEvaluate(turn) == false)
            return null;

        var played = evaluator.ScoreCard(turn, turn.CardId);
        if (double.IsNaN(played))
            return null;

        string bestId = null;
        var bestScore = double.NegativeInfinity;
        foreach (var id in turn.PlayableCardIds)
        {
            if (id == turn.CardId)
                continue;
            var score = evaluator.ScoreCard(turn, id);
            if (double.IsNaN(score) == false && score > bestScore)
            {
                bestScore = score;
                bestId = id;
            }
        }

        if (bestId == null || bestScore - played < minimumGain)
            return null;
        return new MissedOpportunity
        {
            TurnIndex = turn.TurnIndex,
            PlayedCardId = turn.CardId,
            BetterCardId = bestId,
            Gain = bestScore - played,
            AvoidsGameOver = played <= StateScore.GameOverScore && bestScore > StateScore.GameOverScore,
        };
    }

    // O turno com a maior diferenca entre a jogada e a melhor alternativa, se houver.
    public static MissedOpportunity? BiggestMiss(PhaseHistory history, TurnEvaluator evaluator, double minimumGain = MinimumMissGain)
    {
        MissedOpportunity? biggest = null;
        foreach (var turn in history.Turns)
        {
            var miss = BestAlternative(turn, evaluator, minimumGain);
            if (miss.HasValue && (biggest.HasValue == false || miss.Value.Gain > biggest.Value.Gain))
                biggest = miss;
        }
        return biggest;
    }
}
