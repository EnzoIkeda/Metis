using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class RandomEventPoolTests
{
    [Test]
    public void TryTriggerEvent_SameSeed_ProducesSameSequence()
    {
        var pool = Enumerable.Range(0, 10).Select(i => new FakeEvent { Id = $"E{i}" }).ToList();
        var first = new RandomEventPool<FakeEvent>(pool, new Random(7));
        var second = new RandomEventPool<FakeEvent>(pool, new Random(7));
        var stats = CityStatsTestFactory.Build();

        for (int turn = 1; turn <= 20; turn++)
            Assert.That(second.TryTriggerEvent(stats, turn).Id, Is.EqualTo(first.TryTriggerEvent(stats, turn).Id));
    }

    [Test]
    public void TryTriggerEvent_OnlyPicksEventsInsideTurnWindow()
    {
        var early = new FakeEvent { Id = "Early", MaxTurn = 5 };
        var late = new FakeEvent { Id = "Late", MinTurn = 10 };
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { early, late }, new Random(1));
        var stats = CityStatsTestFactory.Build();

        Assert.That(pool.TryTriggerEvent(stats, 3), Is.SameAs(early));
        Assert.That(pool.TryTriggerEvent(stats, 12), Is.SameAs(late));
        Assert.That(pool.TryTriggerEvent(stats, 7), Is.Null);
    }

    [Test]
    public void TryTriggerEvent_AppliesEffectsOfChosenEvent()
    {
        var evt = new FakeEvent
        {
            StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Energia, Amount = -8f } },
        };
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { evt });
        var stats = CityStatsTestFactory.Build();

        pool.TryTriggerEvent(stats, 1);

        Assert.That(stats.GetValue(CityParameterType.Energia), Is.EqualTo(CityStatsTestFactory.NeutralValue - 8f));
    }

    [Test]
    public void IsEligible_FailsWhenAnyTriggerConditionFails()
    {
        var evt = new FakeEvent
        {
            TriggerConditions = new[]
            {
                new TriggerCondition { Parameter = CityParameterType.Pesquisa, Comparison = ComparisonType.GreaterThanOrEqual, Threshold = 40f },
                new TriggerCondition { Parameter = CityParameterType.Seguranca, Comparison = ComparisonType.LessThanOrEqual, Threshold = 30f },
            },
        };
        var pool = new RandomEventPool<FakeEvent>(new List<FakeEvent> { evt });

        Assert.That(pool.IsEligible(evt, CityStatsTestFactory.Build(), 1), Is.False);
    }
}
