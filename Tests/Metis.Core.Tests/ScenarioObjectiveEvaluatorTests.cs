using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class ScenarioObjectiveEvaluatorTests
{
    private static PhaseTurnRecord Turn(int index, float value)
    {
        var values = new float[PhaseHistory.Parameters.Length];
        values[(int)CityParameterType.Saude] = value;
        return new PhaseTurnRecord { TurnIndex = index, EndValues = values };
    }

    private static List<PhaseTurnRecord> Turns(params float[] values)
    {
        var turns = new List<PhaseTurnRecord>();
        for (var i = 0; i < values.Length; i++)
            turns.Add(Turn(i + 1, values[i]));
        return turns;
    }

    private static ScenarioObjective Reach(float threshold, int byTurn, ObjectiveComparison comparison = ObjectiveComparison.AtLeast)
    {
        return new ScenarioObjective { Parameter = CityParameterType.Saude, Comparison = comparison, Threshold = threshold, ByTurn = byTurn };
    }

    private static ScenarioObjective Hold(float threshold, int fromTurn)
    {
        return new ScenarioObjective { Parameter = CityParameterType.Saude, Comparison = ObjectiveComparison.AtLeast, Threshold = threshold, ByTurn = fromTurn, MustHold = true };
    }

    [Test]
    public void Reach_StaysPendingBeforeTheDeadline()
    {
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(Reach(50f, 3), Turns(10f, 20f), GameOutcome.None), Is.EqualTo(ObjectiveState.Pending));
    }

    [Test]
    public void Reach_AchievedOnTheDeadlineTurn()
    {
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(Reach(50f, 3), Turns(10f, 20f, 50f), GameOutcome.None), Is.EqualTo(ObjectiveState.Achieved));
    }

    [Test]
    public void Reach_FailedWhenOnlyMetAfterTheDeadline()
    {
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(Reach(50f, 2), Turns(10f, 20f, 60f), GameOutcome.None), Is.EqualTo(ObjectiveState.Failed));
    }

    [Test]
    public void Reach_StaysAchievedIfTheValueFallsLater()
    {
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(Reach(50f, 2), Turns(60f, 10f, 5f), GameOutcome.Victory), Is.EqualTo(ObjectiveState.Achieved));
    }

    [Test]
    public void Reach_FailedWhenThePhaseEndsBeforeTheDeadline()
    {
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(Reach(50f, 10), Turns(10f, 20f), GameOutcome.GameOver), Is.EqualTo(ObjectiveState.Failed));
    }

    [Test]
    public void Reach_AtMostComparesDownwards()
    {
        var objective = Reach(30f, 2, ObjectiveComparison.AtMost);
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(objective, Turns(40f, 30f), GameOutcome.None), Is.EqualTo(ObjectiveState.Achieved));
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(objective, Turns(40f, 31f), GameOutcome.None), Is.EqualTo(ObjectiveState.Failed));
    }

    [Test]
    public void Hold_IgnoresTurnsBeforeTheStart()
    {
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(Hold(50f, 3), Turns(1f, 2f, 50f, 60f), GameOutcome.None), Is.EqualTo(ObjectiveState.Pending));
    }

    [Test]
    public void Hold_FailsOnTheFirstTurnBelowAfterTheStart()
    {
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(Hold(50f, 2), Turns(1f, 60f, 49f, 90f), GameOutcome.None), Is.EqualTo(ObjectiveState.Failed));
    }

    [Test]
    public void Hold_AchievedOnlyWhenThePhaseIsWon()
    {
        var turns = Turns(60f, 60f);
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(Hold(50f, 1), turns, GameOutcome.Victory), Is.EqualTo(ObjectiveState.Achieved));
        Assert.That(ScenarioObjectiveEvaluator.Evaluate(Hold(50f, 1), turns, GameOutcome.GameOver), Is.EqualTo(ObjectiveState.Failed));
    }

    [Test]
    public void Stars_OnePerSurvivalPlusOnePerObjective()
    {
        var states = new[] { ObjectiveState.Achieved, ObjectiveState.Failed, ObjectiveState.Achieved };
        Assert.That(ScenarioObjectiveEvaluator.Stars(GameOutcome.Victory, states), Is.EqualTo(3));
        Assert.That(ScenarioObjectiveEvaluator.MaxStars(states.Length), Is.EqualTo(4));
    }

    [Test]
    public void Stars_NoneOnDefeatEvenWithObjectivesAchieved()
    {
        Assert.That(ScenarioObjectiveEvaluator.Stars(GameOutcome.GameOver, new[] { ObjectiveState.Achieved }), Is.EqualTo(0));
    }

    [Test]
    public void ProgressState_KeepsTheBestResult()
    {
        var state = new ScenarioProgressState();
        Assert.That(state.Record("A", 2), Is.True);
        Assert.That(state.Record("A", 1), Is.False);
        Assert.That(state.BestStars("A"), Is.EqualTo(2));
        Assert.That(state.BestStars("B"), Is.EqualTo(0));
    }

    [Test]
    public void ProgressState_RoundTripsAndIgnoresGarbage()
    {
        var state = new ScenarioProgressState();
        state.Record("B", 3);
        state.Record("A", 1);

        Assert.That(state.Serialize(), Is.EqualTo("A=1;B=3"));
        var parsed = ScenarioProgressState.Parse("A=1;B=3;lixo;C=x;=2");
        Assert.That(parsed.BestStars("A"), Is.EqualTo(1));
        Assert.That(parsed.BestStars("B"), Is.EqualTo(3));
        Assert.That(parsed.BestStars("C"), Is.EqualTo(0));
    }

    [Test]
    public void SessionMode_SeparatesTheSaveFiles()
    {
        SessionMode.EnterRoguelike();
        Assert.That(SessionMode.IsScenario, Is.False);
        Assert.That(SessionMode.SaveFileName, Is.EqualTo(SessionMode.RunFileName));

        SessionMode.EnterScenario("Vale", "Tema");
        Assert.That(SessionMode.IsScenario, Is.True);
        Assert.That(SessionMode.SaveFileName, Is.EqualTo("scenario_save_Vale.json"));
        Assert.That(SessionMode.ThemeName, Is.EqualTo("Tema"));

        SessionMode.EnterRoguelike();
        Assert.That(SessionMode.ThemeName, Is.Empty);
    }
}
