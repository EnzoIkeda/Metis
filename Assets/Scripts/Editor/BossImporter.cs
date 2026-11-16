using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Cria ou atualiza os chefes a partir de um JSON e monta a biblioteca na ordem do arquivo.
public static class BossImporter
{
    public const string Folder = "Assets/Data/Bosses";
    public const string LibraryPath = Folder + "/BossLibrary.asset";

    [MenuItem("Tools/Metis/Educacao/Importar Chefes...")]
    private static void ImportFromMenu()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resources/Conteudo"));
        var path = EditorUtility.OpenFilePanel("Chefes", folder, "json");
        if (string.IsNullOrEmpty(path))
            return;

        var preview = Import(path, apply: false);
        if (preview.Errors.Count > 0)
        {
            EditorUtility.DisplayDialog("Importar chefes", $"{preview.Errors.Count} erros, nada foi mudado (lista no Console).", "OK");
            return;
        }
        if (EditorUtility.DisplayDialog("Importar chefes", $"{preview.Changes.Count} mudancas e {preview.Warnings.Count} avisos (lista no Console).\n\nAplicar nos assets?", "Aplicar", "Cancelar"))
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
        var file = JsonUtility.FromJson<BossImportFormat.FileDto>(File.ReadAllText(jsonPath));
        report.Errors.AddRange(BossImportFormat.Validate(file, AssetNames("t:KnowledgeEntryData"), ExtraOnlyCards()));
        foreach (var error in report.Errors)
            Debug.LogError($"[Chefes] {error}");
        if (report.Errors.Count > 0)
            return report;

        report.Warnings.AddRange(BossImportFormat.Warnings(file));
        if (apply && AssetDatabase.IsValidFolder(Folder) == false)
            AssetDatabase.CreateFolder("Assets/Data", "Bosses");

        var bosses = new List<BossData>();
        foreach (var dto in file.Bosses)
        {
            var path = $"{Folder}/{dto.Id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<BossData>(path);
            if (asset == null)
            {
                report.Changes.Add($"novo chefe {dto.Id}");
                if (apply == false)
                    continue;
                asset = ScriptableObject.CreateInstance<BossData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            Apply(dto, asset, report, apply);
            bosses.Add(asset);
        }

        var ids = new HashSet<string>(file.Bosses.Select(dto => dto.Id));
        foreach (var guid in AssetDatabase.FindAssets("t:BossData", new[] { Folder }))
        {
            var orphan = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (ids.Contains(orphan) == false)
                report.Warnings.Add($"chefe {orphan} nao esta no arquivo e fica fora da biblioteca");
        }

        ApplyLibrary(bosses, file.Bosses.Length, report, apply);
        foreach (var warning in report.Warnings)
            Debug.LogWarning($"[Chefes] {warning}");
        foreach (var change in report.Changes)
            Debug.Log($"[Chefes] {(apply ? "" : "(previa) ")}{change}");
        if (apply)
            AssetDatabase.SaveAssets();
        return report;
    }

    private static HashSet<string> AssetNames(string filter)
    {
        return new HashSet<string>(AssetDatabase.FindAssets(filter, new[] { "Assets/Data" })
            .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid))));
    }

    private static HashSet<string> ExtraOnlyCards()
    {
        return new HashSet<string>(AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(card => card != null && card.IsExtraOnly)
            .Select(card => card.name));
    }

    private static void Apply(BossImportFormat.BossDto dto, BossData asset, Report report, bool apply)
    {
        var so = new SerializedObject(asset);
        SetString(so.FindProperty("_bossName"), dto.NamePt, $"{dto.Id}._bossName", report);
        SetString(so.FindProperty("_bossNameEn"), dto.NameEn, $"{dto.Id}._bossNameEn", report);
        SetString(so.FindProperty("_summary"), dto.SummaryPt, $"{dto.Id}._summary", report);
        SetString(so.FindProperty("_summaryEn"), dto.SummaryEn, $"{dto.Id}._summaryEn", report);
        SetString(so.FindProperty("_intro"), dto.IntroPt, $"{dto.Id}._intro", report);
        SetString(so.FindProperty("_introEn"), dto.IntroEn, $"{dto.Id}._introEn", report);
        SetString(so.FindProperty("_victory"), dto.VictoryPt, $"{dto.Id}._victory", report);
        SetString(so.FindProperty("_victoryEn"), dto.VictoryEn, $"{dto.Id}._victoryEn", report);

        var entry = FindEntry(dto.KnowledgeEntry);
        var entryProperty = so.FindProperty("_knowledgeEntry");
        if (entryProperty.objectReferenceValue != entry)
        {
            report.Changes.Add($"{dto.Id}._knowledgeEntry");
            entryProperty.objectReferenceValue = entry;
        }

        BossImportFormat.TryParseArchetype(dto.TargetArchetype, out var archetype);
        BossImportFormat.TryParseMechanic(dto.Mechanic, out var mechanic);
        SetEnum(so.FindProperty("_targetArchetype"), (int)archetype, $"{dto.Id}._targetArchetype", report);
        SetEnum(so.FindProperty("_mechanic"), (int)mechanic, $"{dto.Id}._mechanic", report);
        SetInt(so.FindProperty("_startingMomentum"), dto.StartingMomentum, $"{dto.Id}._startingMomentum", report);
        SetInt(so.FindProperty("_momentumPerTurn"), dto.MomentumPerTurn, $"{dto.Id}._momentumPerTurn", report);
        SetFloat(so.FindProperty("_mechanicAmount"), dto.MechanicAmount, $"{dto.Id}._mechanicAmount", report);
        SetString(so.FindProperty("_mechanicCardId"), dto.MechanicCardId, $"{dto.Id}._mechanicCardId", report);

        var cards = dto.Cards ?? Array.Empty<BossImportFormat.CardDto>();
        var list = so.FindProperty("_cards");
        if (list.arraySize != cards.Length)
        {
            report.Changes.Add($"{dto.Id}._cards: {cards.Length} cartas");
            list.arraySize = cards.Length;
        }
        for (var i = 0; i < cards.Length; i++)
            SetCard(list.GetArrayElementAtIndex(i), cards[i], $"{dto.Id}.{cards[i].Id}", report);

        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetCard(SerializedProperty property, BossImportFormat.CardDto dto, string where, Report report)
    {
        SetString(property.FindPropertyRelative("_id"), dto.Id, $"{where}._id", report);
        SetString(property.FindPropertyRelative("_cardName"), dto.NamePt, $"{where}._cardName", report);
        SetString(property.FindPropertyRelative("_cardNameEn"), dto.NameEn, $"{where}._cardNameEn", report);
        SetString(property.FindPropertyRelative("_description"), dto.DescriptionPt, $"{where}._description", report);
        SetString(property.FindPropertyRelative("_descriptionEn"), dto.DescriptionEn, $"{where}._descriptionEn", report);
        SetInt(property.FindPropertyRelative("_momentumCost"), dto.MomentumCost, $"{where}._momentumCost", report);

        var effects = dto.Effects ?? Array.Empty<BossImportFormat.ModifierDto>();
        var list = property.FindPropertyRelative("_effects");
        var differ = list.arraySize != effects.Length;
        for (var i = 0; i < effects.Length && differ == false; i++)
        {
            BossImportFormat.TryParseParameter(effects[i].Parameter, out var parameter);
            var element = list.GetArrayElementAtIndex(i);
            differ = element.FindPropertyRelative("Parameter").enumValueIndex != (int)parameter
                || Mathf.Approximately(element.FindPropertyRelative("Amount").floatValue, effects[i].Amount) == false;
        }
        if (differ == false)
            return;

        report.Changes.Add($"{where}._effects");
        list.arraySize = effects.Length;
        for (var i = 0; i < effects.Length; i++)
        {
            BossImportFormat.TryParseParameter(effects[i].Parameter, out var parameter);
            var element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Parameter").enumValueIndex = (int)parameter;
            element.FindPropertyRelative("Amount").floatValue = effects[i].Amount;
        }
    }

    private static KnowledgeEntryData FindEntry(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        var guid = AssetDatabase.FindAssets($"{name} t:KnowledgeEntryData", new[] { "Assets/Data" })
            .FirstOrDefault(candidate => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(candidate)) == name);
        return guid != null ? AssetDatabase.LoadAssetAtPath<KnowledgeEntryData>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }

    private static void ApplyLibrary(List<BossData> bosses, int expected, Report report, bool apply)
    {
        var library = AssetDatabase.LoadAssetAtPath<BossLibraryData>(LibraryPath);
        if (library == null)
        {
            report.Changes.Add("nova biblioteca de chefes");
            if (apply == false)
                return;
            library = ScriptableObject.CreateInstance<BossLibraryData>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        // Na previa os chefes novos ainda nao existem, entao a lista so e comparada quando esta completa.
        var so = new SerializedObject(library);
        var property = so.FindProperty("_bosses");
        var differ = bosses.Count != expected || property.arraySize != bosses.Count;
        for (var i = 0; i < bosses.Count && differ == false; i++)
            differ = property.GetArrayElementAtIndex(i).objectReferenceValue != bosses[i];
        if (differ == false)
            return;

        report.Changes.Add("biblioteca de chefes: lista");
        property.arraySize = bosses.Count;
        for (var i = 0; i < bosses.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = bosses[i];
        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(SerializedProperty property, string value, string where, Report report)
    {
        var wanted = value?.Trim() ?? string.Empty;
        if (property.stringValue == wanted)
            return;
        report.Changes.Add(where);
        property.stringValue = wanted;
    }

    private static void SetInt(SerializedProperty property, int value, string where, Report report)
    {
        if (property.intValue == value)
            return;
        report.Changes.Add(where);
        property.intValue = value;
    }

    private static void SetFloat(SerializedProperty property, float value, string where, Report report)
    {
        if (Mathf.Approximately(property.floatValue, value))
            return;
        report.Changes.Add(where);
        property.floatValue = value;
    }

    private static void SetEnum(SerializedProperty property, int value, string where, Report report)
    {
        if (property.enumValueIndex == value)
            return;
        report.Changes.Add(where);
        property.enumValueIndex = value;
    }
}
