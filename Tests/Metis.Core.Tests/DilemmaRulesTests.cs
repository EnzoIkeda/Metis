using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class DilemmaRulesTests
{
    private const CityParameterType Target = CityParameterType.Energia;

    private static StatModifier[] Change(float amount)
    {
        return new[] { new StatModifier { Parameter = Target, Amount = amount } };
    }

    private static FakeEvent Dilemma(string id = "Dilema", string followUp = "", int delay = 0, int maxTurn = 999)
    {
        return new FakeEvent
        {
            Id = id,
            MaxTurn = maxTurn,
            Choices = new[]
            {
                new EventChoiceDefinition { Effects = Change(-5f), FollowUpEventId = followUp, FollowUpDelay = delay },
                new EventChoiceDefinition { Effects = Change(3f) },
            },
        };
    }

    private static FakeEvent FollowUp(string id = "Consequencia", float amount = -7f)
    {
        return new FakeEvent { Id = id, IsFollowUpOnly = true, StatEffects = Change(amount) };
    }

    private static float Value(CityStats stats) => stats.GetValue(Target);

    [Test]
    public void FollowUpOnly_IsNeverDrawn()
    {
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { FollowUp() }, new Random(1));

        Assert.That(pool.TryTriggerEvent(CityStatsTestFactory.Build(), 1), Is.Null);
    }

    [Test]
    public void Dilemma_ChangesNothingUntilTheChoice_ThenAppliesOnlyTheChosenOption()
    {
        var dilemma = Dilemma();
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { dilemma }, new Random(1));
        var stats = CityStatsTestFactory.Build();

        Assert.That(pool.TryTriggerEvent(stats, 1), Is.SameAs(dilemma));
        Assert.That(Value(stats), Is.EqualTo(CityStatsTestFactory.NeutralValue));

        pool.ApplyChoice(stats, dilemma, 1, 1);

        Assert.That(Value(stats), Is.EqualTo(CityStatsTestFactory.NeutralValue + 3f));
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void ApplyChoice_InvalidIndex_Throws(int index)
    {
        var dilemma = Dilemma();
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { dilemma });

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.ApplyChoice(CityStatsTestFactory.Build(), dilemma, index, 1));
    }

    [Test]
    public void FollowUp_FiresOnTheScheduledTurn_BeforeTheDraw()
    {
        var dilemma = Dilemma(followUp: "Consequencia", delay: 2);
        var common = new FakeEvent { Id = "Comum" };
        var followUp = FollowUp();
        var all = new List<FakeEvent> { dilemma, common, followUp };
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { common }, new Random(1), all);
        var stats = CityStatsTestFactory.Build();

        pool.ApplyChoice(stats, dilemma, 0, 3);
        var before = Value(stats);

        Assert.That(pool.TryTriggerEvent(stats, 4), Is.SameAs(common));
        Assert.That(pool.TryTriggerEvent(stats, 5), Is.SameAs(followUp));
        Assert.That(Value(stats), Is.EqualTo(before - 7f));
        Assert.That(pool.TryTriggerEvent(stats, 6), Is.SameAs(common));
    }

    [Test]
    public void ChoiceWithoutFollowUp_SchedulesNothing()
    {
        var dilemma = Dilemma(followUp: "Consequencia", delay: 2);
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { dilemma });

        pool.ApplyChoice(CityStatsTestFactory.Build(), dilemma, 1, 3);

        Assert.That(pool.Schedule.PendingCount, Is.Zero);
    }

    [Test]
    public void FollowUp_IgnoresWindowAndConditions_AndIsFoundOutsideTheFilteredPool()
    {
        var followUp = new FakeEvent
        {
            Id = "Consequencia",
            IsFollowUpOnly = true,
            MinTurn = 50,
            MaxTurn = 60,
            TriggerConditions = new[] { new TriggerCondition { Parameter = Target, Comparison = ComparisonType.GreaterThanOrEqual, Threshold = 1000f } },
        };
        var schedule = new EventSchedule();
        schedule.Add("Consequencia", 4);
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent>(), new Random(1), new List<FakeEvent> { followUp }, schedule);

        Assert.That(pool.TryTriggerEvent(CityStatsTestFactory.Build(), 4), Is.SameAs(followUp));
    }

    [Test]
    public void FollowUp_DoesNotConsumeTheDraw()
    {
        var events = Enumerable.Range(0, 10).Select(i => new FakeEvent { Id = $"E{i}" }).ToList();
        var followUp = FollowUp();
        var all = events.Append(followUp).ToList();
        var plain = new RandomEventPool<FakeEvent>(events, new Random(7), all);
        var schedule = new EventSchedule();
        schedule.Add("Consequencia", 2);
        var forced = new RandomEventPool<FakeEvent>(events, new Random(7), all, schedule);
        var stats = CityStatsTestFactory.Build();

        Assert.That(forced.TryTriggerEvent(stats, 2), Is.SameAs(followUp));
        for (var turn = 3; turn <= 12; turn++)
            Assert.That(forced.TryTriggerEvent(stats, turn).Id, Is.EqualTo(plain.TryTriggerEvent(stats, turn).Id));
    }

    [Test]
    public void Dilemma_ComesOutOncePerPhase()
    {
        var dilemma = Dilemma();
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { dilemma }, new Random(1));
        var stats = CityStatsTestFactory.Build();

        Assert.That(pool.TryTriggerEvent(stats, 1), Is.SameAs(dilemma));
        Assert.That(pool.TryTriggerEvent(stats, 2), Is.Null);
    }

    [Test]
    public void CommonEvent_CanRepeat()
    {
        var common = new FakeEvent { Id = "Comum" };
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { common }, new Random(1));
        var stats = CityStatsTestFactory.Build();

        Assert.That(pool.TryTriggerEvent(stats, 1), Is.SameAs(common));
        Assert.That(pool.TryTriggerEvent(stats, 2), Is.SameAs(common));
    }

    [TestCase(17, true)]
    [TestCase(18, false)]
    public void Dilemma_NeedsRoomForItsLongestFollowUp(int turn, bool eligible)
    {
        var dilemma = Dilemma(followUp: "Consequencia", delay: 3);
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { dilemma }, new Random(1), lastTurn: 20);

        Assert.That(pool.IsEligible(dilemma, CityStatsTestFactory.Build(), turn), Is.EqualTo(eligible));
    }

    [Test]
    public void Dilemma_PendingFollowUpsTakeRoomInThePhase()
    {
        var dilemma = Dilemma(followUp: "Consequencia", delay: 3);
        var schedule = new EventSchedule();
        schedule.Add("Outra", 20);
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { dilemma }, new Random(1), schedule: schedule, lastTurn: 20);

        Assert.That(pool.IsEligible(dilemma, CityStatsTestFactory.Build(), 17), Is.False);
        Assert.That(pool.IsEligible(dilemma, CityStatsTestFactory.Build(), 16), Is.True);
    }

    [Test]
    public void DilemmaWithoutFollowUp_FitsOnTheLastTurn()
    {
        var dilemma = Dilemma();
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { dilemma }, new Random(1), lastTurn: 20);

        Assert.That(pool.IsEligible(dilemma, CityStatsTestFactory.Build(), 20), Is.True);
    }

    [Test]
    public void Schedule_SameTurn_PushesTheSecondToTheNextFreeTurn()
    {
        var schedule = new EventSchedule();

        Assert.That(schedule.Add("A", 5), Is.EqualTo(5));
        Assert.That(schedule.Add("B", 5), Is.EqualTo(6));
        Assert.That(schedule.Add("C", 5), Is.EqualTo(7));
    }

    [Test]
    public void Schedule_TakeDue_RemovesItAndReturnsNullAfterwards()
    {
        var schedule = new EventSchedule();
        schedule.Add("A", 5);

        Assert.That(schedule.TakeDue(4), Is.Null);
        Assert.That(schedule.TakeDue(5), Is.EqualTo("A"));
        Assert.That(schedule.TakeDue(5), Is.Null);
    }

    [Test]
    public void Schedule_Clone_DoesNotTouchTheOriginal()
    {
        var schedule = new EventSchedule();
        schedule.Add("A", 5);
        schedule.MarkSeen("Dilema");

        var clone = schedule.Clone();
        clone.TakeDue(5);
        clone.MarkSeen("Outro");

        Assert.That(schedule.PendingCount, Is.EqualTo(1));
        Assert.That(schedule.WasSeen("Outro"), Is.False);
        Assert.That(clone.WasSeen("Dilema"), Is.True);
    }

    [Test]
    public void Schedule_WithoutFollowUps_KeepsOnlyWhatAlreadyCameOut()
    {
        var schedule = new EventSchedule();
        schedule.Add("A", 5);
        schedule.MarkSeen("Dilema");

        var blind = schedule.WithoutFollowUps();

        Assert.That(blind.PendingCount, Is.Zero);
        Assert.That(blind.WasSeen("Dilema"), Is.True);
        Assert.That(schedule.PendingCount, Is.EqualTo(1));
    }

    [Test]
    public void Choice_DelayBelowOne_StillComesOnALaterTurn()
    {
        Assert.That(new EventChoiceDefinition { FollowUpDelay = 0 }.EffectiveDelay, Is.EqualTo(1));
    }

    [Test]
    public void History_RecordsTheChoice_AndATurnWithoutItHasNoMark()
    {
        var history = new PhaseHistory();
        var stats = CityStatsTestFactory.Build();

        history.RecordEvent(1, "Comum");
        history.FinishTurn(1, stats);
        history.RecordEvent(2, "Dilema");
        history.RecordEventChoice(2, 1);
        history.FinishTurn(2, stats);

        Assert.That(history.Turns[0].HasEventChoice, Is.False);
        Assert.That(history.Turns[1].HasEventChoice, Is.True);
        Assert.That(history.Turns[1].EventChoice, Is.EqualTo(1));
    }

    [Test]
    public void Save_KeepsFollowUps_AndRebuildsSeenFromHistory()
    {
        var history = new PhaseHistory();
        var stats = CityStatsTestFactory.Build();
        history.RecordEvent(1, "DilemaFechado");
        history.FinishTurn(1, stats);
        history.RecordEvent(2, "DilemaAberto");
        var schedule = new EventSchedule();
        schedule.Add("Consequencia", 6);
        var phase = new PhaseSaveData();

        RunSaveRules.CaptureSchedule(phase, schedule);
        var restored = RunSaveRules.RestoreSchedule(phase, history);

        Assert.That(restored.TakeDue(6), Is.EqualTo("Consequencia"));
        Assert.That(restored.WasSeen("DilemaFechado"), Is.True);
        Assert.That(restored.WasSeen("DilemaAberto"), Is.True);
        Assert.That(restored.WasSeen("Nunca"), Is.False);
        Assert.That(restored.WasSeen(""), Is.False);
    }

    private static PhaseHistory HistoryWith(params (int Turn, string Event, int Choice)[] turns)
    {
        var history = new PhaseHistory();
        var stats = CityStatsTestFactory.Build();
        foreach (var turn in turns)
        {
            history.RecordEvent(turn.Turn, turn.Event);
            if (turn.Choice >= 0)
                history.RecordEventChoice(turn.Turn, turn.Choice);
            history.FinishTurn(turn.Turn, stats);
        }
        return history;
    }

    private static Func<string, IRandomEventDefinition> Finder(params FakeEvent[] events)
    {
        return id => events.FirstOrDefault(candidate => candidate.Id == id);
    }

    [Test]
    public void Log_LinksTheChoiceToTheTurnItsFollowUpHappened()
    {
        var history = HistoryWith((2, "Dilema", 0), (3, "Comum", -1), (5, "Consequencia", -1));

        var entry = DilemmaLog.Entries(history, Finder(Dilemma(followUp: "Consequencia", delay: 3))).Single();

        Assert.That(entry.Turn, Is.EqualTo(2));
        Assert.That(entry.ChoiceIndex, Is.EqualTo(0));
        Assert.That(entry.FollowUpEventId, Is.EqualTo("Consequencia"));
        Assert.That(entry.FollowUpTurn, Is.EqualTo(5));
    }

    [Test]
    public void Log_ChoiceWithoutFollowUp_HasNoLink()
    {
        var entry = DilemmaLog.Entries(HistoryWith((2, "Dilema", 1)), Finder(Dilemma(followUp: "Consequencia", delay: 3))).Single();

        Assert.That(entry.HasFollowUp, Is.False);
    }

    [Test]
    public void Log_PhaseEndedBeforeTheFollowUp_StaysOpen()
    {
        var entry = DilemmaLog.Entries(HistoryWith((2, "Dilema", 0)), Finder(Dilemma(followUp: "Consequencia", delay: 3))).Single();

        Assert.That(entry.HasFollowUp, Is.True);
        Assert.That(entry.FollowUpTurn, Is.Zero);
    }

    [Test]
    public void Log_TwoChoicesSameFollowUp_EachClaimsItsOwnOccurrence()
    {
        var first = Dilemma("A", "Consequencia", 1);
        var second = Dilemma("B", "Consequencia", 1);
        var history = HistoryWith((1, "A", 0), (2, "Consequencia", -1), (3, "B", 0), (4, "Consequencia", -1));

        var entries = DilemmaLog.Entries(history, Finder(first, second));

        Assert.That(entries.Select(entry => entry.FollowUpTurn), Is.EqualTo(new[] { 2, 4 }));
    }

    [Test]
    public void Log_IgnoresChoiceOfAnEventThatNoLongerExists()
    {
        Assert.That(DilemmaLog.Entries(HistoryWith((2, "Sumiu", 0)), Finder()), Is.Empty);
    }

    [Test]
    public void Log_OriginOf_FindsTheChoiceWhileTheFollowUpIsOpen()
    {
        var history = HistoryWith((2, "Dilema", 0));
        history.RecordEvent(4, "Consequencia");

        var origin = DilemmaLog.OriginOf(history, "Consequencia", Finder(Dilemma(followUp: "Consequencia", delay: 2)));

        Assert.That(origin.HasValue, Is.True);
        Assert.That(origin.Value.Turn, Is.EqualTo(2));
        Assert.That(origin.Value.FollowUpTurn, Is.EqualTo(4));
    }

    [Test]
    public void Log_OriginOf_UnknownFollowUp_IsEmpty()
    {
        Assert.That(DilemmaLog.OriginOf(new PhaseHistory(), "Nada", Finder()).HasValue, Is.False);
    }

    [Test]
    public void Save_WithoutFollowUps_RestoresEmpty()
    {
        var restored = RunSaveRules.RestoreSchedule(new PhaseSaveData { FollowUps = null }, new PhaseHistory());

        Assert.That(restored.PendingCount, Is.Zero);
    }
}
