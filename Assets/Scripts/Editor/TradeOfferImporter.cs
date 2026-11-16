using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Cria ou atualiza as ofertas de troca a partir de um JSON e monta a biblioteca na ordem do arquivo.
public static class TradeOfferImporter
{
    public const string Folder = "Assets/Data/Trades";
    public const string LibraryPath = Folder + "/TradeOfferLibrary.asset";

    [MenuItem("Tools/Metis/Educacao/Importar Negociacao...")]
    private static void ImportFromMenu()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resources/Conteudo"));
        var path = EditorUtility.OpenFilePanel("Ofertas de troca", folder, "json");
        if (string.IsNullOrEmpty(path))
            return;

        var preview = Import(path, apply: false);
        if (preview.Errors.Count > 0)
        {
            EditorUtility.DisplayDialog("Importar negociacao", $"{preview.Errors.Count} erros, nada foi mudado (lista no Console).", "OK");
            return;
        }
        if (EditorUtility.DisplayDialog("Importar negociacao", $"{preview.Changes.Count} mudancas e {preview.Warnings.Count} avisos (lista no Console).\n\nAplicar nos assets?", "Aplicar", "Cancelar"))
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
        var file = JsonUtility.FromJson<TradeOfferImportFormat.FileDto>(File.ReadAllText(jsonPath));
        report.Errors.AddRange(TradeOfferImportFormat.Validate(file, AssetNames("t:KnowledgeEntryData"), ExtraOnlyCardNames()));
        foreach (var error in report.Errors)
            Debug.LogError($"[Negociacao] {error}");
        if (report.Errors.Count > 0)
            return report;

        report.Warnings.AddRange(TradeOfferImportFormat.Warnings(file));
        if (apply && AssetDatabase.IsValidFolder(Folder) == false)
            AssetDatabase.CreateFolder("Assets/Data", "Trades");

        var offers = new List<TradeOfferData>();
        foreach (var dto in file.Offers)
        {
            var path = $"{Folder}/{dto.Id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TradeOfferData>(path);
            if (asset == null)
            {
                report.Changes.Add($"nova oferta {dto.Id}");
                if (apply == false)
                    continue;
                asset = ScriptableObject.CreateInstance<TradeOfferData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            Apply(dto, asset, report, apply);
            offers.Add(asset);
        }

        var ids = new HashSet<string>(file.Offers.Select(dto => dto.Id));
        foreach (var guid in AssetDatabase.FindAssets("t:TradeOfferData", new[] { Folder }))
        {
            var orphan = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (ids.Contains(orphan) == false)
                report.Warnings.Add($"oferta {orphan} nao esta no arquivo e fica fora da biblioteca");
        }

        ApplyLibrary(offers, file.Offers.Length, report, apply);
        foreach (var warning in report.Warnings)
            Debug.LogWarning($"[Negociacao] {warning}");
        foreach (var change in report.Changes)
            Debug.Log($"[Negociacao] {(apply ? "" : "(previa) ")}{change}");
        if (apply)
            AssetDatabase.SaveAssets();
        return report;
    }

    private static HashSet<string> ExtraOnlyCardNames()
    {
        return new HashSet<string>(AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(card => card != null && card.IsExtraOnly).Select(card => card.name));
    }

    private static HashSet<string> AssetNames(string filter)
    {
        return new HashSet<string>(AssetDatabase.FindAssets(filter, new[] { "Assets/Data" })
            .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid))));
    }

    private static void Apply(TradeOfferImportFormat.OfferDto dto, TradeOfferData asset, Report report, bool apply)
    {
        var so = new SerializedObject(asset);
        SetString(so.FindProperty("_offerName"), dto.NamePt, $"{dto.Id}._offerName", report);
        SetString(so.FindProperty("_offerNameEn"), dto.NameEn, $"{dto.Id}._offerNameEn", report);
        SetString(so.FindProperty("_summary"), dto.SummaryPt, $"{dto.Id}._summary", report);
        SetString(so.FindProperty("_summaryEn"), dto.SummaryEn, $"{dto.Id}._summaryEn", report);

        var entry = FindEntry(dto.KnowledgeEntry);
        var entryProperty = so.FindProperty("_knowledgeEntry");
        if (entryProperty.objectReferenceValue != entry)
        {
            report.Changes.Add($"{dto.Id}._knowledgeEntry");
            entryProperty.objectReferenceValue = entry;
        }

        SetTerm(so.FindProperty("_benefit"), dto.Benefit, $"{dto.Id}._benefit", report);
        SetTerm(so.FindProperty("_drawback"), dto.Drawback, $"{dto.Id}._drawback", report);
        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetTerm(SerializedProperty property, TradeOfferImportFormat.TermDto dto, string where, Report report)
    {
        TradeOfferImportFormat.TryParseKind(dto.Kind, out var kind);
        var kindProperty = property.FindPropertyRelative("Kind");
        if (kindProperty.enumValueIndex != (int)kind)
        {
            report.Changes.Add($"{where}.Kind");
            kindProperty.enumValueIndex = (int)kind;
        }

        var amount = property.FindPropertyRelative("Amount");
        if (amount.intValue != dto.Amount)
        {
            report.Changes.Add($"{where}.Amount");
            amount.intValue = dto.Amount;
        }
        SetString(property.FindPropertyRelative("CardId"), dto.CardId, $"{where}.CardId", report);

        var effects = dto.Effects ?? Array.Empty<TradeOfferImportFormat.ModifierDto>();
        var list = property.FindPropertyRelative("Effects");
        var differ = list.arraySize != effects.Length;
        for (var i = 0; i < effects.Length && differ == false; i++)
        {
            TradeOfferImportFormat.TryParseParameter(effects[i].Parameter, out var parameter);
            var element = list.GetArrayElementAtIndex(i);
            differ = element.FindPropertyRelative("Parameter").enumValueIndex != (int)parameter
                || Mathf.Approximately(element.FindPropertyRelative("Amount").floatValue, effects[i].Amount) == false;
        }
        if (differ == false)
            return;

        report.Changes.Add($"{where}.Effects");
        list.arraySize = effects.Length;
        for (var i = 0; i < effects.Length; i++)
        {
            TradeOfferImportFormat.TryParseParameter(effects[i].Parameter, out var parameter);
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

    private static void ApplyLibrary(List<TradeOfferData> offers, int expected, Report report, bool apply)
    {
        var library = AssetDatabase.LoadAssetAtPath<TradeOfferLibraryData>(LibraryPath);
        if (library == null)
        {
            report.Changes.Add("nova biblioteca de ofertas");
            if (apply == false)
                return;
            library = ScriptableObject.CreateInstance<TradeOfferLibraryData>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        // Na previa as ofertas novas ainda nao existem, entao a lista so e comparada quando esta completa.
        var so = new SerializedObject(library);
        var property = so.FindProperty("_offers");
        var differ = offers.Count != expected || property.arraySize != offers.Count;
        for (var i = 0; i < offers.Count && differ == false; i++)
            differ = property.GetArrayElementAtIndex(i).objectReferenceValue != offers[i];
        if (differ == false)
            return;

        report.Changes.Add("biblioteca de ofertas: lista");
        property.arraySize = offers.Count;
        for (var i = 0; i < offers.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = offers[i];
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
}
