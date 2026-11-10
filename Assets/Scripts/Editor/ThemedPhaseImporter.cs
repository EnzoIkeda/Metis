using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Cria ou atualiza as fases tematicas a partir de um JSON e monta a biblioteca na ordem do arquivo.
public static class ThemedPhaseImporter
{
    public const string Folder = "Assets/Data/Themes";
    public const string LibraryPath = Folder + "/ThemedPhaseLibrary.asset";

    [MenuItem("Tools/Metis/Educacao/Importar Temas...")]
    private static void ImportFromMenu()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resources/Conteudo"));
        var path = EditorUtility.OpenFilePanel("Fases tematicas", folder, "json");
        if (string.IsNullOrEmpty(path))
            return;

        var preview = Import(path, apply: false);
        if (preview.Errors.Count > 0)
        {
            EditorUtility.DisplayDialog("Importar temas", $"{preview.Errors.Count} erros, nada foi mudado (lista no Console).", "OK");
            return;
        }
        if (EditorUtility.DisplayDialog("Importar temas", $"{preview.Changes.Count} mudancas e {preview.Warnings.Count} avisos (lista no Console).\n\nAplicar nos assets?", "Aplicar", "Cancelar"))
            Import(path, apply: true);
    }

    public class Report
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Changes = new List<string>();
    }

    // Com apply false so valida e relata o que mudaria. Com erro nada e gravado.
    public static Report Import(string jsonPath, bool apply)
    {
        var report = new Report();
        var file = JsonUtility.FromJson<ThemedPhaseImportFormat.FileDto>(File.ReadAllText(jsonPath));
        report.Errors.AddRange(ThemedPhaseImportFormat.Validate(file, AssetNames("t:RandomEventData"), AssetNames("t:KnowledgeEntryData"), FollowUpNames()));
        foreach (var error in report.Errors)
            Debug.LogError($"[Temas] {error}");
        if (report.Errors.Count > 0)
            return report;

        report.Warnings.AddRange(ThemedPhaseImportFormat.Warnings(file));
        if (apply && AssetDatabase.IsValidFolder(Folder) == false)
            AssetDatabase.CreateFolder("Assets/Data", "Themes");

        var themes = new List<ThemedPhaseData>();
        foreach (var dto in file.Themes)
        {
            var path = $"{Folder}/{dto.Id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ThemedPhaseData>(path);
            if (asset == null)
            {
                report.Changes.Add($"novo tema {dto.Id}");
                if (apply == false)
                    continue;
                asset = ScriptableObject.CreateInstance<ThemedPhaseData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            Apply(dto, asset, report, apply);
            themes.Add(asset);
        }

        var ids = new HashSet<string>(file.Themes.Select(dto => dto.Id));
        foreach (var guid in AssetDatabase.FindAssets("t:ThemedPhaseData", new[] { Folder }))
        {
            var orphan = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (ids.Contains(orphan) == false)
                report.Warnings.Add($"tema {orphan} nao esta no arquivo e fica fora da biblioteca");
        }

        ApplyLibrary(themes, file.Themes.Length, report, apply);
        foreach (var warning in report.Warnings)
            Debug.LogWarning($"[Temas] {warning}");
        foreach (var change in report.Changes)
            Debug.Log($"[Temas] {(apply ? "" : "(previa) ")}{change}");
        if (apply)
            AssetDatabase.SaveAssets();
        return report;
    }

    private static HashSet<string> FollowUpNames()
    {
        return new HashSet<string>(AssetDatabase.FindAssets("t:RandomEventData", new[] { "Assets/Data" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<RandomEventData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset.IsFollowUpOnly).Select(asset => asset.name));
    }

    private static HashSet<string> AssetNames(string filter)
    {
        return new HashSet<string>(AssetDatabase.FindAssets(filter, new[] { "Assets/Data" })
            .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid))));
    }

    private static void Apply(ThemedPhaseImportFormat.ThemeDto dto, ThemedPhaseData asset, Report report, bool apply)
    {
        var so = new SerializedObject(asset);
        SetString(so, "_themeName", dto.NamePt, dto.Id, report);
        SetString(so, "_themeNameEn", dto.NameEn, dto.Id, report);
        SetString(so, "_summary", dto.SummaryPt, dto.Id, report);
        SetString(so, "_summaryEn", dto.SummaryEn, dto.Id, report);
        SetString(so, "_opening", dto.OpeningPt, dto.Id, report);
        SetString(so, "_openingEn", dto.OpeningEn, dto.Id, report);
        SetString(so, "_victory", dto.VictoryPt, dto.Id, report);
        SetString(so, "_victoryEn", dto.VictoryEn, dto.Id, report);
        SetString(so, "_inspiration", dto.InspirationPt, dto.Id, report);
        SetString(so, "_inspirationEn", dto.InspirationEn, dto.Id, report);

        var entry = FindEntry(dto.KnowledgeEntry);
        var entryProperty = so.FindProperty("_knowledgeEntry");
        if (entryProperty.objectReferenceValue != entry)
        {
            report.Changes.Add($"{dto.Id}._knowledgeEntry");
            entryProperty.objectReferenceValue = entry;
        }

        SetFloat(so, "_derivaMultiplier", dto.DerivaMultiplier, dto.Id, report);
        SetInt(so, "_backgroundIndex", dto.BackgroundIndex, dto.Id, report);
        SetInt(so, "_layoutSeed", dto.LayoutSeed, dto.Id, report);
        SetOverrides(so, dto, report);
        SetEvents(so, dto, report);
        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static KnowledgeEntryData FindEntry(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        var guid = AssetDatabase.FindAssets($"{name} t:KnowledgeEntryData", new[] { "Assets/Data" })
            .FirstOrDefault(candidate => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(candidate)) == name);
        return guid != null ? AssetDatabase.LoadAssetAtPath<KnowledgeEntryData>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }

    private static void SetOverrides(SerializedObject so, ThemedPhaseImportFormat.ThemeDto dto, Report report)
    {
        var dtos = dto.Overrides ?? Array.Empty<ThemedPhaseImportFormat.OverrideDto>();
        var property = so.FindProperty("_overrides");
        var differ = property.arraySize != dtos.Length;
        for (var i = 0; i < dtos.Length && differ == false; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            differ = element.FindPropertyRelative("Parameter").enumNames[element.FindPropertyRelative("Parameter").enumValueIndex] != dtos[i].Parameter
                || Mathf.Approximately(element.FindPropertyRelative("InitialValue").floatValue, dtos[i].InitialValue) == false;
        }
        if (differ == false)
            return;

        report.Changes.Add($"{dto.Id}._overrides");
        property.arraySize = dtos.Length;
        for (var i = 0; i < dtos.Length; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            Enum.TryParse<CityParameterType>(dtos[i].Parameter, out var parameter);
            element.FindPropertyRelative("Parameter").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(CityParameterType)), parameter);
            element.FindPropertyRelative("InitialValue").floatValue = dtos[i].InitialValue;
        }
    }

    private static void SetEvents(SerializedObject so, ThemedPhaseImportFormat.ThemeDto dto, Report report)
    {
        var dtos = dto.AllowedEvents ?? Array.Empty<string>();
        var property = so.FindProperty("_allowedEventIds");
        var differ = property.arraySize != dtos.Length;
        for (var i = 0; i < dtos.Length && differ == false; i++)
            differ = property.GetArrayElementAtIndex(i).stringValue != dtos[i];
        if (differ == false)
            return;

        report.Changes.Add($"{dto.Id}._allowedEventIds");
        property.arraySize = dtos.Length;
        for (var i = 0; i < dtos.Length; i++)
            property.GetArrayElementAtIndex(i).stringValue = dtos[i];
    }

    private static void ApplyLibrary(List<ThemedPhaseData> themes, int expected, Report report, bool apply)
    {
        var library = AssetDatabase.LoadAssetAtPath<ThemedPhaseLibraryData>(LibraryPath);
        if (library == null)
        {
            report.Changes.Add("nova biblioteca de temas");
            if (apply == false)
                return;
            library = ScriptableObject.CreateInstance<ThemedPhaseLibraryData>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        // Na previa os temas novos ainda nao existem, entao a lista so e comparada quando esta completa.
        var so = new SerializedObject(library);
        var property = so.FindProperty("_themes");
        var differ = themes.Count != expected || property.arraySize != themes.Count;
        for (var i = 0; i < themes.Count && differ == false; i++)
            differ = property.GetArrayElementAtIndex(i).objectReferenceValue != themes[i];
        if (differ == false)
            return;

        report.Changes.Add("biblioteca de temas: lista");
        property.arraySize = themes.Count;
        for (var i = 0; i < themes.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = themes[i];
        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(SerializedObject so, string field, string value, string id, Report report)
    {
        var property = so.FindProperty(field);
        var wanted = value?.Trim() ?? string.Empty;
        if (property.stringValue == wanted)
            return;
        report.Changes.Add($"{id}.{field}");
        property.stringValue = wanted;
    }

    private static void SetFloat(SerializedObject so, string field, float value, string id, Report report)
    {
        var property = so.FindProperty(field);
        if (Mathf.Approximately(property.floatValue, value))
            return;
        report.Changes.Add($"{id}.{field}");
        property.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string field, int value, string id, Report report)
    {
        var property = so.FindProperty(field);
        if (property.intValue == value)
            return;
        report.Changes.Add($"{id}.{field}");
        property.intValue = value;
    }
}
