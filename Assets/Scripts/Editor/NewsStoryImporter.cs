using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Cria ou atualiza as noticias do jornal a partir de um JSON e monta a biblioteca na ordem do arquivo.
// O arquivo e a fonte de verdade: noticia que saiu dele so e listada, pra ser apagada a mao.
public static class NewsStoryImporter
{
    public const string StoryFolder = "Assets/Data/News";
    public const string LibraryPath = StoryFolder + "/NewsLibrary.asset";

    [MenuItem("Tools/Metis/Educacao/Importar Jornal...")]
    private static void ImportFromMenu()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resources/Conteudo"));
        var path = EditorUtility.OpenFilePanel("Noticias do jornal", folder, "json");
        if (string.IsNullOrEmpty(path))
            return;

        var preview = Import(path, apply: false);
        if (preview.Errors.Count > 0)
        {
            EditorUtility.DisplayDialog("Importar jornal", $"{preview.Errors.Count} erros, nada foi mudado (lista no Console).", "OK");
            return;
        }
        if (EditorUtility.DisplayDialog("Importar jornal", $"{preview.Changes.Count} mudancas e {preview.Warnings.Count} avisos (lista no Console).\n\nAplicar nos assets?", "Aplicar", "Cancelar"))
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
        var file = JsonUtility.FromJson<NewsImportFormat.FileDto>(File.ReadAllText(jsonPath));

        report.Errors.AddRange(NewsImportFormat.Validate(file));
        foreach (var error in report.Errors)
            Debug.LogError($"[Jornal] {error}");
        if (report.Errors.Count > 0)
            return report;

        foreach (var missing in NewsImportFormat.MissingCoverage(file))
            report.Warnings.Add($"sem noticia para {missing}");

        if (apply && AssetDatabase.IsValidFolder(StoryFolder) == false)
            AssetDatabase.CreateFolder("Assets/Data", "News");

        var stories = new List<NewsStoryData>();
        foreach (var dto in file.Stories)
        {
            var path = $"{StoryFolder}/{dto.Id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<NewsStoryData>(path);
            if (asset == null)
            {
                report.Changes.Add($"nova noticia {dto.Id}");
                if (apply == false)
                    continue;
                asset = ScriptableObject.CreateInstance<NewsStoryData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            ApplyStory(dto, asset, report, apply);
            stories.Add(asset);
        }

        var ids = new HashSet<string>(file.Stories.Select(dto => dto.Id));
        foreach (var guid in AssetDatabase.FindAssets("t:NewsStoryData", new[] { StoryFolder }))
        {
            var orphan = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (ids.Contains(orphan) == false)
                report.Warnings.Add($"noticia {orphan} nao esta no arquivo e fica fora da biblioteca");
        }

        ApplyLibrary(stories, file.Stories.Length, report, apply);

        foreach (var warning in report.Warnings)
            Debug.LogWarning($"[Jornal] {warning}");
        foreach (var change in report.Changes)
            Debug.Log($"[Jornal] {(apply ? "" : "(previa) ")}{change}");
        if (apply)
            AssetDatabase.SaveAssets();
        return report;
    }

    private static void ApplyStory(NewsImportFormat.StoryDto dto, NewsStoryData asset, Report report, bool apply)
    {
        var so = new SerializedObject(asset);
        KnowledgeImportFormat.TryParse<NewsTrigger>(dto.Trigger, out var trigger);
        SetEnum(so, "_trigger", (int)trigger, dto.Id, report);
        var parameter = default(CityParameterType);
        if (NewsRules.UsesParameter(trigger))
            KnowledgeImportFormat.TryParse(dto.Parameter, out parameter);
        SetEnum(so, "_parameter", (int)parameter, dto.Id, report);
        SetString(so, "_headline", dto.HeadlinePt, dto.Id, report);
        SetString(so, "_headlineEn", dto.HeadlineEn, dto.Id, report);
        SetString(so, "_body", dto.BodyPt, dto.Id, report);
        SetString(so, "_bodyEn", dto.BodyEn, dto.Id, report);
        if (apply)
            so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyLibrary(List<NewsStoryData> stories, int expected, Report report, bool apply)
    {
        var library = AssetDatabase.LoadAssetAtPath<NewsLibraryData>(LibraryPath);
        if (library == null)
        {
            report.Changes.Add("nova biblioteca do jornal");
            if (apply == false)
                return;
            library = ScriptableObject.CreateInstance<NewsLibraryData>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        // Na previa as noticias novas ainda nao existem, entao a lista so e comparada quando esta completa.
        var so = new SerializedObject(library);
        var property = so.FindProperty("_stories");
        var differ = stories.Count != expected || property.arraySize != stories.Count;
        for (var i = 0; i < stories.Count && differ == false; i++)
            differ = property.GetArrayElementAtIndex(i).objectReferenceValue != stories[i];
        if (differ == false)
            return;

        report.Changes.Add("biblioteca do jornal: lista de noticias");
        property.arraySize = stories.Count;
        for (var i = 0; i < stories.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = stories[i];
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

    private static void SetEnum(SerializedObject so, string field, int value, string id, Report report)
    {
        var property = so.FindProperty(field);
        if (property.enumValueIndex == value)
            return;
        report.Changes.Add($"{id}.{field}");
        property.enumValueIndex = value;
    }
}
