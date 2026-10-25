using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Cria ou atualiza as fichas "Saiba mais" a partir de um JSON, monta o almanaque na ordem do arquivo e
// liga cada carta e evento a sua ficha. O arquivo e a fonte de verdade: carta ou evento fora dele fica sem ficha.
public static class KnowledgeEntryImporter
{
    public const string EntryFolder = "Assets/Data/Knowledge";
    public const string LibraryPath = EntryFolder + "/KnowledgeLibrary.asset";
    public const string ParameterDisplayPath = "Assets/Data/UI/ParameterDisplay.asset";

    [MenuItem("Tools/Metis/Educacao/Importar Fichas...")]
    private static void ImportFromMenu()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resources/Conteudo"));
        var path = EditorUtility.OpenFilePanel("Fichas Saiba mais", folder, "json");
        if (string.IsNullOrEmpty(path))
            return;

        var preview = Import(path, apply: false);
        if (preview.Errors.Count > 0)
        {
            EditorUtility.DisplayDialog("Importar fichas", $"{preview.Errors.Count} erros, nada foi mudado (lista no Console).", "OK");
            return;
        }
        if (EditorUtility.DisplayDialog("Importar fichas", $"{preview.Changes.Count} mudancas (lista no Console).\n\nAplicar nos assets?", "Aplicar", "Cancelar"))
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
        var file = JsonUtility.FromJson<KnowledgeImportFormat.FileDto>(File.ReadAllText(jsonPath));
        var cards = LoadAll<CardData>();
        var events = LoadAll<RandomEventData>();

        report.Errors.AddRange(KnowledgeImportFormat.Validate(file, cards.Keys, events.Keys));
        foreach (var error in report.Errors)
            Debug.LogError($"[Fichas] {error}");
        if (report.Errors.Count > 0)
            return report;

        if (apply && AssetDatabase.IsValidFolder(EntryFolder) == false)
            AssetDatabase.CreateFolder("Assets/Data", "Knowledge");

        // Primeiro garante que toda ficha existe, pra as relacionadas poderem apontar umas pras outras.
        var entries = new Dictionary<string, KnowledgeEntryData>();
        foreach (var dto in file.Entries)
        {
            var path = $"{EntryFolder}/{dto.Id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<KnowledgeEntryData>(path);
            if (asset == null)
            {
                report.Changes.Add($"nova ficha {dto.Id}");
                if (apply)
                {
                    asset = ScriptableObject.CreateInstance<KnowledgeEntryData>();
                    AssetDatabase.CreateAsset(asset, path);
                }
            }
            entries[dto.Id] = asset;
        }

        var display = AssetDatabase.LoadAssetAtPath<ParameterDisplayData>(ParameterDisplayPath);
        foreach (var dto in file.Entries)
            ApplyEntry(dto, entries, ImageFor(dto, cards, display), report, apply);

        ApplyLibrary(file, entries, report, apply);
        ApplyLinks(cards.Values, file.Entries.ToDictionary(e => e.Id, e => e.Cards ?? Array.Empty<string>()), entries, report, apply);
        ApplyLinks(events.Values, file.Entries.ToDictionary(e => e.Id, e => e.Events ?? Array.Empty<string>()), entries, report, apply);

        foreach (var change in report.Changes)
            Debug.Log($"[Fichas] {(apply ? "" : "(previa) ")}{change}");
        if (apply)
            AssetDatabase.SaveAssets();
        return report;
    }

    // Ficha de carta usa a arte da primeira carta ligada que tiver arte; ficha de parametro, o icone do painel.
    private static Sprite ImageFor(KnowledgeImportFormat.EntryDto dto, Dictionary<string, CardData> cards, ParameterDisplayData display)
    {
        KnowledgeImportFormat.TryParse<KnowledgeCategory>(dto.Category, out var category);
        if (category == KnowledgeCategory.Parameter && display != null && KnowledgeImportFormat.TryParse<CityParameterType>(dto.Parameter, out var parameter))
            return display.GetIcon(parameter);
        if (category == KnowledgeCategory.Card)
        {
            foreach (var id in dto.Cards ?? Array.Empty<string>())
            {
                if (cards.TryGetValue(id, out var card) && card.Artwork != null)
                    return card.Artwork;
            }
        }
        return null;
    }

    private static void ApplyEntry(KnowledgeImportFormat.EntryDto dto, Dictionary<string, KnowledgeEntryData> entries, Sprite image, Report report, bool apply)
    {
        var asset = entries[dto.Id];
        if (asset == null)
            return;

        var so = new SerializedObject(asset);
        SetString(so, "_title", dto.TitlePt, dto.Id, report);
        SetString(so, "_titleEn", dto.TitleEn, dto.Id, report);
        KnowledgeImportFormat.TryParse<KnowledgeCategory>(dto.Category, out var category);
        SetEnum(so, "_category", (int)category, dto.Id, report);
        KnowledgeImportFormat.TryParse<KnowledgeUnlockTrigger>(string.IsNullOrEmpty(dto.Trigger) ? "None" : dto.Trigger, out var trigger);
        SetEnum(so, "_unlockTrigger", (int)trigger, dto.Id, report);
        SetString(so, "_summary", dto.SummaryPt, dto.Id, report);
        SetString(so, "_summaryEn", dto.SummaryEn, dto.Id, report);
        SetString(so, "_realWorld", dto.RealWorldPt, dto.Id, report);
        SetString(so, "_realWorldEn", dto.RealWorldEn, dto.Id, report);
        SetString(so, "_tradeOffs", dto.TradeOffsPt, dto.Id, report);
        SetString(so, "_tradeOffsEn", dto.TradeOffsEn, dto.Id, report);

        var sources = so.FindProperty("_sources");
        var wantedSources = dto.Sources ?? Array.Empty<KnowledgeImportFormat.SourceDto>();
        var sourcesDiffer = sources.arraySize != wantedSources.Length;
        for (var i = 0; i < wantedSources.Length && sourcesDiffer == false; i++)
        {
            var element = sources.GetArrayElementAtIndex(i);
            sourcesDiffer = element.FindPropertyRelative("Label").stringValue != wantedSources[i].Label.Trim()
                || element.FindPropertyRelative("Url").stringValue != wantedSources[i].Url.Trim();
        }
        if (sourcesDiffer)
        {
            report.Changes.Add($"{dto.Id}: fontes ({wantedSources.Length})");
            sources.arraySize = wantedSources.Length;
            for (var i = 0; i < wantedSources.Length; i++)
            {
                var element = sources.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Label").stringValue = wantedSources[i].Label.Trim();
                element.FindPropertyRelative("Url").stringValue = wantedSources[i].Url.Trim();
            }
        }

        var imageProperty = so.FindProperty("_image");
        if (imageProperty.objectReferenceValue != image)
        {
            report.Changes.Add($"{dto.Id}._image");
            imageProperty.objectReferenceValue = image;
        }

        var related = (dto.Related ?? Array.Empty<string>()).Select(id => entries[id]).ToArray();
        SetObjects(so, "_relatedEntries", related, $"{dto.Id}: relacionadas", report);

        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyLibrary(KnowledgeImportFormat.FileDto file, Dictionary<string, KnowledgeEntryData> entries, Report report, bool apply)
    {
        var library = AssetDatabase.LoadAssetAtPath<KnowledgeLibraryData>(LibraryPath);
        if (library == null)
        {
            report.Changes.Add("novo almanaque");
            if (apply == false)
                return;
            library = ScriptableObject.CreateInstance<KnowledgeLibraryData>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        var so = new SerializedObject(library);
        SetObjects(so, "_entries", file.Entries.Select(dto => entries[dto.Id]).ToArray(), "almanaque: lista de fichas", report);
        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyLinks<T>(IEnumerable<T> assets, Dictionary<string, string[]> linksByEntry, Dictionary<string, KnowledgeEntryData> entries, Report report, bool apply) where T : ScriptableObject
    {
        var owner = new Dictionary<string, KnowledgeEntryData>();
        foreach (var pair in linksByEntry)
        {
            foreach (var id in pair.Value)
                owner[id] = entries[pair.Key];
        }

        foreach (var asset in assets)
        {
            // Na previa a ficha nova ainda nao existe: tem dono, mas o asset e nulo.
            var hasOwner = owner.TryGetValue(asset.name, out var wanted);
            var so = new SerializedObject(asset);
            var property = so.FindProperty("_knowledgeEntry");
            var current = property.objectReferenceValue;
            var differs = hasOwner ? wanted == null || current != wanted : current != null;
            if (differs == false)
                continue;
            report.Changes.Add($"{asset.name}: ficha {(hasOwner ? (wanted != null ? wanted.name : "(nova)") : "(nenhuma)")}");
            property.objectReferenceValue = wanted;
            if (apply)
                so.ApplyModifiedPropertiesWithoutUndo();
        }
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

    private static void SetEnum(SerializedObject so, string field, int value, string id, Report report)
    {
        var property = so.FindProperty(field);
        if (property.enumValueIndex == value)
            return;
        report.Changes.Add($"{id}.{field}");
        property.enumValueIndex = value;
    }

    private static void SetObjects(SerializedObject so, string field, UnityEngine.Object[] wanted, string label, Report report)
    {
        var property = so.FindProperty(field);
        var differ = property.arraySize != wanted.Length;
        for (var i = 0; i < wanted.Length && differ == false; i++)
            differ = property.GetArrayElementAtIndex(i).objectReferenceValue != wanted[i];
        if (differ == false)
            return;

        report.Changes.Add(label);
        property.arraySize = wanted.Length;
        for (var i = 0; i < wanted.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = wanted[i];
    }

    private static Dictionary<string, T> LoadAll<T>() where T : ScriptableObject
    {
        var result = new Dictionary<string, T>();
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
                result[asset.name] = asset;
        }
        return result;
    }
}
