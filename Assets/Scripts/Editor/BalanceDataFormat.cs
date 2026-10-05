using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Formato JSON trocado com o simulador de balanceamento, e o acesso a cena da fase, compartilhados pelo exportador e pelo importador.
public static class BalanceDataFormat
{
    public const string ScenePath = "Assets/Scenes/City_Scene.unity";

    [Serializable]
    public class ModifierDto
    {
        public string Parameter;
        public float Amount;
    }

    [Serializable]
    public class ConditionDto
    {
        public string Parameter;
        public string Comparison;
        public float Threshold;
    }

    [Serializable]
    public class CardDto
    {
        public string Id;
        public string Name;
        public string Tier;
        public string Archetype;
        public float Cost;
        public float RequiredPesquisa;
        public bool PlacesStructure;
        public string Ability;
        public int Copies;
        public List<ModifierDto> Effects = new List<ModifierDto>();
    }

    [Serializable]
    public class EventDto
    {
        public string Id;
        public string Title;
        public int MinTurn;
        public int MaxTurn;
        public List<ConditionDto> Conditions = new List<ConditionDto>();
        public List<ModifierDto> Effects = new List<ModifierDto>();
    }

    [Serializable]
    public class AdvantageDto
    {
        public string Id;
        public string Name;
        public List<ModifierDto> Effects = new List<ModifierDto>();
    }

    [Serializable]
    public class OverrideDto
    {
        public string Parameter;
        public float InitialValue;
    }

    [Serializable]
    public class PresetDto
    {
        public string Id;
        public string Name;
        public List<OverrideDto> Overrides = new List<OverrideDto>();
        public float DerivaMultiplier;
    }

    [Serializable]
    public class ParameterDto
    {
        public string Parameter;
        public float InitialValue;
        public float MinValue;
        public float MaxValue;
        public float CriticalLevel;
        public float Deriva;
    }

    [Serializable]
    public class RulesDto
    {
        public int HandSize;
        public int VictoryTurnCount;
        public int RewardOptionCount;
    }

    [Serializable]
    public class BalanceDataDto
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

    // Roda a acao com a cena da fase carregada, abrindo e fechando ela so se ainda nao estava aberta.
    public static T WithCityScene<T>(Func<Scene, T> action)
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var openedHere = false;
        if (scene.isLoaded == false)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            return action(scene);
        }
        finally
        {
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    public static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        throw new InvalidOperationException($"Nenhum {typeof(T).Name} encontrado em '{scene.path}'.");
    }

    public static List<T> ReadObjectArray<T>(SerializedProperty array) where T : UnityEngine.Object
    {
        var items = new List<T>();
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue is T item)
                items.Add(item);
        }
        return items;
    }
}
