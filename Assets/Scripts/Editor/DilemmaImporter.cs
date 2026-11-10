using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Cria ou atualiza dilemas e consequencias a partir de um JSON e os coloca na lista de eventos da cidade.
public static class DilemmaImporter
{
    public const string Folder = "Assets/Data/Event";

    [MenuItem("Tools/Metis/Educacao/Importar Dilemas...")]
    private static void ImportFromMenu()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resources/Conteudo"));
        var path = EditorUtility.OpenFilePanel("Dilemas", folder, "json");
        if (string.IsNullOrEmpty(path))
            return;

        var preview = Import(path, apply: false);
        if (preview.Errors.Count > 0)
        {
            EditorUtility.DisplayDialog("Importar dilemas", $"{preview.Errors.Count} erros, nada foi mudado (lista no Console).", "OK");
            return;
        }
        if (EditorUtility.DisplayDialog("Importar dilemas", $"{preview.Changes.Count} mudancas e {preview.Warnings.Count} avisos (lista no Console). Aplicar?", "Aplicar", "Cancelar"))
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
        var file = JsonUtility.FromJson<DilemmaImportFormat.FileDto>(File.ReadAllText(jsonPath));
        report.Errors.AddRange(DilemmaImportFormat.Validate(file, CommonEventNames(file)));
        foreach (var error in report.Errors)
            Debug.LogError($"[Dilemas] {error}");
        if (report.Errors.Count > 0)
            return report;

        report.Warnings.AddRange(DilemmaImportFormat.Warnings(file));
        var assets = new List<RandomEventData>();
        foreach (var dto in file.FollowUps)
            assets.Add(ApplyFollowUp(dto, report, apply));
        foreach (var dto in file.Dilemmas)
            assets.Add(ApplyDilemma(dto, report, apply));

        WarnOrphans(file, report);
        ApplyPool(assets, report, apply);
        foreach (var warning in report.Warnings)
            Debug.LogWarning($"[Dilemas] {warning}");
        foreach (var change in report.Changes)
            Debug.Log($"[Dilemas] {(apply ? "" : "(previa) ")}{change}");
        if (apply)
            AssetDatabase.SaveAssets();
        return report;
    }

    private static HashSet<string> Ids(DilemmaImportFormat.FileDto file)
    {
        return new HashSet<string>(file.Dilemmas.Select(dto => dto.Id).Concat(file.FollowUps.Select(dto => dto.Id)));
    }

    private static IEnumerable<RandomEventData> AllEvents()
    {
        return AssetDatabase.FindAssets("t:RandomEventData", new[] { Folder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<RandomEventData>(AssetDatabase.GUIDToAssetPath(guid)));
    }

    // O arquivo so pode sobrescrever o que ja e dilema; evento comum com o mesmo nome e erro.
    private static HashSet<string> CommonEventNames(DilemmaImportFormat.FileDto file)
    {
        return new HashSet<string>(AllEvents().Where(asset => BalanceDataImporter.IsDilemmaContent(asset) == false).Select(asset => asset.name));
    }

    private static void WarnOrphans(DilemmaImportFormat.FileDto file, Report report)
    {
        var ids = Ids(file);
        foreach (var asset in AllEvents())
        {
            if (BalanceDataImporter.IsDilemmaContent(asset) && ids.Contains(asset.name) == false)
                report.Warnings.Add($"{asset.name} nao esta no arquivo e continua no jogo");
        }
    }

    private static RandomEventData LoadOrCreate(string id, Report report, bool apply)
    {
        var path = $"{Folder}/{id}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<RandomEventData>(path);
        if (asset != null)
            return asset;

        report.Changes.Add($"novo evento {id}");
        if (apply == false)
            return null;
        asset = ScriptableObject.CreateInstance<RandomEventData>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static RandomEventData ApplyFollowUp(DilemmaImportFormat.FollowUpDto dto, Report report, bool apply)
    {
        var asset = LoadOrCreate(dto.Id, report, apply);
        if (asset == null)
            return null;

        var so = new SerializedObject(asset);
        SetTexts(so, dto.Id, dto.TitlePt, dto.TitleEn, dto.DescriptionPt, dto.DescriptionEn, report);
        SetBool(so, "_isFollowUpOnly", true, dto.Id, report);
        SetInt(so, "_minTurn", 1, dto.Id, report);
        SetInt(so, "_maxTurn", DilemmaImportFormat.PhaseTurns, dto.Id, report);
        SetEffects(so.FindProperty("_statEffects"), dto.Effects, $"{dto.Id}._statEffects", report);
        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
        return asset;
    }

    private static RandomEventData ApplyDilemma(DilemmaImportFormat.DilemmaDto dto, Report report, bool apply)
    {
        var asset = LoadOrCreate(dto.Id, report, apply);
        if (asset == null)
            return null;

        var so = new SerializedObject(asset);
        SetTexts(so, dto.Id, dto.TitlePt, dto.TitleEn, dto.DescriptionPt, dto.DescriptionEn, report);
        SetBool(so, "_isFollowUpOnly", false, dto.Id, report);
        SetInt(so, "_minTurn", dto.MinTurn, dto.Id, report);
        SetInt(so, "_maxTurn", dto.MaxTurn, dto.Id, report);

        var choices = so.FindProperty("_choices");
        if (choices.arraySize != dto.Choices.Length)
        {
            report.Changes.Add($"{dto.Id}._choices: quantidade");
            choices.arraySize = dto.Choices.Length;
        }
        for (var i = 0; i < dto.Choices.Length; i++)
            ApplyChoice(choices.GetArrayElementAtIndex(i), dto.Choices[i], $"{dto.Id}._choices[{i}]", report);

        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
        return asset;
    }

    private static void ApplyChoice(SerializedProperty element, DilemmaImportFormat.ChoiceDto dto, string id, Report report)
    {
        SetText(element.FindPropertyRelative("Label"), dto.LabelPt, $"{id}.Label", report);
        SetText(element.FindPropertyRelative("LabelEn"), dto.LabelEn, $"{id}.LabelEn", report);
        SetText(element.FindPropertyRelative("Detail"), dto.DetailPt, $"{id}.Detail", report);
        SetText(element.FindPropertyRelative("DetailEn"), dto.DetailEn, $"{id}.DetailEn", report);

        var rule = element.FindPropertyRelative("Rule");
        SetEffects(rule.FindPropertyRelative("Effects"), dto.Effects, $"{id}.Effects", report);
        SetText(rule.FindPropertyRelative("FollowUpEventId"), dto.FollowUp, $"{id}.FollowUpEventId", report);
        var delay = rule.FindPropertyRelative("FollowUpDelay");
        if (delay.intValue != dto.Delay)
        {
            report.Changes.Add($"{id}.FollowUpDelay");
            delay.intValue = dto.Delay;
        }
    }

    private static void ApplyPool(List<RandomEventData> assets, Report report, bool apply)
    {
        BalanceDataFormat.WithCityScene(scene =>
        {
            var turn = new SerializedObject(BalanceDataFormat.FindInScene<TurnManager>(scene));
            var pool = turn.FindProperty("_eventPool");
            var present = new HashSet<UnityEngine.Object>();
            for (var i = 0; i < pool.arraySize; i++)
                present.Add(pool.GetArrayElementAtIndex(i).objectReferenceValue);

            var added = false;
            foreach (var asset in assets.Where(candidate => candidate != null && present.Contains(candidate) == false))
            {
                report.Changes.Add($"lista de eventos da cidade: {asset.name}");
                pool.arraySize++;
                pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = asset;
                added = true;
            }

            // Na previa os eventos novos ainda nao existem, entao entram no relato sem asset.
            var missing = assets.Count(candidate => candidate == null);
            if (missing > 0)
                report.Changes.Add($"lista de eventos da cidade: {missing} eventos novos");

            if (apply && added)
            {
                turn.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return added;
        });
    }

    private static void SetTexts(SerializedObject so, string id, string titlePt, string titleEn, string descriptionPt, string descriptionEn, Report report)
    {
        SetText(so.FindProperty("_title"), titlePt, $"{id}._title", report);
        SetText(so.FindProperty("_titleEn"), titleEn, $"{id}._titleEn", report);
        SetText(so.FindProperty("_description"), descriptionPt, $"{id}._description", report);
        SetText(so.FindProperty("_descriptionEn"), descriptionEn, $"{id}._descriptionEn", report);
    }

    private static void SetText(SerializedProperty property, string value, string id, Report report)
    {
        var wanted = value?.Trim() ?? string.Empty;
        if (property.stringValue == wanted)
            return;
        report.Changes.Add(id);
        property.stringValue = wanted;
    }

    private static void SetBool(SerializedObject so, string field, bool value, string id, Report report)
    {
        var property = so.FindProperty(field);
        if (property.boolValue == value)
            return;
        report.Changes.Add($"{id}.{field}");
        property.boolValue = value;
    }

    private static void SetInt(SerializedObject so, string field, int value, string id, Report report)
    {
        var property = so.FindProperty(field);
        if (property.intValue == value)
            return;
        report.Changes.Add($"{id}.{field}");
        property.intValue = value;
    }

    private static void SetEffects(SerializedProperty property, DilemmaImportFormat.EffectDto[] effects, string id, Report report)
    {
        var dtos = effects ?? Array.Empty<DilemmaImportFormat.EffectDto>();
        var differ = property.arraySize != dtos.Length;
        for (var i = 0; i < dtos.Length && differ == false; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            var parameter = element.FindPropertyRelative("Parameter");
            differ = parameter.enumNames[parameter.enumValueIndex] != dtos[i].Parameter
                || Mathf.Approximately(element.FindPropertyRelative("Amount").floatValue, dtos[i].Amount) == false;
        }
        if (differ == false)
            return;

        report.Changes.Add(id);
        property.arraySize = dtos.Length;
        for (var i = 0; i < dtos.Length; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            var parameter = element.FindPropertyRelative("Parameter");
            parameter.enumValueIndex = Array.IndexOf(parameter.enumNames, dtos[i].Parameter);
            element.FindPropertyRelative("Amount").floatValue = dtos[i].Amount;
        }
    }
}
