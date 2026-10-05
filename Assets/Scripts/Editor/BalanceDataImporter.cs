using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Aplica nos assets e na cena da fase os numeros de um JSON no formato do exportador, vindos do simulador de balanceamento.
// So mexe em numeros (custo, gate, efeitos, copias, condicoes, derivas, presets, matriz, regras); textos, arte e tipos nao mudam.
public static class BalanceDataImporter
{
    private const float Tolerance = 1e-4f;

    [MenuItem("Tools/Metis/Balanceamento/Importar Dados do Simulador...")]
    private static void ImportFromMenu()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resources/Balance/sim"));
        var path = EditorUtility.OpenFilePanel("Dados de balanceamento", folder, "json");
        if (string.IsNullOrEmpty(path))
            return;

        var preview = Import(path, apply: false);
        var changes = preview.Count(line => line.StartsWith("  "));
        if (EditorUtility.DisplayDialog("Importar balanceamento", $"{changes} campos mudam (lista completa no Console).\n\nAplicar nos assets e na City_Scene?", "Aplicar", "Cancelar"))
            Import(path, apply: true);
    }

    // Com apply false so relata o que mudaria; devolve o relatorio, uma linha por campo alterado.
    public static List<string> Import(string jsonPath, bool apply)
    {
        // A previa passa por todos os campos e valida todo enum e id; so depois disso algo e gravado.
        if (apply)
            Import(jsonPath, apply: false);

        var data = JsonUtility.FromJson<BalanceDataFormat.BalanceDataDto>(File.ReadAllText(jsonPath));
        var report = new List<string>();

        BalanceDataFormat.WithCityScene(scene =>
        {
            var turnManager = BalanceDataFormat.FindInScene<TurnManager>(scene);
            var statsManager = BalanceDataFormat.FindInScene<CityStatsManager>(scene);
            var rewardPopup = BalanceDataFormat.FindInScene<PhaseRewardPopupView>(scene);
            var turn = new SerializedObject(turnManager);
            var stats = new SerializedObject(statsManager);
            var reward = new SerializedObject(rewardPopup);

            var cards = BalanceDataFormat.ReadObjectArray<CardData>(turn.FindProperty("_cardPool")).ToDictionary(card => card.name);
            var events = BalanceDataFormat.ReadObjectArray<RandomEventData>(turn.FindProperty("_eventPool")).ToDictionary(item => item.name);
            var advantages = BalanceDataFormat.ReadObjectArray<PassiveAdvantageData>(turn.FindProperty("_advantagePool")).ToDictionary(item => item.name);
            var presets = BalanceDataFormat.ReadObjectArray<CityParameterPresetData>(stats.FindProperty("_phasePresets")).ToDictionary(item => item.name);

            // Valida tudo antes de escrever qualquer coisa: um id desconhecido aborta a importacao inteira.
            RequireIds("carta", data.Cards.Select(card => card.Id), cards.Keys);
            RequireIds("evento", data.Events.Select(item => item.Id), events.Keys);
            RequireIds("vantagem", data.Advantages.Select(item => item.Id), advantages.Keys);
            RequireIds("preset", data.Presets.Select(item => item.Id), presets.Keys);
            if (data.Presets.Any(preset => preset.DerivaMultiplier <= 0f))
                throw new InvalidDataException("Preset sem DerivaMultiplier valido (precisa ser maior que 0).");
            if (data.Rules.HandSize <= 0 || data.Rules.VictoryTurnCount <= 0 || data.Rules.RewardOptionCount <= 0)
                throw new InvalidDataException("Regras com valor ausente ou nao positivo.");

            foreach (var dto in data.Cards)
            {
                var so = new SerializedObject(cards[dto.Id]);
                SetFloat(so, "_cost", dto.Cost, $"{dto.Id}.Cost", report);
                SetFloat(so, "_requiredPesquisa", dto.RequiredPesquisa, $"{dto.Id}.RequiredPesquisa", report);
                if (dto.Copies > 0)
                    SetInt(so, "_copies", dto.Copies, $"{dto.Id}.Copies", report);
                SetModifiers(so.FindProperty("_statEffects"), dto.Effects, $"{dto.Id}.Effects", report);
                Finish(so, apply);
            }

            foreach (var dto in data.Events)
            {
                var so = new SerializedObject(events[dto.Id]);
                SetInt(so, "_minTurn", dto.MinTurn, $"{dto.Id}.MinTurn", report);
                SetInt(so, "_maxTurn", dto.MaxTurn, $"{dto.Id}.MaxTurn", report);
                SetConditions(so.FindProperty("_triggerConditions"), dto.Conditions, $"{dto.Id}.Conditions", report);
                SetModifiers(so.FindProperty("_statEffects"), dto.Effects, $"{dto.Id}.Effects", report);
                Finish(so, apply);
            }

            foreach (var dto in data.Advantages)
            {
                var so = new SerializedObject(advantages[dto.Id]);
                SetModifiers(so.FindProperty("_statEffects"), dto.Effects, $"{dto.Id}.Effects", report);
                Finish(so, apply);
            }

            foreach (var dto in data.Presets)
            {
                var so = new SerializedObject(presets[dto.Id]);
                SetFloat(so, "_derivaMultiplier", dto.DerivaMultiplier, $"{dto.Id}.DerivaMultiplier", report);
                SetOverrides(so.FindProperty("_overrides"), dto.Overrides, $"{dto.Id}.Overrides", report);
                Finish(so, apply);
            }

            SetParameters(stats.FindProperty("_initialParameters"), data.Parameters, report);
            SetInteraction(stats.FindProperty("_interactionConfig"), data.Interaction, report);
            SetInt(turn, "_handSize", data.Rules.HandSize, "Regras.HandSize", report);
            SetInt(turn, "_victoryTurnCount", data.Rules.VictoryTurnCount, "Regras.VictoryTurnCount", report);
            SetInt(reward, "_optionCount", data.Rules.RewardOptionCount, "Regras.RewardOptionCount", report);
            Finish(stats, apply);
            Finish(turn, apply);
            Finish(reward, apply);

            if (apply)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return 0;
        });

        if (apply)
            AssetDatabase.SaveAssets();

        var header = apply ? "[BalanceDataImporter] Aplicado" : "[BalanceDataImporter] Previa (nada gravado)";
        report.Insert(0, $"{header}: {report.Count} campos diferentes em '{jsonPath}'.");
        Debug.Log(string.Join("\n", report));
        return report;
    }

    private static void RequireIds(string kind, IEnumerable<string> wanted, IEnumerable<string> known)
    {
        var missing = wanted.Except(known).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"Nenhum(a) {kind} no jogo com id: {string.Join(", ", missing)}. Nada foi alterado.");
    }

    private static void Finish(SerializedObject so, bool apply)
    {
        if (apply == false)
            return;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(so.targetObject);
    }

    private static void SetFloat(SerializedObject so, string field, float value, string label, List<string> report)
    {
        var property = so.FindProperty(field);
        if (Mathf.Abs(property.floatValue - value) <= Tolerance)
            return;

        report.Add($"  {label}: {Format(property.floatValue)} -> {Format(value)}");
        property.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string field, int value, string label, List<string> report)
    {
        var property = so.FindProperty(field);
        if (property.intValue == value)
            return;

        report.Add($"  {label}: {property.intValue} -> {value}");
        property.intValue = value;
    }

    private static void SetModifiers(SerializedProperty array, List<BalanceDataFormat.ModifierDto> wanted, string label, List<string> report)
    {
        var current = new List<string>();
        for (int i = 0; i < array.arraySize; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            current.Add($"{(CityParameterType)element.FindPropertyRelative("Parameter").intValue} {Format(element.FindPropertyRelative("Amount").floatValue)}");
        }
        var target = wanted.Select(modifier => $"{ParseEnum<CityParameterType>(modifier.Parameter)} {Format(modifier.Amount)}").ToList();
        if (current.SequenceEqual(target))
            return;

        report.Add($"  {label}: [{string.Join(", ", current)}] -> [{string.Join(", ", target)}]");
        array.arraySize = wanted.Count;
        for (int i = 0; i < wanted.Count; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Parameter").intValue = (int)ParseEnum<CityParameterType>(wanted[i].Parameter);
            element.FindPropertyRelative("Amount").floatValue = wanted[i].Amount;
        }
    }

    private static void SetConditions(SerializedProperty array, List<BalanceDataFormat.ConditionDto> wanted, string label, List<string> report)
    {
        var current = new List<string>();
        for (int i = 0; i < array.arraySize; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            current.Add($"{(CityParameterType)element.FindPropertyRelative("Parameter").intValue} {(ComparisonType)element.FindPropertyRelative("Comparison").intValue} {Format(element.FindPropertyRelative("Threshold").floatValue)}");
        }
        var target = wanted.Select(condition => $"{ParseEnum<CityParameterType>(condition.Parameter)} {ParseEnum<ComparisonType>(condition.Comparison)} {Format(condition.Threshold)}").ToList();
        if (current.SequenceEqual(target))
            return;

        report.Add($"  {label}: [{string.Join(", ", current)}] -> [{string.Join(", ", target)}]");
        array.arraySize = wanted.Count;
        for (int i = 0; i < wanted.Count; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Parameter").intValue = (int)ParseEnum<CityParameterType>(wanted[i].Parameter);
            element.FindPropertyRelative("Comparison").intValue = (int)ParseEnum<ComparisonType>(wanted[i].Comparison);
            element.FindPropertyRelative("Threshold").floatValue = wanted[i].Threshold;
        }
    }

    private static void SetOverrides(SerializedProperty array, List<BalanceDataFormat.OverrideDto> wanted, string label, List<string> report)
    {
        var current = new List<string>();
        for (int i = 0; i < array.arraySize; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            current.Add($"{(CityParameterType)element.FindPropertyRelative("Parameter").intValue} {Format(element.FindPropertyRelative("InitialValue").floatValue)}");
        }
        var target = wanted.Select(item => $"{ParseEnum<CityParameterType>(item.Parameter)} {Format(item.InitialValue)}").ToList();
        if (current.SequenceEqual(target))
            return;

        report.Add($"  {label}: [{string.Join(", ", current)}] -> [{string.Join(", ", target)}]");
        array.arraySize = wanted.Count;
        for (int i = 0; i < wanted.Count; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Parameter").intValue = (int)ParseEnum<CityParameterType>(wanted[i].Parameter);
            element.FindPropertyRelative("InitialValue").floatValue = wanted[i].InitialValue;
        }
    }

    // Casa cada parametro do JSON com a entrada de mesmo tipo na cena; parametro ausente na cena aborta.
    private static void SetParameters(SerializedProperty array, List<BalanceDataFormat.ParameterDto> wanted, List<string> report)
    {
        foreach (var dto in wanted)
        {
            var parameter = ParseEnum<CityParameterType>(dto.Parameter);
            SerializedProperty entry = null;
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).FindPropertyRelative("Parameter").intValue == (int)parameter)
                    entry = array.GetArrayElementAtIndex(i);
            }
            if (entry == null)
                throw new InvalidDataException($"Parametro {parameter} nao existe no CityStatsManager da cena.");

            SetRelativeFloat(entry, "InitialValue", dto.InitialValue, $"Parametros.{parameter}.InitialValue", report);
            SetRelativeFloat(entry, "MinValue", dto.MinValue, $"Parametros.{parameter}.MinValue", report);
            SetRelativeFloat(entry, "MaxValue", dto.MaxValue, $"Parametros.{parameter}.MaxValue", report);
            SetRelativeFloat(entry, "CriticalLevel", dto.CriticalLevel, $"Parametros.{parameter}.CriticalLevel", report);
            SetRelativeFloat(entry, "Deriva", dto.Deriva, $"Parametros.{parameter}.Deriva", report);
        }
    }

    private static void SetInteraction(SerializedProperty property, InteractionConfig wanted, List<string> report)
    {
        foreach (var field in typeof(InteractionConfig).GetFields())
            SetRelativeFloat(property, field.Name, (float)field.GetValue(wanted), $"Interacao.{field.Name}", report);
    }

    private static void SetRelativeFloat(SerializedProperty parent, string field, float value, string label, List<string> report)
    {
        var property = parent.FindPropertyRelative(field);
        if (Mathf.Abs(property.floatValue - value) <= Tolerance)
            return;

        report.Add($"  {label}: {Format(property.floatValue)} -> {Format(value)}");
        property.floatValue = value;
    }

    private static T ParseEnum<T>(string value) where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(typeof(T), parsed))
            return parsed;
        throw new InvalidDataException($"Valor '{value}' desconhecido pra {typeof(T).Name}.");
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
