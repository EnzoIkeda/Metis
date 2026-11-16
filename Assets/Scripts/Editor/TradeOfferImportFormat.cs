using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Formato do arquivo de ofertas de troca e a validacao dele, sem dependencia da Unity.
public static class TradeOfferImportFormat
{
    public const int MaxNameLength = 40;
    public const int MaxSummaryLength = 240;

    private static readonly Regex IdPattern = new Regex("^[A-Za-z0-9_-]+$");

    [Serializable]
    public class ModifierDto
    {
        public string Parameter;
        public float Amount;
    }

    [Serializable]
    public class TermDto
    {
        public string Kind;
        public ModifierDto[] Effects = Array.Empty<ModifierDto>();
        public int Amount;
        public string CardId;
    }

    [Serializable]
    public class OfferDto
    {
        public string Id;
        public string NamePt;
        public string NameEn;
        public string SummaryPt;
        public string SummaryEn;

        // Ficha pelo nome do asset; vazia ate a ficha existir.
        public string KnowledgeEntry;
        public TermDto Benefit = new TermDto();
        public TermDto Drawback = new TermDto();
    }

    [Serializable]
    public class FileDto
    {
        public OfferDto[] Offers = Array.Empty<OfferDto>();
    }

    // extraOnlyCards: cartas que podem ser acrescentadas por troca, as que ficam fora de todo baralho.
    public static List<string> Validate(FileDto file, ICollection<string> knownEntries = null, ICollection<string> extraOnlyCards = null)
    {
        var errors = new List<string>();
        if (file == null || file.Offers == null)
        {
            errors.Add("arquivo sem a lista Offers");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var offer in file.Offers)
        {
            if (offer == null || string.IsNullOrWhiteSpace(offer.Id) || IdPattern.IsMatch(offer.Id) == false)
            {
                errors.Add($"oferta com Id invalido: '{offer?.Id}'");
                continue;
            }
            if (ids.Add(offer.Id) == false)
                errors.Add($"{offer.Id}: Id repetido");

            CheckText(errors, offer.Id, "Name", offer.NamePt, offer.NameEn, MaxNameLength);
            CheckText(errors, offer.Id, "Summary", offer.SummaryPt, offer.SummaryEn, MaxSummaryLength);
            var benefit = CheckTerm(errors, offer.Id, "Benefit", offer.Benefit, good: true, extraOnlyCards);
            var drawback = CheckTerm(errors, offer.Id, "Drawback", offer.Drawback, good: false, extraOnlyCards);
            if (benefit.HasValue && drawback.HasValue && benefit.Value == drawback.Value)
                errors.Add($"{offer.Id}: vantagem e desvantagem do mesmo tipo ({benefit.Value})");
            if (string.IsNullOrEmpty(offer.KnowledgeEntry) == false && knownEntries != null && knownEntries.Contains(offer.KnowledgeEntry) == false)
                errors.Add($"{offer.Id}: ficha desconhecida '{offer.KnowledgeEntry}'");
        }
        return errors;
    }

    public static List<string> Warnings(FileDto file)
    {
        var warnings = new List<string>();
        foreach (var offer in file.Offers)
        {
            if (string.IsNullOrEmpty(offer.KnowledgeEntry))
                warnings.Add($"{offer.Id}: sem ficha");
        }
        return warnings;
    }

    public static bool TryParseKind(string text, out TradeTermKind kind)
    {
        return Enum.TryParse(text, out kind) && Enum.IsDefined(typeof(TradeTermKind), kind);
    }

    public static bool TryParseParameter(string text, out CityParameterType parameter)
    {
        return Enum.TryParse(text, out parameter) && Enum.IsDefined(typeof(CityParameterType), parameter);
    }

    // A vantagem so ajuda e a desvantagem so atrapalha; a carta acrescentada e sempre desvantagem.
    private static TradeTermKind? CheckTerm(List<string> errors, string id, string field, TermDto term, bool good, ICollection<string> extraOnlyCards)
    {
        var where = $"{id}.{field}";
        if (term == null || TryParseKind(term.Kind, out var kind) == false)
        {
            errors.Add($"{where}: tipo desconhecido '{term?.Kind}'");
            return null;
        }

        switch (kind)
        {
            case TradeTermKind.PhaseStartEffect:
                var effects = term.Effects ?? Array.Empty<ModifierDto>();
                if (effects.Length == 0)
                    errors.Add($"{where}: sem efeitos");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var effect in effects)
                {
                    if (TryParseParameter(effect.Parameter, out _) == false)
                        errors.Add($"{where}: parametro desconhecido '{effect.Parameter}'");
                    else if (seen.Add(effect.Parameter) == false)
                        errors.Add($"{where}: {effect.Parameter} repetido");
                    if (good ? effect.Amount <= 0f : effect.Amount >= 0f)
                        errors.Add($"{where}: {effect.Parameter} {effect.Amount} vai no sentido errado");
                }
                break;
            case TradeTermKind.CardCostOffset:
                if (term.Amount == 0 || (good ? term.Amount > 0 : term.Amount < 0))
                    errors.Add($"{where}: custo {term.Amount} vai no sentido errado");
                break;
            case TradeTermKind.RewardOptionOffset:
                if (term.Amount == 0 || (good ? term.Amount < 0 : term.Amount > 0))
                    errors.Add($"{where}: opcoes de recompensa {term.Amount} vai no sentido errado");
                break;
            case TradeTermKind.ExtraCard:
                if (good)
                    errors.Add($"{where}: carta acrescentada so pode ser desvantagem");
                if (string.IsNullOrEmpty(term.CardId))
                    errors.Add($"{where}: sem CardId");
                else if (extraOnlyCards != null && extraOnlyCards.Contains(term.CardId) == false)
                    errors.Add($"{where}: '{term.CardId}' nao e uma carta que fica fora dos baralhos");
                break;
        }
        return kind;
    }

    private static void CheckText(List<string> errors, string id, string field, string pt, string en, int limit)
    {
        CheckLanguage(errors, id, field + "Pt", pt, limit);
        CheckLanguage(errors, id, field + "En", en, limit);
    }

    private static void CheckLanguage(List<string> errors, string id, string field, string text, int limit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add($"{id}.{field}: vazio");
            return;
        }
        if (text.Length > limit)
            errors.Add($"{id}.{field}: {text.Length} caracteres, limite {limit}");
        if (text.IndexOf('—') >= 0)
            errors.Add($"{id}.{field}: tem travessao");
    }
}
