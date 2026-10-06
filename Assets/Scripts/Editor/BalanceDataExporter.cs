using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exporta pra JSON tudo que o simulador de balanceamento precisa, lido da cena da fase pra usar exatamente os dados do jogo.
public static class BalanceDataExporter
{
    private const string OutputRelativePath = "Resources/Balance/sim/balance_data.json";

    [MenuItem("Tools/Metis/Balanceamento/Exportar Dados do Simulador")]
    public static void Export()
    {
        var data = BalanceDataFormat.WithCityScene(scene =>
        {
            if (scene.isDirty)
                Debug.LogWarning("[BalanceDataExporter] City_Scene tem mudancas nao salvas, exportando os valores atuais em memoria.");
            return BuildData(scene);
        });

        var outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputRelativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, JsonUtility.ToJson(data, true));
        Debug.Log($"[BalanceDataExporter] {data.Cards.Count} cartas, {data.Events.Count} eventos, {data.Advantages.Count} vantagens e {data.Presets.Count} presets exportados pra '{outputPath}'.");
    }

    private static BalanceDataFormat.BalanceDataDto BuildData(Scene scene)
    {
        var turnManager = BalanceDataFormat.FindInScene<TurnManager>(scene);
        var statsManager = BalanceDataFormat.FindInScene<CityStatsManager>(scene);
        var rewardPopup = BalanceDataFormat.FindInScene<PhaseRewardPopupView>(scene);

        var turn = new SerializedObject(turnManager);
        var stats = new SerializedObject(statsManager);
        var reward = new SerializedObject(rewardPopup);

        var data = new BalanceDataFormat.BalanceDataDto { ExportedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") };
        data.Rules.HandSize = turn.FindProperty("_handSize").intValue;
        data.Rules.VictoryTurnCount = turn.FindProperty("_victoryTurnCount").intValue;
        data.Rules.RewardOptionCount = Math.Min(
            reward.FindProperty("_optionCount").intValue,
            reward.FindProperty("_optionButtons").arraySize);

        var parameters = stats.FindProperty("_initialParameters");
        for (int i = 0; i < parameters.arraySize; i++)
        {
            var entry = parameters.GetArrayElementAtIndex(i);
            data.Parameters.Add(new BalanceDataFormat.ParameterDto
            {
                Parameter = ((CityParameterType)entry.FindPropertyRelative("Parameter").intValue).ToString(),
                InitialValue = entry.FindPropertyRelative("InitialValue").floatValue,
                MinValue = entry.FindPropertyRelative("MinValue").floatValue,
                MaxValue = entry.FindPropertyRelative("MaxValue").floatValue,
                CriticalLevel = entry.FindPropertyRelative("CriticalLevel").floatValue,
                Deriva = entry.FindPropertyRelative("Deriva").floatValue,
            });
        }

        data.Interaction = (InteractionConfig)stats.FindProperty("_interactionConfig").boxedValue;

        foreach (var card in BalanceDataFormat.ReadObjectArray<CardData>(turn.FindProperty("_cardPool")))
        {
            data.Cards.Add(new BalanceDataFormat.CardDto
            {
                Id = card.name,
                Name = PortugueseText(card, "_cardName"),
                Tier = card.Tier.ToString(),
                Archetype = card.Archetype.ToString(),
                Cost = card.Cost,
                RequiredPesquisa = card.RequiredPesquisa,
                PlacesStructure = card.StructureToPlace != null,
                Ability = card.Ability.ToString(),
                Copies = card.Copies,
                Effects = ToDtos(card.StatEffects),
            });
        }

        foreach (var randomEvent in BalanceDataFormat.ReadObjectArray<RandomEventData>(turn.FindProperty("_eventPool")))
        {
            var dto = new BalanceDataFormat.EventDto
            {
                Id = randomEvent.name,
                Title = PortugueseText(randomEvent, "_title"),
                MinTurn = randomEvent.MinTurn,
                MaxTurn = randomEvent.MaxTurn,
                Effects = ToDtos(randomEvent.StatEffects),
            };
            if (randomEvent.TriggerConditions != null)
            {
                foreach (var condition in randomEvent.TriggerConditions)
                {
                    dto.Conditions.Add(new BalanceDataFormat.ConditionDto
                    {
                        Parameter = condition.Parameter.ToString(),
                        Comparison = condition.Comparison.ToString(),
                        Threshold = condition.Threshold,
                    });
                }
            }
            data.Events.Add(dto);
        }

        foreach (var advantage in BalanceDataFormat.ReadObjectArray<PassiveAdvantageData>(turn.FindProperty("_advantagePool")))
        {
            data.Advantages.Add(new BalanceDataFormat.AdvantageDto
            {
                Id = advantage.name,
                Name = PortugueseText(advantage, "_advantageName"),
                Effects = ToDtos(advantage.StatEffects),
            });
        }

        foreach (var preset in BalanceDataFormat.ReadObjectArray<CityParameterPresetData>(stats.FindProperty("_phasePresets")))
        {
            var dto = new BalanceDataFormat.PresetDto { Id = preset.name, Name = PortugueseText(preset, "_presetName"), DerivaMultiplier = preset.DerivaMultiplier };
            foreach (var overrideValue in preset.Overrides)
                dto.Overrides.Add(new BalanceDataFormat.OverrideDto { Parameter = overrideValue.Parameter.ToString(), InitialValue = overrideValue.InitialValue });
            data.Presets.Add(dto);
        }

        return data;
    }

    // Texto em portugues direto do campo serializado, independente do idioma ativo no Editor.
    private static string PortugueseText(UnityEngine.Object asset, string fieldName)
    {
        return new SerializedObject(asset).FindProperty(fieldName).stringValue;
    }

    private static List<BalanceDataFormat.ModifierDto> ToDtos(IReadOnlyList<StatModifier> modifiers)
    {
        var dtos = new List<BalanceDataFormat.ModifierDto>();
        if (modifiers == null)
            return dtos;

        foreach (var modifier in modifiers)
            dtos.Add(new BalanceDataFormat.ModifierDto { Parameter = modifier.Parameter.ToString(), Amount = modifier.Amount });
        return dtos;
    }
}
