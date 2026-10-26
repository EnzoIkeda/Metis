using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Formato do arquivo de noticias do jornal e a validacao dele, sem dependencia da Unity pra poder
// ser testado fora do Editor.
public static class NewsImportFormat
{
    // Marcadores que o jornal troca pelos numeros do turno.
    public static readonly string[] Placeholders = { "{valor}", "{variacao}", "{turno}", "{parametro}" };

    private static readonly Regex PlaceholderPattern = new Regex(@"\{[^{}]*\}");

    [Serializable]
    public class FileDto
    {
        public StoryDto[] Stories = Array.Empty<StoryDto>();
    }

    [Serializable]
    public class StoryDto
    {
        public string Id;
        public string Trigger;

        // Nome do enum; vazio nos gatilhos sem parametro.
        public string Parameter;
        public string HeadlinePt;
        public string HeadlineEn;
        public string BodyPt;
        public string BodyEn;
    }

    // Lista de erros; vazia quando o arquivo pode ser aplicado.
    public static List<string> Validate(FileDto file)
    {
        var errors = new List<string>();
        if (file == null || file.Stories == null || file.Stories.Length == 0)
        {
            errors.Add("Arquivo sem noticias.");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var story in file.Stories)
        {
            if (story == null || string.IsNullOrWhiteSpace(story.Id))
            {
                errors.Add("Noticia sem Id.");
                continue;
            }
            var id = story.Id;
            if (ids.Add(id) == false)
                errors.Add($"Id repetido: {id}.");
            if (IsValidId(id) == false)
                errors.Add($"{id}: Id so pode ter letras sem acento, numeros, '_' e '-'.");

            if (KnowledgeImportFormat.TryParse<NewsTrigger>(story.Trigger, out var trigger) == false)
            {
                errors.Add($"{id}: gatilho desconhecido '{story.Trigger}'.");
            }
            else if (NewsRules.UsesParameter(trigger))
            {
                if (KnowledgeImportFormat.TryParse<CityParameterType>(story.Parameter, out var parameter) == false)
                    errors.Add($"{id}: parametro desconhecido '{story.Parameter}'.");
                else if (NewsRules.Applies(trigger, parameter) == false)
                    errors.Add($"{id}: {trigger} nunca acontece com {parameter}.");
            }
            else if (string.IsNullOrEmpty(story.Parameter) == false)
            {
                errors.Add($"{id}: {trigger} nao usa parametro.");
            }

            RequireText(errors, id, "HeadlinePt", story.HeadlinePt);
            RequireText(errors, id, "HeadlineEn", story.HeadlineEn);
            RequireText(errors, id, "BodyPt", story.BodyPt);
            RequireText(errors, id, "BodyEn", story.BodyEn);
            foreach (var text in new[] { story.HeadlinePt, story.HeadlineEn, story.BodyPt, story.BodyEn })
                CheckText(errors, id, text);
        }
        return errors;
    }

    // Combinacoes de gatilho e parametro sem nenhuma noticia, que sairiam sem manchete.
    public static List<string> MissingCoverage(FileDto file)
    {
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var story in file?.Stories ?? Array.Empty<StoryDto>())
        {
            if (story == null || KnowledgeImportFormat.TryParse<NewsTrigger>(story.Trigger, out var trigger) == false)
                continue;
            covered.Add(NewsRules.UsesParameter(trigger) ? $"{trigger}/{story.Parameter}" : trigger.ToString());
        }

        var missing = new List<string>();
        foreach (NewsTrigger trigger in Enum.GetValues(typeof(NewsTrigger)))
        {
            if (NewsRules.UsesParameter(trigger) == false)
            {
                if (covered.Contains(trigger.ToString()) == false)
                    missing.Add(trigger.ToString());
                continue;
            }
            foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
            {
                if (NewsRules.Applies(trigger, parameter) && covered.Contains($"{trigger}/{parameter}") == false)
                    missing.Add($"{trigger}/{parameter}");
            }
        }
        return missing;
    }

    private static void RequireText(List<string> errors, string id, string field, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            errors.Add($"{id}: {field} vazio.");
    }

    private static void CheckText(List<string> errors, string id, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (text.Contains("—") || text.Contains("–"))
            errors.Add($"{id}: texto com travessao.");
        foreach (Match match in PlaceholderPattern.Matches(text))
        {
            if (Array.IndexOf(Placeholders, match.Value) < 0)
                errors.Add($"{id}: marcador desconhecido '{match.Value}'.");
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
