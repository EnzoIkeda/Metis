using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Formato do arquivo de fases tematicas e a validacao dele, sem dependencia da Unity.
public static class ThemedPhaseImportFormat
{
    public const int MaxNameLength = 40;
    public const int MaxSummaryLength = 80;
    public const int MaxOpeningLength = 260;
    public const int MaxVictoryLength = 200;
    public const int MaxInspirationLength = 200;

    // Com poucos eventos permitidos, a fase pode passar turnos sem nenhum elegivel.
    public const int MinAllowedEvents = 8;

    private static readonly Regex IdPattern = new Regex("^[A-Za-z0-9_-]+$");

    [Serializable]
    public class OverrideDto
    {
        public string Parameter;
        public float InitialValue;
    }

    [Serializable]
    public class ThemeDto
    {
        public string Id;
        public string NamePt;
        public string NameEn;
        public string SummaryPt;
        public string SummaryEn;
        public string OpeningPt;
        public string OpeningEn;
        public string VictoryPt;
        public string VictoryEn;
        public string InspirationPt;
        public string InspirationEn;

        // Ficha pelo nome do asset; vazia ate a ficha existir.
        public string KnowledgeEntry;
        public OverrideDto[] Overrides = Array.Empty<OverrideDto>();
        public float DerivaMultiplier = 1f;
        public string[] AllowedEvents = Array.Empty<string>();
        public int BackgroundIndex = -1;
        public int LayoutSeed;
    }

    [Serializable]
    public class FileDto
    {
        public ThemeDto[] Themes = Array.Empty<ThemeDto>();
    }

    public static List<string> Validate(FileDto file, ICollection<string> knownEvents = null, ICollection<string> knownEntries = null)
    {
        var errors = new List<string>();
        if (file == null || file.Themes == null)
        {
            errors.Add("arquivo sem a lista Themes");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var theme in file.Themes)
        {
            if (theme == null || string.IsNullOrWhiteSpace(theme.Id) || IdPattern.IsMatch(theme.Id) == false)
            {
                errors.Add($"tema com Id invalido: '{theme?.Id}'");
                continue;
            }
            if (ids.Add(theme.Id) == false)
                errors.Add($"{theme.Id}: Id repetido");

            CheckText(errors, theme.Id, "Name", theme.NamePt, theme.NameEn, MaxNameLength);
            CheckText(errors, theme.Id, "Summary", theme.SummaryPt, theme.SummaryEn, MaxSummaryLength);
            CheckText(errors, theme.Id, "Opening", theme.OpeningPt, theme.OpeningEn, MaxOpeningLength);
            CheckText(errors, theme.Id, "Victory", theme.VictoryPt, theme.VictoryEn, MaxVictoryLength);
            CheckText(errors, theme.Id, "Inspiration", theme.InspirationPt, theme.InspirationEn, MaxInspirationLength);
            CheckOverrides(errors, theme);
            CheckEvents(errors, theme, knownEvents);

            if (theme.DerivaMultiplier <= 0f)
                errors.Add($"{theme.Id}: DerivaMultiplier precisa ser maior que zero");
            if (theme.BackgroundIndex < -1)
                errors.Add($"{theme.Id}: BackgroundIndex precisa ser -1 ou maior");
            if (theme.LayoutSeed < 0)
                errors.Add($"{theme.Id}: LayoutSeed nao pode ser negativa");
            if (string.IsNullOrEmpty(theme.KnowledgeEntry) == false && knownEntries != null && knownEntries.Contains(theme.KnowledgeEntry) == false)
                errors.Add($"{theme.Id}: ficha desconhecida '{theme.KnowledgeEntry}'");
        }
        return errors;
    }

    public static List<string> Warnings(FileDto file)
    {
        var warnings = new List<string>();
        foreach (var theme in file.Themes)
        {
            if (theme.AllowedEvents != null && theme.AllowedEvents.Length > 0 && theme.AllowedEvents.Length < MinAllowedEvents)
                warnings.Add($"{theme.Id}: so {theme.AllowedEvents.Length} eventos permitidos");
            if (string.IsNullOrEmpty(theme.KnowledgeEntry))
                warnings.Add($"{theme.Id}: sem ficha");
        }
        return warnings;
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

    private static void CheckOverrides(List<string> errors, ThemeDto theme)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in theme.Overrides ?? Array.Empty<OverrideDto>())
        {
            if (Enum.TryParse<CityParameterType>(entry.Parameter, out var parameter) == false || Enum.IsDefined(typeof(CityParameterType), parameter) == false)
            {
                errors.Add($"{theme.Id}: parametro desconhecido '{entry.Parameter}'");
                continue;
            }
            if (parameter == CityStats.AnchorParameter)
                errors.Add($"{theme.Id}: {entry.Parameter} e calculado a partir dos outros");
            if (seen.Add(entry.Parameter) == false)
                errors.Add($"{theme.Id}: {entry.Parameter} repetido");
            if (entry.InitialValue < 0f || entry.InitialValue > 100f)
                errors.Add($"{theme.Id}: {entry.Parameter} fora de 0 a 100");
        }
    }

    private static void CheckEvents(List<string> errors, ThemeDto theme, ICollection<string> knownEvents)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var randomEvent in theme.AllowedEvents ?? Array.Empty<string>())
        {
            if (seen.Add(randomEvent) == false)
                errors.Add($"{theme.Id}: evento repetido '{randomEvent}'");
            if (knownEvents != null && knownEvents.Contains(randomEvent) == false)
                errors.Add($"{theme.Id}: evento desconhecido '{randomEvent}'");
        }
    }
}
