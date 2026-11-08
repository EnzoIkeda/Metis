using System;
using System.Collections.Generic;
using System.Globalization;

public enum ObjectiveComparison
{
    AtLeast,
    AtMost
}

public enum ObjectiveState
{
    Pending,
    Achieved,
    Failed
}

// Meta de um cenario, medida no fim de cada turno. O limite fica no indice do motor.
[Serializable]
public struct ScenarioObjective
{
    public CityParameterType Parameter;
    public ObjectiveComparison Comparison;
    public float Threshold;

    // Sem manter: basta alcancar ate este turno. Mantendo: vale deste turno ate o fim da fase.
    public int ByTurn;
    public bool MustHold;

    public bool IsMetBy(float value)
    {
        return Comparison == ObjectiveComparison.AtLeast ? value >= Threshold : value <= Threshold;
    }
}

// Objetivos so leem o historico da fase: nenhuma regra do turno depende deles.
public static class ScenarioObjectiveEvaluator
{
    // Com a fase em andamento, outcome e None.
    public static ObjectiveState Evaluate(ScenarioObjective objective, IReadOnlyList<PhaseTurnRecord> turns, GameOutcome outcome)
    {
        return objective.MustHold ? EvaluateHold(objective, turns, outcome) : EvaluateReach(objective, turns, outcome);
    }

    public static List<ObjectiveState> EvaluateAll(IReadOnlyList<ScenarioObjective> objectives, IReadOnlyList<PhaseTurnRecord> turns, GameOutcome outcome)
    {
        var states = new List<ObjectiveState>(objectives.Count);
        foreach (var objective in objectives)
            states.Add(Evaluate(objective, turns, outcome));
        return states;
    }

    // Uma estrela por sobreviver e uma por objetivo; quem perde a fase nao pontua.
    public static int Stars(GameOutcome outcome, IReadOnlyList<ObjectiveState> states)
    {
        if (outcome != GameOutcome.Victory)
            return 0;

        var stars = 1;
        foreach (var state in states)
        {
            if (state == ObjectiveState.Achieved)
                stars++;
        }
        return stars;
    }

    public static int MaxStars(int objectiveCount) => 1 + objectiveCount;

    private static ObjectiveState EvaluateReach(ScenarioObjective objective, IReadOnlyList<PhaseTurnRecord> turns, GameOutcome outcome)
    {
        var lastTurn = 0;
        foreach (var turn in turns)
        {
            lastTurn = Math.Max(lastTurn, turn.TurnIndex);
            if (turn.TurnIndex <= objective.ByTurn && objective.IsMetBy(turn.GetEnd(objective.Parameter)))
                return ObjectiveState.Achieved;
        }
        return outcome != GameOutcome.None || lastTurn >= objective.ByTurn ? ObjectiveState.Failed : ObjectiveState.Pending;
    }

    private static ObjectiveState EvaluateHold(ScenarioObjective objective, IReadOnlyList<PhaseTurnRecord> turns, GameOutcome outcome)
    {
        foreach (var turn in turns)
        {
            if (turn.TurnIndex >= objective.ByTurn && objective.IsMetBy(turn.GetEnd(objective.Parameter)) == false)
                return ObjectiveState.Failed;
        }

        if (outcome == GameOutcome.None)
            return ObjectiveState.Pending;
        return outcome == GameOutcome.Victory ? ObjectiveState.Achieved : ObjectiveState.Failed;
    }
}

// Melhor resultado de cada cenario, num texto simples para o armazenamento.
public class ScenarioProgressState
{
    private readonly Dictionary<string, int> _bestStars = new Dictionary<string, int>(StringComparer.Ordinal);

    public int BestStars(string scenarioId)
    {
        return _bestStars.TryGetValue(scenarioId, out var stars) ? stars : 0;
    }

    // Devolve verdadeiro quando o resultado supera o melhor anterior.
    public bool Record(string scenarioId, int stars)
    {
        if (stars <= BestStars(scenarioId))
            return false;
        _bestStars[scenarioId] = stars;
        return true;
    }

    public string Serialize()
    {
        var ids = new List<string>(_bestStars.Keys);
        ids.Sort(StringComparer.Ordinal);
        var parts = new List<string>(ids.Count);
        foreach (var id in ids)
            parts.Add(id + "=" + _bestStars[id].ToString(CultureInfo.InvariantCulture));
        return string.Join(";", parts);
    }

    public static ScenarioProgressState Parse(string text)
    {
        var state = new ScenarioProgressState();
        if (string.IsNullOrEmpty(text))
            return state;

        foreach (var part in text.Split(';'))
        {
            var pieces = part.Split('=');
            if (pieces.Length == 2 && pieces[0].Length > 0
                && int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stars) && stars > 0)
                state._bestStars[pieces[0]] = stars;
        }
        return state;
    }
}
