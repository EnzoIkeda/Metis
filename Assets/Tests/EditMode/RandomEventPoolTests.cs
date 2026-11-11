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
    public void Asset_WithoutChoices_IsACommonEvent()
    {
        var evt = Event("Comum");

        Assert.That(evt.Choices, Is.Empty);
        Assert.That(evt.IsFollowUpOnly, Is.False);
    }

    [Test]
    public void Asset_ExposesItsChoices_AndThePoolAppliesTheChosenOne()
    {
        var evt = Event("Dilema");
        var effects = new[] { new StatModifier { Parameter = CityParameterType.Renda, Amount = -4f } };
        TestDataFactory.SetField(evt, "_choices", new[]
        {
            new EventChoiceData(),
            new EventChoiceData { Rule = new EventChoiceDefinition { Effects = effects, FollowUpEventId = "Depois", FollowUpDelay = 2 } },
        });
        var pool = new RandomEventPool<RandomEventData>(new List<RandomEventData> { evt });
        var stats = EditModeCityStatsFactory.Build();
        var before = stats.GetValue(CityParameterType.Renda);

        Assert.That(evt.Choices.Count, Is.EqualTo(2));
        pool.ApplyChoice(stats, evt, 1, 5);

        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(before - 4f));
        Assert.That(pool.Schedule.FollowUps[0].EventId, Is.EqualTo("Depois"));
        Assert.That(pool.Schedule.FollowUps[0].Turn, Is.EqualTo(7));
    }

    [Test]
    public void Export_CarriesOptionsAndFollowUps_AndCommonEventsStayEmpty()
    {
        var dilemma = Event("Dilema");
        var effects = new[] { new StatModifier { Parameter = CityParameterType.Renda, Amount = -4f } };
        TestDataFactory.SetField(dilemma, "_choices", new[]
        {
            new EventChoiceData { Label = "Implantar", Rule = new EventChoiceDefinition { Effects = effects, FollowUpEventId = "Depois", FollowUpDelay = 2 } },
        });
        var followUp = Event("Depois");
        TestDataFactory.SetField(followUp, "_isFollowUpOnly", true);

        var dto = BalanceDataExporter.ToEventDto(dilemma);

        Assert.That(dto.Choices[0].Label, Is.EqualTo("Implantar"));
        Assert.That(dto.Choices[0].Effects[0].Amount, Is.EqualTo(-4f));
        Assert.That(dto.Choices[0].FollowUpEventId, Is.EqualTo("Depois"));
        Assert.That(dto.Choices[0].FollowUpDelay, Is.EqualTo(2));
        Assert.That(BalanceDataExporter.ToEventDto(followUp).FollowUpOnly, Is.True);
        Assert.That(BalanceDataExporter.ToEventDto(Event("Comum")).Choices, Is.Empty);
    }

    [Test]
    public void Importer_LeavesDilemmaContentToItsOwnSource()
    {
        var dilemma = Event("Dilema");
        TestDataFactory.SetField(dilemma, "_choices", new[] { new EventChoiceData() });
        var followUp = Event("Depois");
        TestDataFactory.SetField(followUp, "_isFollowUpOnly", true);

        Assert.That(BalanceDataImporter.IsDilemmaContent(dilemma), Is.True);
        Assert.That(BalanceDataImporter.IsDilemmaContent(followUp), Is.True);
        Assert.That(BalanceDataImporter.IsDilemmaContent(Event("Comum")), Is.False);
    }

    [Test]
    public void IsEligible_TurnWindow_IsInclusiveOnBothEnds()
    {
        var evt = Event("Janela", minTurn: 5, maxTurn: 10);
        var pool = new RandomEventPool<RandomEventData>(new List<RandomEventData> { evt });
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
        var pool = new RandomEventPool<RandomEventData>(new List<RandomEventData> { evt });

        Assert.That(pool.IsEligible(evt, EditModeCityStatsFactory.Build(), 1), Is.True);
    }

    [Test]
    public void IsEligible_GreaterThanOrEqual_ChecksThresholdCorrectly()
    {
        var condition = new TriggerCondition { Parameter = CityParameterType.Pesquisa, Comparison = ComparisonType.GreaterThanOrEqual, Threshold = 50f };
        var evt = Event("Gate", triggerConditions: new[] { condition });
        var pool = new RandomEventPool<RandomEventData>(new List<RandomEventData> { evt });

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
        var pool = new RandomEventPool<RandomEventData>(new List<RandomEventData> { evt });

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
        var pool = new RandomEventPool<RandomEventData>(new List<RandomEventData> { evt });

        // Pesquisa neutro (50) passa a primeira, mas Seguranca neutro (50) falha a segunda.
        Assert.That(pool.IsEligible(evt, EditModeCityStatsFactory.Build(), 1), Is.False);

        var ambasPassam = EditModeCityStatsFactory.Build(c => SetInitial(c, CityParameterType.Seguranca, 5f));
        Assert.That(pool.IsEligible(evt, ambasPassam, 1), Is.True);
    }

    [Test]
    public void TryTriggerEvent_NoneEligible_ReturnsNull()
    {
        var foraDaJanela = Event("ForaDaJanela", minTurn: 100, maxTurn: 200);
        var pool = new RandomEventPool<RandomEventData>(new List<RandomEventData> { foraDaJanela });

        var chosen = pool.TryTriggerEvent(EditModeCityStatsFactory.Build(), 1);

        Assert.That(chosen, Is.Null);
    }

    [Test]
    public void TryTriggerEvent_SingleEligible_AppliesItsStatEffectsAndReturnsIt()
    {
        var statEffects = new[] { new StatModifier { Parameter = CityParameterType.Sustentabilidade, Amount = -8f } };
        var evt = Event("Unico", statEffects: statEffects);
        var pool = new RandomEventPool<RandomEventData>(new List<RandomEventData> { evt });
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
