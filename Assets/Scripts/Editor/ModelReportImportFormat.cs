using System;
using System.Collections.Generic;

// Validacao do arquivo do relatorio tecnico do modelo, sem dependencia da Unity pra poder ser testada fora do Editor.
public static class ModelReportImportFormat
{
    public const int CurrentVersion = 1;

    // Lista de erros; vazia quando o arquivo pode ser aplicado. knownEntryIds nulo pula a conferencia dos links.
    public static List<string> Validate(ReportFile file, ICollection<string> knownEntryIds)
    {
        var errors = new List<string>();
        if (file == null || file.Sheets == null || file.Sheets.Length == 0)
        {
            errors.Add("Arquivo sem abas.");
            return errors;
        }
        if (file.Version != CurrentVersion)
            errors.Add($"Versao {file.Version} desconhecida, esperado {CurrentVersion}.");
        if (string.IsNullOrWhiteSpace(file.Fingerprint))
            errors.Add("Arquivo sem a impressao dos numeros do modelo (Fingerprint).");
        if (string.IsNullOrWhiteSpace(file.ModelVersion))
            errors.Add("Arquivo sem a versao do modelo.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sheet in file.Sheets)
        {
            if (sheet == null || string.IsNullOrWhiteSpace(sheet.Id))
            {
                errors.Add("Aba sem Id.");
                continue;
            }
            var id = sheet.Id;
            if (ids.Add(id) == false)
                errors.Add($"Id de aba repetido: {id}.");
            if (IsValidId(id) == false)
                errors.Add($"{id}: Id so pode ter letras sem acento, numeros, '_' e '-'.");

            RequireBoth(errors, id, "Tab", sheet.Tab);
            RequireBoth(errors, id, "Title", sheet.Title);
            RequireBoth(errors, id, "Intro", sheet.Intro);

            if (string.IsNullOrEmpty(sheet.Auto) == false && Array.IndexOf(ModelReportLinks.AutoTables, sheet.Auto) < 0)
                errors.Add($"{id}: tabela automatica desconhecida '{sheet.Auto}'.");

            var header = sheet.Header ?? Array.Empty<ReportHeaderField>();
            for (var i = 0; i < header.Length; i++)
            {
                RequireBoth(errors, id, $"Header[{i}].Field", header[i]?.Field);
                RequireBoth(errors, id, $"Header[{i}].Value", header[i]?.Value);
            }

            var columns = sheet.Columns ?? Array.Empty<ReportText>();
            if (columns.Length == 0)
                errors.Add($"{id}: tabela sem colunas.");
            for (var i = 0; i < columns.Length; i++)
                RequireBoth(errors, id, $"Columns[{i}]", columns[i]);

            var rows = sheet.Rows ?? Array.Empty<ReportRow>();
            if (rows.Length == 0)
                errors.Add($"{id}: tabela sem linhas.");
            for (var r = 0; r < rows.Length; r++)
            {
                var cells = rows[r]?.Cells ?? Array.Empty<ReportText>();
                if (cells.Length > columns.Length)
                    errors.Add($"{id}: linha {r + 1} tem {cells.Length} celulas e a tabela {columns.Length} colunas.");
                for (var c = 0; c < cells.Length; c++)
                    CheckCell(errors, id, $"Rows[{r}][{c}]", cells[c], c == 0);
            }

            var notes = sheet.Notes ?? Array.Empty<ReportText>();
            for (var i = 0; i < notes.Length; i++)
                RequireBoth(errors, id, $"Notes[{i}]", notes[i]);

            foreach (var link in sheet.SeeAlso ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(link))
                    errors.Add($"{id}: link vazio.");
                else if (link != ModelReportLinks.RelationMap && knownEntryIds != null && knownEntryIds.Contains(link) == false)
                    errors.Add($"{id}: ficha desconhecida '{link}'.");
            }
        }
        return errors;
    }

    // Celula pode ficar vazia nos dois idiomas (matriz sem relacao), mas nao so num deles; a primeira coluna nunca.
    private static void CheckCell(List<string> errors, string id, string field, ReportText cell, bool required)
    {
        if (cell == null)
        {
            errors.Add($"{id}: {field} nulo.");
            return;
        }
        if (required)
            RequireBoth(errors, id, field, cell);
        else if (string.IsNullOrWhiteSpace(cell.Pt) != string.IsNullOrWhiteSpace(cell.En))
            errors.Add($"{id}: {field} so tem um idioma.");
        else
            CheckDashes(errors, id, field, cell);

        if (string.IsNullOrEmpty(cell.Tag) == false && Array.IndexOf(ModelReportLinks.Tags, cell.Tag) < 0)
            errors.Add($"{id}: {field} com marca desconhecida '{cell.Tag}'.");
    }

    private static void RequireBoth(List<string> errors, string id, string field, ReportText text)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.Pt))
            errors.Add($"{id}: {field} sem portugues.");
        if (text == null || string.IsNullOrWhiteSpace(text.En))
            errors.Add($"{id}: {field} sem ingles.");
        CheckDashes(errors, id, field, text);
    }

    private static void CheckDashes(List<string> errors, string id, string field, ReportText text)
    {
        if (text == null)
            return;
        foreach (var value in new[] { text.Pt, text.En })
        {
            if (value != null && (value.Contains("—") || value.Contains("–")))
                errors.Add($"{id}: {field} com travessao.");
        }
    }

    private static bool IsValidId(string id)
    {
        foreach (var c in id)
        {
            var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
            if (ok == false)
                return false;
        }
        return true;
    }
}
