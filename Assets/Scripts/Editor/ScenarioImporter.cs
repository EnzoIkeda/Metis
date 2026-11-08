using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Cria ou atualiza os cenarios a partir de um JSON e monta a biblioteca na ordem do arquivo.
public static class ScenarioImporter
{
    public const string Folder = "Assets/Data/Scenarios";
    public const string LibraryPath = Folder + "/ScenarioLibrary.asset";

    [MenuItem("Tools/Metis/Educacao/Importar Cenarios...")]
    private static void ImportFromMenu()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resources/Conteudo"));
        var path = EditorUtility.OpenFilePanel("Cenarios", folder, "json");
        if (string.IsNullOrEmpty(path))
            return;

        var preview = Import(path, apply: false);
        if (preview.Errors.Count > 0)
        {
            EditorUtility.DisplayDialog("Importar cenarios", $"{preview.Errors.Count} erros, nada foi mudado (lista no Console).", "OK");
            return;
        }
        if (EditorUtility.DisplayDialog("Importar cenarios", $"{preview.Changes.Count} mudancas (lista no Console).\n\nAplicar nos assets?", "Aplicar", "Cancelar"))
            Import(path, apply: true);
    }

    public class Report
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Changes = new List<string>();
    }

    // Com apply false so valida e relata o que mudaria. Com erro nada e gravado.
    public static Report Import(string jsonPath, bool apply)
    {
        var report = new Report();
        var file = JsonUtility.FromJson<ScenarioImportFormat.FileDto>(File.ReadAllText(jsonPath));
        var themes = AssetDatabase.FindAssets("t:ThemedPhaseData", new[] { "Assets/Data" })
            .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
            .ToList();
        report.Errors.AddRange(ScenarioImportFormat.Validate(file, TurnMachine.DefaultVictoryTurnCount, themes));
        foreach (var error in report.Errors)
            Debug.LogError($"[Cenarios] {error}");
        if (report.Errors.Count > 0)
            return report;

        if (apply && AssetDatabase.IsValidFolder(Folder) == false)
            AssetDatabase.CreateFolder("Assets/Data", "Scenarios");

        var scenarios = new List<ScenarioData>();
        foreach (var dto in file.Scenarios)
        {
            var path = $"{Folder}/{dto.Id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ScenarioData>(path);
            if (asset == null)
            {
                report.Changes.Add($"novo cenario {dto.Id}");
                if (apply == false)
                    continue;
                asset = ScriptableObject.CreateInstance<ScenarioData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            ApplyScenario(dto, asset, report, apply);
            scenarios.Add(asset);
        }

        ApplyLibrary(scenarios, file.Scenarios.Length, report, apply);
        foreach (var change in report.Changes)
            Debug.Log($"[Cenarios] {(apply ? "" : "(previa) ")}{change}");
        if (apply)
            AssetDatabase.SaveAssets();
        return report;
    }

    private static void ApplyScenario(ScenarioImportFormat.ScenarioDto dto, ScenarioData asset, Report report, bool apply)
    {
        var so = new SerializedObject(asset);

        var theme = AssetDatabase.LoadAssetAtPath<ThemedPhaseData>($"{ThemedPhaseImporter.Folder}/{dto.Theme}.asset");
        var themeProperty = so.FindProperty("_theme");
        if (themeProperty.objectReferenceValue != theme)
        {
            report.Changes.Add($"{dto.Id}._theme");
            themeProperty.objectReferenceValue = theme;
        }

        var freeProperty = so.FindProperty("_freeArchetype");
        if (freeProperty.boolValue != dto.FreeArchetype)
        {
            report.Changes.Add($"{dto.Id}._freeArchetype");
            freeProperty.boolValue = dto.FreeArchetype;
        }

        if (dto.FreeArchetype == false)
        {
            Enum.TryParse<CardArchetype>(dto.Archetype, out var archetype);
            var archetypeProperty = so.FindProperty("_archetype");
            if (archetypeProperty.intValue != (int)archetype)
            {
                report.Changes.Add($"{dto.Id}._archetype");
                archetypeProperty.intValue = (int)archetype;
            }
        }

        SetObjectives(so, dto, report);
        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectives(SerializedObject so, ScenarioImportFormat.ScenarioDto dto, Report report)
    {
        var dtos = dto.Objectives;
        var property = so.FindProperty("_objectives");
        var differ = property.arraySize != dtos.Length;
        for (var i = 0; i < dtos.Length && differ == false; i++)
            differ = Matches(property.GetArrayElementAtIndex(i), dtos[i]) == false;
        if (differ == false)
            return;

        report.Changes.Add($"{dto.Id}._objectives");
        property.arraySize = dtos.Length;
        for (var i = 0; i < dtos.Length; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            Enum.TryParse<CityParameterType>(dtos[i].Parameter, out var parameter);
            Enum.TryParse<ObjectiveComparison>(dtos[i].Comparison, out var comparison);
            element.FindPropertyRelative("Parameter").intValue = (int)parameter;
            element.FindPropertyRelative("Comparison").intValue = (int)comparison;
            element.FindPropertyRelative("Threshold").floatValue = dtos[i].Threshold;
            element.FindPropertyRelative("ByTurn").intValue = dtos[i].ByTurn;
            element.FindPropertyRelative("MustHold").boolValue = dtos[i].MustHold;
        }
    }

    private static bool Matches(SerializedProperty element, ScenarioImportFormat.ObjectiveDto dto)
    {
        Enum.TryParse<CityParameterType>(dto.Parameter, out var parameter);
        Enum.TryParse<ObjectiveComparison>(dto.Comparison, out var comparison);
        return element.FindPropertyRelative("Parameter").intValue == (int)parameter
            && element.FindPropertyRelative("Comparison").intValue == (int)comparison
            && Mathf.Approximately(element.FindPropertyRelative("Threshold").floatValue, dto.Threshold)
            && element.FindPropertyRelative("ByTurn").intValue == dto.ByTurn
            && element.FindPropertyRelative("MustHold").boolValue == dto.MustHold;
    }

    private static void ApplyLibrary(List<ScenarioData> scenarios, int expected, Report report, bool apply)
    {
        var library = AssetDatabase.LoadAssetAtPath<ScenarioLibraryData>(LibraryPath);
        if (library == null)
        {
            report.Changes.Add("nova biblioteca de cenarios");
            if (apply == false)
                return;
            library = ScriptableObject.CreateInstance<ScenarioLibraryData>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        // Na previa os cenarios novos ainda nao existem, entao a lista so e comparada quando esta completa.
        var so = new SerializedObject(library);
        var property = so.FindProperty("_scenarios");
        var differ = scenarios.Count != expected || property.arraySize != scenarios.Count;
        for (var i = 0; i < scenarios.Count && differ == false; i++)
            differ = property.GetArrayElementAtIndex(i).objectReferenceValue != scenarios[i];
        if (differ == false)
            return;

        report.Changes.Add("biblioteca de cenarios: lista");
        property.arraySize = scenarios.Count;
        for (var i = 0; i < scenarios.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = scenarios[i];
        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }
}
