using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exporta pra JSON tudo que o simulador de balanceamento precisa, lido da cena da fase pra usar exatamente os dados do jogo.
public static class BalanceDataExporter
{
    private const string ScenePath = "Assets/Scenes/City_Scene.unity";
    private const string OutputRelativePath = "Resources/Balance/sim/balance_data.json";

    [Serializable]
    private class ModifierDto
    {
        public string Parameter;
        public float Amount;
    }

    [Serializable]
    private class ConditionDto
    {
        public string Parameter;
        public string Comparison;
        public float Threshold;
    }

    [Serializable]
    private class CardDto
    {
        public string Id;
        public string Name;
        public string Tier;
        public string Archetype;
        public float Cost;
        public float RequiredPesquisa;
        public bool PlacesStructure;
        public List<ModifierDto> Effects = new List<ModifierDto>();
    }

    [Serializable]
    private class EventDto
    {
        public string Id;
        public string Title;
        public int MinTurn;
        public int MaxTurn;
        public List<ConditionDto> Conditions = new List<ConditionDto>();
        public List<ModifierDto> Effects = new List<ModifierDto>();
    }

    [Serializable]
    private class AdvantageDto
    {
        public string Id;
        public string Name;
        public List<ModifierDto> Effects = new List<ModifierDto>();
    }

    [Serializable]
    private class OverrideDto
    {
        public string Parameter;
        public float InitialValue;
    }

    [Serializable]
    private class PresetDto
    {
        public string Id;
        public string Name;
        public List<OverrideDto> Overrides = new List<OverrideDto>();
    }

    [Serializable]
    private class ParameterDto
    {
        public string Parameter;
        public float InitialValue;
        public float MinValue;
        public float MaxValue;
        public float CriticalLevel;
        public float Deriva;
    }

    [Serializable]
    private class RulesDto
    {
        public int HandSize;
        public int VictoryTurnCount;
        public int RewardOptionCount;
    }

    [Serializable]
    private class BalanceDataDto
    {
        public string ExportedAt;
        public RulesDto Rules = new RulesDto();
        public List<ParameterDto> Parameters = new List<ParameterDto>();
        public InteractionConfig Interaction;
        public List<CardDto> Cards = new List<CardDto>();
        public List<EventDto> Events = new List<EventDto>();
        public List<AdvantageDto> Advantages = new List<AdvantageDto>();
        public List<PresetDto> Presets = new List<PresetDto>();
    }

    [MenuItem("Tools/Metis/Balanceamento/Exportar Dados do Simulador")]
    public static void Export()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var openedHere = false;
        if (scene.isLoaded == false)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        else if (scene.isDirty)
        {
            Debug.LogWarning("[BalanceDataExporter] City_Scene tem mudancas nao salvas, exportando os valores atuais em memoria.");
        }

        try
        {
            var data = BuildData(scene);
            var outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputRelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(data, true));
            Debug.Log($"[BalanceDataExporter] {data.Cards.Count} cartas, {data.Events.Count} eventos, {data.Advantages.Count} vantagens e {data.Presets.Count} presets exportados pra '{outputPath}'.");
        }
        finally
        {
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static BalanceDataDto BuildData(Scene scene)
    {
        var turnManager = FindInScene<TurnManager>(scene);
        var statsManager = FindInScene<CityStatsManager>(scene);
        var rewardPopup = FindInScene<PhaseRewardPopupView>(scene);

        var turn = new SerializedObject(turnManager);
        var stats = new SerializedObject(statsManager);
        var reward = new SerializedObject(rewardPopup);

        var data = new BalanceDataDto { ExportedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") };
        data.Rules.HandSize = turn.FindProperty("_handSize").intValue;
        data.Rules.VictoryTurnCount = turn.FindProperty("_victoryTurnCount").intValue;
        data.Rules.RewardOptionCount = Math.Min(
            reward.FindProperty("_optionCount").intValue,
            reward.FindProperty("_optionButtons").arraySize);

        var parameters = stats.FindProperty("_initialParameters");
        for (int i = 0; i < parameters.arraySize; i++)
        {
            var entry = parameters.GetArrayElementAtIndex(i);
            data.Parameters.Add(new ParameterDto
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

        foreach (var card in ReadObjectArray<CardData>(turn.FindProperty("_cardPool")))
        {
            data.Cards.Add(new CardDto
            {
                Id = card.name,
                Name = PortugueseText(card, "_cardName"),
                Tier = card.Tier.ToString(),
                Archetype = card.Archetype.ToString(),
                Cost = card.Cost,
                RequiredPesquisa = card.RequiredPesquisa,
                PlacesStructure = card.StructureToPlace != null,
                Effects = ToDtos(card.StatEffects),
            });
        }

        foreach (var randomEvent in ReadObjectArray<RandomEventData>(turn.FindProperty("_eventPool")))
        {
            var dto = new EventDto
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
                    dto.Conditions.Add(new ConditionDto
                    {
                        Parameter = condition.Parameter.ToString(),
                        Comparison = condition.Comparison.ToString(),
                        Threshold = condition.Threshold,
                    });
                }
            }
            data.Events.Add(dto);
        }

        foreach (var advantage in ReadObjectArray<PassiveAdvantageData>(turn.FindProperty("_advantagePool")))
        {
            data.Advantages.Add(new AdvantageDto
            {
                Id = advantage.name,
                Name = PortugueseText(advantage, "_advantageName"),
                Effects = ToDtos(advantage.StatEffects),
            });
        }

        foreach (var preset in ReadObjectArray<CityParameterPresetData>(stats.FindProperty("_phasePresets")))
        {
            var dto = new PresetDto { Id = preset.name, Name = PortugueseText(preset, "_presetName") };
            foreach (var overrideValue in preset.Overrides)
                dto.Overrides.Add(new OverrideDto { Parameter = overrideValue.Parameter.ToString(), InitialValue = overrideValue.InitialValue });
            data.Presets.Add(dto);
        }

        return data;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        throw new InvalidOperationException($"Nenhum {typeof(T).Name} encontrado em '{scene.path}'.");
    }

    private static List<T> ReadObjectArray<T>(SerializedProperty array) where T : UnityEngine.Object
    {
        var items = new List<T>();
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue is T item)
                items.Add(item);
        }
        return items;
    }

    // Texto em portugues direto do campo serializado, independente do idioma ativo no Editor.
    private static string PortugueseText(UnityEngine.Object asset, string fieldName)
    {
        return new SerializedObject(asset).FindProperty(fieldName).stringValue;
    }

    private static List<ModifierDto> ToDtos(IReadOnlyList<StatModifier> modifiers)
    {
        var dtos = new List<ModifierDto>();
        if (modifiers == null)
            return dtos;

        foreach (var modifier in modifiers)
            dtos.Add(new ModifierDto { Parameter = modifier.Parameter.ToString(), Amount = modifier.Amount });
        return dtos;
    }
}
