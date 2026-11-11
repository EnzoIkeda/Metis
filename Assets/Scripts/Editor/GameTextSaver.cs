using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Grava um texto editado na fonte de verdade dele, validando antes de tocar em qualquer arquivo.
public static class GameTextSaver
{
    public const string FichasPath = "Resources/Conteudo/fichas.json";
    public const string NewsPath = "Resources/Conteudo/jornal.json";
    public const string RelationsPath = "Resources/Conteudo/mapa_relacoes.json";
    public const string ThemesPath = "Resources/Conteudo/temas.json";
    public const string DilemmasPath = "Resources/Conteudo/dilemas.json";
    public const string BackupFolder = "Resources/Conteudo/arquivo/backups_textos";

    public class Plan
    {
        public GameTextItem Item;
        public string Pt;
        public string En;
        public string Destination;
        public string NewContent;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool CanCommit => Errors.Count == 0;
    }

    // Nao altera nada: so valida e descobre o que a gravacao faria.
    public static Plan Prepare(GameTextItem item, string pt, string en)
    {
        var plan = new Plan { Item = item, Pt = GameTextSaveRules.Normalize(pt), En = GameTextSaveRules.Normalize(en) };
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            plan.Errors.Add("Saia do Play Mode e espere a compilação terminar.");
        plan.Errors.AddRange(GameTextSaveRules.Errors(item, plan.Pt, plan.En));
        if (plan.Errors.Count > 0)
            return plan;

        switch (item.EditKind)
        {
            case GameTextEditKind.Asset:
                plan.Destination = item.Location;
                break;
            case GameTextEditKind.News:
                PrepareNews(plan, "Stories");
                break;
            case GameTextEditKind.NewsColumn:
                PrepareNews(plan, "Columns");
                break;
            case GameTextEditKind.Knowledge:
                PrepareImported(plan, FichasPath, "Entries", path =>
                {
                    var report = KnowledgeEntryImporter.Import(path, apply: false);
                    return (report.Errors, report.Changes);
                });
                break;
            case GameTextEditKind.Theme:
                PrepareImported(plan, ThemesPath, "Themes", path =>
                {
                    var report = ThemedPhaseImporter.Import(path, apply: false);
                    return (report.Errors, report.Changes);
                });
                break;
            case GameTextEditKind.Dilemma:
                PrepareImported(plan, DilemmasPath, item.JsonListKey, path =>
                {
                    var report = DilemmaImporter.Import(path, apply: false);
                    return (report.Errors, report.Changes);
                });
                break;
            case GameTextEditKind.RelationNote:
                PrepareRelation(plan);
                break;
            case GameTextEditKind.Interface:
                PrepareInterface(plan);
                break;
        }

        var preview = new GameTextItem
        {
            Origin = item.Origin,
            Pt = plan.Pt,
            En = plan.En,
            IsPaired = item.IsPaired,
            UsesTitleFont = item.UsesTitleFont,
        };
        foreach (var issue in GameTextRules.Check(preview))
            plan.Warnings.Add("Verificação: " + issue);
        return plan;
    }

    public static bool Commit(Plan plan, out string error)
    {
        error = null;
        if (plan.CanCommit == false)
        {
            error = string.Join("\n", plan.Errors);
            return false;
        }

        var item = plan.Item;
        switch (item.EditKind)
        {
            case GameTextEditKind.Asset:
                ApplyToAsset(item.Location, item.PtField, item.EnField, plan.Pt, plan.En);
                return true;
            case GameTextEditKind.News:
            case GameTextEditKind.NewsColumn:
                WriteJson(NewsPath, plan.NewContent);
                return Reported(NewsStoryImporter.Import(FullPath(NewsPath), apply: true).Errors, out error);
            case GameTextEditKind.Knowledge:
                WriteJson(FichasPath, plan.NewContent);
                return Reported(KnowledgeEntryImporter.Import(FullPath(FichasPath), apply: true).Errors, out error);
            case GameTextEditKind.Theme:
                WriteJson(ThemesPath, plan.NewContent);
                return Reported(ThemedPhaseImporter.Import(FullPath(ThemesPath), apply: true).Errors, out error);
            case GameTextEditKind.Dilemma:
                WriteJson(DilemmasPath, plan.NewContent);
                return Reported(DilemmaImporter.Import(FullPath(DilemmasPath), apply: true).Errors, out error);
            case GameTextEditKind.RelationNote:
                WriteJson(RelationsPath, plan.NewContent);
                ApplyToNote(item, plan.Pt, plan.En);
                return true;
            case GameTextEditKind.Interface:
                File.WriteAllText(item.Location, plan.NewContent, EncodingOf(item.Location));
                AssetDatabase.ImportAsset(item.Location);
                return true;
            default:
                error = "Este texto não pode ser editado aqui.";
                return false;
        }
    }

    public static void ApplyToAsset(string path, string ptField, string enField, string pt, string en)
    {
        var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
        so.FindProperty(ptField).stringValue = pt;
        if (string.IsNullOrEmpty(enField) == false)
            so.FindProperty(enField).stringValue = en;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static bool Reported(List<string> errors, out string error)
    {
        error = errors.Count > 0 ? string.Join("\n", errors) : null;
        return errors.Count == 0;
    }

    private static void PrepareNews(Plan plan, string listKey)
    {
        PrepareImported(plan, NewsPath, listKey, path =>
        {
            var report = NewsStoryImporter.Import(path, apply: false);
            return (report.Errors, report.Changes);
        });
    }

    private static void PrepareImported(Plan plan, string relativePath, string listKey, Func<string, (List<string> Errors, List<string> Changes)> preview)
    {
        var item = plan.Item;
        var changes = new[]
        {
            new ContentJsonEditor.Change { Key = item.JsonPtKey, Expected = item.Pt, Value = plan.Pt },
            new ContentJsonEditor.Change { Key = item.JsonEnKey, Expected = item.En, Value = plan.En },
        };
        if (TryReadSource(plan, relativePath, out var json) == false)
            return;
        var replaced = string.IsNullOrEmpty(item.JsonNestedKey)
            ? ContentJsonEditor.TryReplaceInObject(json, listKey, item.Id, changes, out var edited, out var error)
            : ContentJsonEditor.TryReplaceInNested(json, listKey, item.Id, item.JsonNestedKey, item.ArrayIndex, changes, out edited, out error);
        if (replaced == false)
        {
            plan.Errors.Add(error);
            return;
        }

        // A importacao valida uma copia temporaria; o arquivo real so muda na gravacao.
        var temp = FileUtil.GetUniqueTempPathInProject() + ".json";
        File.WriteAllText(temp, edited);
        try
        {
            var report = preview(temp);
            plan.Errors.AddRange(report.Errors);
            var expected = (GameTextSaveRules.Normalize(item.Pt) != plan.Pt ? 1 : 0) + (GameTextSaveRules.Normalize(item.En) != plan.En ? 1 : 0);
            if (report.Changes.Count > expected)
                plan.Warnings.Add($"A importação também mudaria outros campos ({report.Changes.Count} no total): " + string.Join("; ", report.Changes.Take(6)));
        }
        finally
        {
            File.Delete(temp);
        }
        plan.NewContent = edited;
        plan.Destination = $"{relativePath}, depois os assets pela importação";
    }

    private static void PrepareRelation(Plan plan)
    {
        var item = plan.Item;
        if (TryReadSource(plan, RelationsPath, out var json) == false)
            return;
        var changes = new[]
        {
            new ContentJsonEditor.Change { Cell = 2, Expected = item.Pt, Value = plan.Pt },
            new ContentJsonEditor.Change { Cell = 3, Expected = item.En, Value = plan.En },
        };
        if (ContentJsonEditor.TryReplaceInRow(json, item.NoteSource, item.NoteTarget, changes, out var edited, out var error) == false)
        {
            plan.Errors.Add(error);
            return;
        }
        plan.NewContent = edited;
        plan.Destination = $"{RelationsPath} e {item.Location}";
    }

    private static void PrepareInterface(Plan plan)
    {
        var item = plan.Item;
        if (InterfaceTextEditor.TryEdit(File.ReadAllText(item.Location), item.Location, item, plan.Pt, plan.En, out var edited, out var error) == false)
        {
            plan.Errors.Add(error);
            return;
        }
        plan.NewContent = edited;
        plan.Destination = $"{item.Location}; a Unity vai recompilar";
    }

    // Mantem a marca de ordem de bytes que o arquivo ja tinha, para o diff mostrar so o texto.
    private static System.Text.Encoding EncodingOf(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasMark = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return new System.Text.UTF8Encoding(hasMark);
    }

    private static bool TryReadSource(Plan plan, string relativePath, out string json)
    {
        json = null;
        var path = FullPath(relativePath);
        if (File.Exists(path) == false)
        {
            plan.Errors.Add($"Fonte não encontrada: {relativePath}. Sem ela a edição se perderia na próxima importação.");
            return false;
        }
        json = File.ReadAllText(path);
        return true;
    }

    private static void ApplyToNote(GameTextItem item, string pt, string en)
    {
        var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(item.Location));
        var element = so.FindProperty("_notes").GetArrayElementAtIndex(item.ArrayIndex);
        element.FindPropertyRelative(item.PtField).stringValue = pt;
        element.FindPropertyRelative(item.EnField).stringValue = en;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    // O arquivo fica fora do Git: guarda uma copia por dia antes da primeira mudanca.
    private static void WriteJson(string relativePath, string json)
    {
        var path = FullPath(relativePath);
        var folder = FullPath(BackupFolder);
        Directory.CreateDirectory(folder);
        var backup = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(path)}_{DateTime.Now:yyyy-MM-dd}.json");
        if (File.Exists(backup) == false)
            File.Copy(path, backup);

        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, path, true);
        File.Delete(temp);
    }

    private static string FullPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }
}

// Marcas de revisao em arquivo, junto dos demais materiais de revisao.
public static class GameTextReviewStore
{
    public const string RelativePath = "Resources/Conteudo/revisao_textos.json";

    private static string FullPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelativePath));

    public static GameTextReview Load()
    {
        return File.Exists(FullPath)
            ? new GameTextReview(JsonUtility.FromJson<GameTextReviewFile>(File.ReadAllText(FullPath)))
            : new GameTextReview();
    }

    public static void Save(GameTextReview review)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath));
        var temp = FullPath + ".tmp";
        File.WriteAllText(temp, JsonUtility.ToJson(review.ToFile(), true));
        File.Copy(temp, FullPath, true);
        File.Delete(temp);
    }
}
