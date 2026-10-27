using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Cria ou atualiza o relatorio tecnico do modelo a partir do JSON gerado fora do Unity, que e a fonte de verdade.
public static class ModelReportImporter
{
    public const string ReportFolder = "Assets/Data/Report";
    public const string ReportPath = ReportFolder + "/ModelReport.asset";

    [MenuItem("Tools/Metis/Educacao/Importar Relatorio do Modelo...")]
    private static void ImportFromMenu()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resources/Conteudo"));
        var path = EditorUtility.OpenFilePanel("Relatorio tecnico do modelo", folder, "json");
        if (string.IsNullOrEmpty(path))
            return;

        var preview = Import(path, apply: false);
        if (preview.Errors.Count > 0)
        {
            EditorUtility.DisplayDialog("Importar relatorio", $"{preview.Errors.Count} erros, nada foi mudado (lista no Console).", "OK");
            return;
        }
        if (EditorUtility.DisplayDialog("Importar relatorio", $"{preview.Changes.Count} mudancas e {preview.Warnings.Count} avisos (lista no Console).\n\nAplicar no asset?", "Aplicar", "Cancelar"))
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
        var file = JsonUtility.FromJson<ReportFile>(File.ReadAllText(jsonPath));

        var library = AssetDatabase.LoadAssetAtPath<KnowledgeLibraryData>(KnowledgeEntryImporter.LibraryPath);
        var known = library != null ? new HashSet<string>(library.Entries.Where(e => e != null).Select(e => e.Id)) : null;
        report.Errors.AddRange(ModelReportImportFormat.Validate(file, known));
        foreach (var error in report.Errors)
            Debug.LogError($"[Relatorio] {error}");
        if (report.Errors.Count > 0)
            return report;

        if (file.Fingerprint != ModelReportLiveData.Fingerprint())
            report.Warnings.Add("os numeros do arquivo nao batem com os do jogo: gerar de novo com o export atual");

        var asset = AssetDatabase.LoadAssetAtPath<ModelReportData>(ReportPath);
        if (asset == null)
        {
            report.Changes.Add("novo relatorio do modelo");
        }
        else
        {
            CompareSheets(asset.File, file, report);
            if (asset.Fingerprint != file.Fingerprint)
                report.Changes.Add("numeros do modelo");
            if (asset.ModelVersion != file.ModelVersion)
                report.Changes.Add($"versao do modelo {asset.ModelVersion} para {file.ModelVersion}");
        }

        foreach (var warning in report.Warnings)
            Debug.LogWarning($"[Relatorio] {warning}");
        foreach (var change in report.Changes)
            Debug.Log($"[Relatorio] {(apply ? "" : "(previa) ")}{change}");

        if (apply == false || report.Changes.Count == 0)
            return report;

        if (AssetDatabase.IsValidFolder(ReportFolder) == false)
            AssetDatabase.CreateFolder("Assets/Data", "Report");
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<ModelReportData>();
            asset.EditorReplace(file);
            AssetDatabase.CreateAsset(asset, ReportPath);
        }
        else
        {
            asset.EditorReplace(file);
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
        return report;
    }

    private static void CompareSheets(ReportFile current, ReportFile wanted, Report report)
    {
        var before = (current?.Sheets ?? new ReportSheet[0]).Where(s => s != null).ToDictionary(s => s.Id, s => JsonUtility.ToJson(s));
        var order = (current?.Sheets ?? new ReportSheet[0]).Where(s => s != null).Select(s => s.Id).ToList();
        foreach (var sheet in wanted.Sheets)
        {
            if (before.TryGetValue(sheet.Id, out var json) == false)
                report.Changes.Add($"nova aba {sheet.Id}");
            else if (json != JsonUtility.ToJson(sheet))
                report.Changes.Add($"aba {sheet.Id}");
        }
        foreach (var id in before.Keys.Except(wanted.Sheets.Select(s => s.Id)))
            report.Changes.Add($"aba {id} removida");
        if (order.SequenceEqual(wanted.Sheets.Select(s => s.Id)) == false && order.Count == wanted.Sheets.Length)
            report.Changes.Add("ordem das abas");
    }
}
