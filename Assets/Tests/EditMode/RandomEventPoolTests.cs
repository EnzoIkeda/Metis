using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RandomEventPoolTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private RandomEventData Event(string name, int minTurn = 1, int maxTurn = 999, TriggerCondition[] triggerConditions = null, StatModifier[] statEffects = null)
    {
        var eventData = TestDataFactory.CreateEvent(name: name, minTurn: minTurn, maxTurn: maxTurn, triggerConditions: triggerConditions, statEffects: statEffects);
        _created.Add(eventData);
        return eventData;
    }

    [Test]
    public void IsEligible_TurnWindow_IsInclusiveOnBothEnds()
    {
        var evt = Event("Janela", minTurn: 5, maxTurn: 10);
        var pool = new RandomEventPool(new List<RandomEventData> { evt });
        var stats = EditModeCityStatsFactory.Build();

        Assert.That(pool.IsEligible(evt, stats, 4), Is.False);
        Assert.That(pool.IsEligible(evt, stats, 5), Is.True);
        Assert.That(pool.IsEligible(evt, stats, 10), Is.True);
        Assert.That(pool.IsEligible(evt, stats, 11), Is.False);
    }

    [Test]
    public void IsEligible_NullTriggerConditions_OnlyDependsOnTurnWindow()
    {
        var evt = Event("SemCondicao", triggerConditions: null);
        var pool = new RandomEventPool(new List<RandomEventData> { evt });

        Assert.That(pool.IsEligible(evt, EditModeCityStatsFactory.Build(), 1), Is.True);
    }

    [Test]
    public void IsEligible_GreaterThanOrEqual_ChecksThresholdCorrectly()
    {
        var condition = new TriggerCondition { Parameter = CityParameterType.Pesquisa, Comparison = ComparisonType.GreaterThanOrEqual, Threshold = 50f };
        var evt = Event("Gate", triggerConditions: new[] { condition });
        var pool = new RandomEventPool(new List<RandomEventData> { evt });

        var abaixo = EditModeCityStatsFactory.Build(c => SetInitial(c, CityParameterType.Pesquisa, 49f));
        var noLimite = EditModeCityStatsFactory.Build(c => SetInitial(c, CityParameterType.Pesquisa, 50f));

        Assert.That(pool.IsEligible(evt, abaixo, 1), Is.False);
        Assert.That(pool.IsEligible(evt, noLimite, 1), Is.True);
    }

    [Test]
    public void IsEligible_LessThanOrEqual_ChecksThresholdCorrectly()
    {
        var condition = new TriggerCondition { Parameter = CityParameterType.Seguranca, Comparison = ComparisonType.LessThanOrEqual, Threshold = 30f };
        var evt = Event("Gate", triggerConditions: new[] { condition });
        var pool = new RandomEventPool(new List<RandomEventData> { evt });

        var acima = EditModeCityStatsFactory.Build(c => SetInitial(c, CityParameterType.Seguranca, 31f));
        var noLimite = EditModeCityStatsFactory.Build(c => SetInitial(c, CityParameterType.Seguranca, 30f));

        Assert.That(pool.IsEligible(evt, acima, 1), Is.False);
        Assert.That(pool.IsEligible(evt, noLimite, 1), Is.True);
    }

    [Test]
    public void IsEligible_MultipleConditions_RequiresAllToPass()
    {
        var conditions = new[]
        {
            new TriggerCondition { Parameter = CityParameterType.Pesquisa, Comparison = ComparisonType.GreaterThanOrEqual, Threshold = 50f },
            new TriggerCondition { Parameter = CityParameterType.Seguranca, Comparison = ComparisonType.LessThanOrEqual, Threshold = 10f },
        };
        var evt = Event("DuasCondicoes", triggerConditions: conditions);
        var pool = new RandomEventPool(new List<RandomEventData> { evt });

        // Pesquisa neutro (50) passa a primeira, mas Seguranca neutro (50) falha a segunda.
        Assert.That(pool.IsEligible(evt, EditModeCityStatsFactory.Build(), 1), Is.False);

        var ambasPassam = EditModeCityStatsFactory.Build(c => SetInitial(c, CityParameterType.Seguranca, 5f));
        Assert.That(pool.IsEligible(evt, ambasPassam, 1), Is.True);
    }

    [Test]
    public void TryTriggerEvent_NoneEligible_ReturnsNull()
    {
        var foraDaJanela = Event("ForaDaJanela", minTurn: 100, maxTurn: 200);
        var pool = new RandomEventPool(new List<RandomEventData> { foraDaJanela });

        var chosen = pool.TryTriggerEvent(EditModeCityStatsFactory.Build(), 1);

        Assert.That(chosen, Is.Null);
    }

    [Test]
    public void TryTriggerEvent_SingleEligible_AppliesItsStatEffectsAndReturnsIt()
    {
        var statEffects = new[] { new StatModifier { Parameter = CityParameterType.Sustentabilidade, Amount = -8f } };
        var evt = Event("Unico", statEffects: statEffects);
        var pool = new RandomEventPool(new List<RandomEventData> { evt });
        var stats = EditModeCityStatsFactory.Build();

        var chosen = pool.TryTriggerEvent(stats, 1);

        Assert.That(chosen, Is.EqualTo(evt));
        Assert.That(stats.GetValue(CityParameterType.Sustentabilidade), Is.EqualTo(42f).Within(0.001f));
    }

    private static void SetInitial(Dictionary<CityParameterType, CityParameterConfig> configs, CityParameterType parameter, float value)
    {
        var cfg = configs[parameter];
        cfg.InitialValue = value;
        configs[parameter] = cfg;
    }
}
