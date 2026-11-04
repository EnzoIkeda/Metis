using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Formato do arquivo de noticias do jornal e a validacao dele, sem dependencia da Unity pra poder
// ser testado fora do Editor.
public static class NewsImportFormat
{
    // Marcadores que o jornal troca pelos numeros do turno.
    public static readonly string[] Placeholders = { "{valor}", "{variacao}", "{turno}", "{parametro}" };

    public const int MaxHeadlineLength = 65;
    public const int MaxBodyLength = 145;
    public const int MaxColumnLength = 110;

    private static readonly Regex PlaceholderPattern = new Regex(@"\{[^{}]*\}");

    [Serializable]
    public class FileDto
    {
        public StoryDto[] Stories = Array.Empty<StoryDto>();
        public ColumnDto[] Columns = Array.Empty<ColumnDto>();
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

        // Carta ou evento citado, pelo nome do asset.
        public string Target;
    }

    [Serializable]
    public class ColumnDto
    {
        public string Id;
        public string Kind;

        // So nas falas.
        public string Citizen;

        // Nas falas, no horoscopo e na previsao; os classificados nao tem faixa.
        public string Mood;
        public string TextPt;
        public string TextEn;
    }

    // Lista de erros; vazia quando o arquivo pode ser aplicado.
    public static List<string> Validate(FileDto file, ICollection<string> knownCards = null, ICollection<string> knownEvents = null)
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
            CheckLength(errors, id, "manchete", story.HeadlinePt, MaxHeadlineLength);
            CheckLength(errors, id, "manchete", story.HeadlineEn, MaxHeadlineLength);
            CheckLength(errors, id, "corpo", story.BodyPt, MaxBodyLength);
            CheckLength(errors, id, "corpo", story.BodyEn, MaxBodyLength);
            if (KnowledgeImportFormat.TryParse<NewsTrigger>(story.Trigger, out var parsedTrigger))
                CheckTarget(errors, id, parsedTrigger, story.Target, knownCards, knownEvents);
        }
        ValidateColumns(errors, file.Columns, ids);
        return errors;
    }

    private static void CheckTarget(List<string> errors, string id, NewsTrigger trigger, string target, ICollection<string> knownCards, ICollection<string> knownEvents)
    {
        if (NewsRules.UsesTarget(trigger) == false)
        {
            if (string.IsNullOrEmpty(target) == false)
                errors.Add($"{id}: {trigger} nao cita carta nem evento.");
            return;
        }
        if (string.IsNullOrEmpty(target))
        {
            errors.Add($"{id}: {trigger} precisa citar uma carta ou evento.");
            return;
        }
        var known = trigger == NewsTrigger.CardPlayed ? knownCards : knownEvents;
        if (known != null && known.Contains(target) == false)
            errors.Add($"{id}: alvo desconhecido '{target}'.");
    }

    private static void ValidateColumns(List<string> errors, ColumnDto[] columns, HashSet<string> ids)
    {
        foreach (var column in columns ?? Array.Empty<ColumnDto>())
        {
            if (column == null || string.IsNullOrWhiteSpace(column.Id))
            {
                errors.Add("Secao sem Id.");
                continue;
            }
            var id = column.Id;
            if (ids.Add(id) == false)
                errors.Add($"Id repetido: {id}.");
            if (IsValidId(id) == false)
                errors.Add($"{id}: Id so pode ter letras sem acento, numeros, '_' e '-'.");

            if (KnowledgeImportFormat.TryParse<NewsColumnKind>(column.Kind, out var kind) == false)
            {
                errors.Add($"{id}: tipo desconhecido '{column.Kind}'.");
                continue;
            }

            var isQuote = kind == NewsColumnKind.CitizenQuote;
            if (isQuote && KnowledgeImportFormat.TryParse<NewsCitizen>(column.Citizen, out _) == false)
                errors.Add($"{id}: morador desconhecido '{column.Citizen}'.");
            if (isQuote == false && string.IsNullOrEmpty(column.Citizen) == false)
                errors.Add($"{id}: {kind} nao tem morador.");

            var usesMood = kind != NewsColumnKind.Classified;
            if (usesMood && KnowledgeImportFormat.TryParse<NewsMoodBand>(column.Mood, out _) == false)
                errors.Add($"{id}: faixa desconhecida '{column.Mood}'.");
            if (usesMood == false && string.IsNullOrEmpty(column.Mood) == false)
                errors.Add($"{id}: {kind} nao tem faixa.");

            RequireText(errors, id, "TextPt", column.TextPt);
            RequireText(errors, id, "TextEn", column.TextEn);
            foreach (var text in new[] { column.TextPt, column.TextEn })
            {
                CheckText(errors, id, text);
                CheckLength(errors, id, "texto", text, MaxColumnLength);
                if (kind != NewsColumnKind.PollutionForecast && text != null && PlaceholderPattern.IsMatch(text))
                    errors.Add($"{id}: {kind} nao usa marcador.");
                if (kind == NewsColumnKind.PollutionForecast && text != null && PlaceholderPattern.Replace(text, match => match.Value == "{valor}" ? string.Empty : match.Value).Contains("{"))
                    errors.Add($"{id}: a previsao so usa {{valor}}.");
            }
        }
    }

    // Combinacoes de morador, tipo e faixa sem nenhum texto.
    public static List<string> MissingColumnCoverage(FileDto file)
    {
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in file?.Columns ?? Array.Empty<ColumnDto>())
        {
            if (column != null)
                covered.Add($"{column.Kind}/{column.Citizen}/{column.Mood}");
        }

        var missing = new List<string>();
        if (covered.Contains($"{NewsColumnKind.Classified}//") == false)
            missing.Add(NewsColumnKind.Classified.ToString());
        foreach (NewsMoodBand band in Enum.GetValues(typeof(NewsMoodBand)))
        {
            foreach (NewsCitizen citizen in Enum.GetValues(typeof(NewsCitizen)))
            {
                if (covered.Contains($"{NewsColumnKind.CitizenQuote}/{citizen}/{band}") == false)
                    missing.Add($"{NewsColumnKind.CitizenQuote}/{citizen}/{band}");
            }
            foreach (var kind in new[] { NewsColumnKind.Horoscope, NewsColumnKind.PollutionForecast })
            {
                if (covered.Contains($"{kind}//{band}") == false)
                    missing.Add($"{kind}/{band}");
            }
        }
        return missing;
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
            // Carta e evento sem texto so ficam fora da edicao, sem deixar o turno sem manchete.
            if (NewsRules.UsesTarget(trigger))
                continue;
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

    private static void CheckLength(List<string> errors, string id, string field, string text, int limit)
    {
        if (text != null && text.Length > limit)
            errors.Add($"{id}: {field} com {text.Length} caracteres, limite {limit}.");
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
