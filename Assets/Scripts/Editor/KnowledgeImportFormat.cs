using System;
using System.Collections.Generic;

// Formato do arquivo de fichas "Saiba mais" e a validacao dele, sem dependencia da Unity pra poder
// ser testado fora do Editor.
public static class KnowledgeImportFormat
{
    [Serializable]
    public class FileDto
    {
        public EntryDto[] Entries = Array.Empty<EntryDto>();
    }

    [Serializable]
    public class SourceDto
    {
        public string Label;
        public string Url;
    }

    [Serializable]
    public class EntryDto
    {
        public string Id;
        public string Category;
        public string Trigger;
        public string TitlePt;
        public string TitleEn;
        public string SummaryPt;
        public string SummaryEn;
        public string RealWorldPt;
        public string RealWorldEn;
        public string TradeOffsPt;
        public string TradeOffsEn;
        public SourceDto[] Sources = Array.Empty<SourceDto>();
        public string[] Related = Array.Empty<string>();

        // Parametro mostrado pela ficha de parametro, pelo nome do enum; vazio nas outras.
        public string Parameter;

        // Ids das cartas e eventos que apontam pra esta ficha.
        public string[] Cards = Array.Empty<string>();
        public string[] Events = Array.Empty<string>();
    }

    // Lista de erros; vazia quando o arquivo pode ser aplicado.
    public static List<string> Validate(FileDto file, ICollection<string> knownCards, ICollection<string> knownEvents)
    {
        var errors = new List<string>();
        if (file == null || file.Entries == null || file.Entries.Length == 0)
        {
            errors.Add("Arquivo sem fichas.");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in file.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
            {
                errors.Add("Ficha sem Id.");
                continue;
            }
            if (ids.Add(entry.Id) == false)
                errors.Add($"Id repetido: {entry.Id}.");
            if (IsValidId(entry.Id) == false)
                errors.Add($"{entry.Id}: Id so pode ter letras sem acento, numeros, '_' e '-'.");
        }

        var cardOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var eventOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in file.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                continue;
            var id = entry.Id;

            var categoryOk = TryParse<KnowledgeCategory>(entry.Category, out var category);
            if (categoryOk == false)
                errors.Add($"{id}: categoria desconhecida '{entry.Category}'.");
            var triggerOk = TryParse<KnowledgeUnlockTrigger>(string.IsNullOrEmpty(entry.Trigger) ? nameof(KnowledgeUnlockTrigger.None) : entry.Trigger, out var trigger);
            if (triggerOk == false)
                errors.Add($"{id}: gatilho desconhecido '{entry.Trigger}'.");

            RequireText(errors, id, "TitlePt", entry.TitlePt);
            RequireText(errors, id, "TitleEn", entry.TitleEn);
            RequireText(errors, id, "SummaryPt", entry.SummaryPt);
            RequireText(errors, id, "SummaryEn", entry.SummaryEn);
            RequirePair(errors, id, "RealWorld", entry.RealWorldPt, entry.RealWorldEn);
            RequirePair(errors, id, "TradeOffs", entry.TradeOffsPt, entry.TradeOffsEn);
            CheckNoDash(errors, id, entry);

            foreach (var source in entry.Sources ?? Array.Empty<SourceDto>())
            {
                if (source == null || string.IsNullOrWhiteSpace(source.Label))
                    errors.Add($"{id}: fonte sem nome.");
                if (source == null || IsWebUrl(source.Url) == false)
                    errors.Add($"{id}: link de fonte invalido '{source?.Url}'.");
            }

            foreach (var related in entry.Related ?? Array.Empty<string>())
            {
                if (ids.Contains(related) == false)
                    errors.Add($"{id}: ficha relacionada desconhecida '{related}'.");
                else if (related == id)
                    errors.Add($"{id}: ficha relacionada a si mesma.");
            }

            var linksCards = (entry.Cards?.Length ?? 0) > 0;
            var linksEvents = (entry.Events?.Length ?? 0) > 0;
            CheckLinks(errors, id, entry.Cards, knownCards, cardOwners, "carta");
            CheckLinks(errors, id, entry.Events, knownEvents, eventOwners, "evento");

            if (categoryOk && triggerOk && trigger == KnowledgeUnlockTrigger.None && linksCards == false && linksEvents == false)
                errors.Add($"{id}: sem gatilho e sem carta ou evento, nunca seria desbloqueada.");
            if (categoryOk && category == KnowledgeCategory.Parameter && TryParse<CityParameterType>(entry.Parameter, out _) == false)
                errors.Add($"{id}: parametro desconhecido '{entry.Parameter}'.");
            if (categoryOk && category != KnowledgeCategory.Parameter && string.IsNullOrEmpty(entry.Parameter) == false)
                errors.Add($"{id}: so ficha de parametro indica parametro.");
            if (categoryOk && category == KnowledgeCategory.Card && linksCards == false)
                errors.Add($"{id}: ficha de carta sem carta ligada.");
            if (categoryOk && category == KnowledgeCategory.Event && linksEvents == false)
                errors.Add($"{id}: ficha de evento sem evento ligado.");
        }
        return errors;
    }

    public static bool TryParse<T>(string text, out T value) where T : struct
    {
        value = default;
        return string.IsNullOrWhiteSpace(text) == false
            && int.TryParse(text, out _) == false
            && Enum.TryParse(text.Trim(), false, out value)
            && Enum.IsDefined(typeof(T), value);
    }

    private static void CheckLinks(List<string> errors, string id, string[] links, ICollection<string> known, Dictionary<string, string> owners, string kind)
    {
        foreach (var link in links ?? Array.Empty<string>())
        {
            if (known != null && known.Contains(link) == false)
                errors.Add($"{id}: {kind} desconhecida '{link}'.");
            if (owners.TryGetValue(link, out var owner))
                errors.Add($"{id}: {kind} '{link}' ja aponta pra ficha {owner}.");
            else
                owners[link] = id;
        }
    }

    private static void RequireText(List<string> errors, string id, string field, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            errors.Add($"{id}: {field} vazio.");
    }

    // Secao opcional, mas se existir precisa estar nos dois idiomas.
    private static void RequirePair(List<string> errors, string id, string field, string pt, string en)
    {
        if (string.IsNullOrWhiteSpace(pt) != string.IsNullOrWhiteSpace(en))
            errors.Add($"{id}: {field} so em um idioma.");
    }

    private static void CheckNoDash(List<string> errors, string id, EntryDto entry)
    {
        var texts = new[] { entry.TitlePt, entry.TitleEn, entry.SummaryPt, entry.SummaryEn, entry.RealWorldPt, entry.RealWorldEn, entry.TradeOffsPt, entry.TradeOffsEn };
        foreach (var text in texts)
        {
            if (text != null && (text.Contains("—") || text.Contains("–")))
            {
                errors.Add($"{id}: texto com travessao.");
                return;
            }
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

    private static bool IsWebUrl(string url)
    {
        return Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }
}
