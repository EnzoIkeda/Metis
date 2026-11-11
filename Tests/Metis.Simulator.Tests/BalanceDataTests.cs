using Metis.Simulator;

namespace Metis.Simulator.Tests;

public class BalanceDataTests
{
    private const string MinimalJson = """
    {
      "Rules": { "HandSize": 4, "VictoryTurnCount": 12, "RewardOptionCount": 2 },
      "Parameters": [ { "Parameter": "Renda", "InitialValue": 55, "MinValue": 0, "MaxValue": 100, "CriticalLevel": 10, "Deriva": 1 } ],
      "Interaction": { "ValorNeutro": 50, "EscalaGlobal": 1, "PenalidadeColapso": 12 },
      "Cards": [ { "Id": "CardA", "Tier": "CidadeDigital", "Archetype": "Industria", "Cost": 3, "RequiredPesquisa": 25,
                   "Effects": [ { "Parameter": "Renda", "Amount": 6 } ] } ],
      "Events": [ { "Id": "EventA", "MinTurn": 2, "MaxTurn": 9,
                    "Conditions": [ { "Parameter": "Seguranca", "Comparison": "LessThanOrEqual", "Threshold": 30 } ],
                    "Effects": [ { "Parameter": "Energia", "Amount": -4 } ] } ],
      "Advantages": [ { "Id": "AdvA", "Effects": [ { "Parameter": "Pesquisa", "Amount": 8 } ] } ],
      "Presets": [ { "Id": "PresetA", "Overrides": [ { "Parameter": "Renda", "InitialValue": 25 } ] } ]
    }
    """;

    private const string DilemmaJson = """
    {
      "Rules": { "HandSize": 4, "VictoryTurnCount": 12, "RewardOptionCount": 2 },
      "Events": [
        { "Id": "D", "MinTurn": 1, "MaxTurn": 9, "Choices": [
          { "Label": "Implantar", "Effects": [ { "Parameter": "Renda", "Amount": -2 } ], "FollowUpEventId": "F", "FollowUpDelay": 3 },
          { "Label": "Recusar", "Effects": [] } ] },
        { "Id": "F", "MinTurn": 1, "MaxTurn": 999, "FollowUpOnly": true, "Effects": [ { "Parameter": "Saude", "Amount": -4 } ] }
      ]
    }
    """;

    [Test]
    public void Parse_ReadsChoicesAndFollowUps()
    {
        var data = BalanceData.Parse(DilemmaJson);

        var dilemma = data.Events[0];
        Assert.That(dilemma.Choices.Count, Is.EqualTo(2));
        Assert.That(dilemma.ChoiceLabels[1], Is.EqualTo("Recusar"));
        Assert.That(dilemma.Choices[0].Effects.Single().Amount, Is.EqualTo(-2f));
        Assert.That(dilemma.Choices[0].FollowUpEventId, Is.EqualTo("F"));
        Assert.That(dilemma.Choices[0].FollowUpDelay, Is.EqualTo(3));
        Assert.That(data.Events[1].IsFollowUpOnly, Is.True);
    }

    [Test]
    public void Parse_OldExportWithoutChoices_IsACommonEvent()
    {
        var randomEvent = BalanceData.Parse(MinimalJson).Events.Single();

        Assert.That(randomEvent.Choices, Is.Empty);
        Assert.That(randomEvent.IsFollowUpOnly, Is.False);
    }

    [Test]
    public void Parse_UnknownFollowUp_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => BalanceData.Parse(DilemmaJson.Replace("\"F\", \"FollowUpDelay\"", "\"Sumiu\", \"FollowUpDelay\"")));
    }

    [Test]
    public void Parse_MapsEveryFieldIncludingEnumsByName()
    {
        var data = BalanceData.Parse(MinimalJson);

        Assert.That(data.Rules.HandSize, Is.EqualTo(4));
        Assert.That(data.Rules.VictoryTurnCount, Is.EqualTo(12));
        Assert.That(data.Interaction.PenalidadeColapso, Is.EqualTo(12f));
        Assert.That(data.GetParameterConfig(CityParameterType.Renda).Deriva, Is.EqualTo(1f));

        var card = data.Cards.Single();
        Assert.That(card.Tier, Is.EqualTo(CardTier.CidadeDigital));
        Assert.That(card.Archetype, Is.EqualTo(CardArchetype.Industria));
        Assert.That(card.Cost, Is.EqualTo(3f));
        Assert.That(card.StatEffects.Single().Amount, Is.EqualTo(6f));

        var randomEvent = data.Events.Single();
        Assert.That(randomEvent.TriggerConditions.Single().Comparison, Is.EqualTo(ComparisonType.LessThanOrEqual));
        Assert.That(randomEvent.MinTurn, Is.EqualTo(2));

        Assert.That(data.Advantages.Single().StatEffects.Single().Parameter, Is.EqualTo(CityParameterType.Pesquisa));
        Assert.That(data.Presets.Single().Overrides[CityParameterType.Renda], Is.EqualTo(25f));
    }

    [Test]
    public void Parse_UnknownEnumName_Throws()
    {
        var json = MinimalJson.Replace("\"CidadeDigital\"", "\"TierInexistente\"");

        Assert.Throws<InvalidDataException>(() => BalanceData.Parse(json));
    }

    [Test]
    public void BuildParameterConfigs_PresetOverridesOnlyItsParameters()
    {
        var data = BalanceData.Parse(MinimalJson);

        Assert.That(data.BuildParameterConfigs(null).Single().InitialValue, Is.EqualTo(55f));
        Assert.That(data.BuildParameterConfigs(data.Presets.Single()).Single().InitialValue, Is.EqualTo(25f));
        Assert.That(data.GetParameterConfig(CityParameterType.Renda).InitialValue, Is.EqualTo(55f), "nao deveria mexer na configuracao original");
    }

    [Test]
    public void TierThresholds_AreDistinctPositiveRequirementsInOrder()
    {
        var data = TestData.Build(cards: new[]
        {
            new SimCard { Id = "A", RequiredPesquisa = 50f },
            new SimCard { Id = "B", RequiredPesquisa = 0f },
            new SimCard { Id = "C", RequiredPesquisa = 25f },
            new SimCard { Id = "D", RequiredPesquisa = 50f },
        });

        Assert.That(data.TierThresholds, Is.EqualTo(new[] { 25f, 50f }));
    }

    [Test]
    public void BuildParameterConfigs_PresetDerivaMultiplier_ScalesOnlyNegativeDrifts()
    {
        var json = MinimalJson
            .Replace("\"Deriva\": 1 }", "\"Deriva\": 1 }, { \"Parameter\": \"Energia\", \"InitialValue\": 50, \"MinValue\": 0, \"MaxValue\": 100, \"CriticalLevel\": 10, \"Deriva\": -2 }")
            .Replace("\"Overrides\": [ { \"Parameter\": \"Renda\", \"InitialValue\": 25 } ]", "\"Overrides\": [ { \"Parameter\": \"Renda\", \"InitialValue\": 25 } ], \"DerivaMultiplier\": 1.5");
        var data = BalanceData.Parse(json);

        var configs = data.BuildParameterConfigs(data.Presets.Single());

        Assert.That(configs.Single(config => config.Parameter == CityParameterType.Energia).Deriva, Is.EqualTo(-3f));
        Assert.That(configs.Single(config => config.Parameter == CityParameterType.Renda).Deriva, Is.EqualTo(1f), "deriva positiva nao muda");
        Assert.That(data.BuildParameterConfigs(null).Single(config => config.Parameter == CityParameterType.Energia).Deriva, Is.EqualTo(-2f), "fase 1 sem preset nao muda");
    }
}
