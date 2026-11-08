using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Formato do arquivo de cenarios e a validacao dele, sem dependencia da Unity.
public static class ScenarioImportFormat
{
    public const int MinObjectives = 1;
    public const int MaxObjectives = 3;
    public const float MinThreshold = 0f;
    public const float MaxThreshold = 100f;

    private static readonly Regex IdPattern = new Regex("^[A-Za-z0-9_-]+$");

    [Serializable]
    public class ObjectiveDto
    {
        public string Parameter;
        public string Comparison;
        public float Threshold;
        public int ByTurn;
        public bool MustHold;
    }

    [Serializable]
    public class ScenarioDto
    {
        public string Id;
        public string Theme;
        public bool FreeArchetype = true;
        public string Archetype;
        public ObjectiveDto[] Objectives = Array.Empty<ObjectiveDto>();
    }

    [Serializable]
    public class FileDto
    {
        public ScenarioDto[] Scenarios = Array.Empty<ScenarioDto>();
    }

    public static List<string> Validate(FileDto file, int totalTurns, ICollection<string> knownThemes = null)
    {
        var errors = new List<string>();
        if (file == null || file.Scenarios == null)
        {
            errors.Add("arquivo sem a lista Scenarios");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scenario in file.Scenarios)
        {
            if (scenario == null || string.IsNullOrWhiteSpace(scenario.Id) || IdPattern.IsMatch(scenario.Id) == false)
            {
                errors.Add($"cenario com Id invalido: '{scenario?.Id}'");
                continue;
            }
            if (ids.Add(scenario.Id) == false)
                errors.Add($"{scenario.Id}: Id repetido");

            if (string.IsNullOrWhiteSpace(scenario.Theme))
                errors.Add($"{scenario.Id}: sem tema");
            else if (knownThemes != null && knownThemes.Contains(scenario.Theme) == false)
                errors.Add($"{scenario.Id}: tema desconhecido '{scenario.Theme}'");

            CheckArchetype(errors, scenario);
            CheckObjectives(errors, scenario, totalTurns);
        }
        return errors;
    }

    private static void CheckArchetype(List<string> errors, ScenarioDto scenario)
    {
        if (scenario.FreeArchetype)
            return;
        if (TryParseEnum<CardArchetype>(scenario.Archetype, out var archetype) == false || archetype == CardArchetype.Geral)
            errors.Add($"{scenario.Id}: arquetipo invalido '{scenario.Archetype}'");
    }

    private static void CheckObjectives(List<string> errors, ScenarioDto scenario, int totalTurns)
    {
        var objectives = scenario.Objectives ?? Array.Empty<ObjectiveDto>();
        if (objectives.Length < MinObjectives || objectives.Length > MaxObjectives)
            errors.Add($"{scenario.Id}: {objectives.Length} objetivos, precisa de {MinObjectives} a {MaxObjectives}");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var objective in objectives)
        {
            if (objective == null)
            {
                errors.Add($"{scenario.Id}: objetivo vazio");
                continue;
            }
            if (TryParseEnum<CityParameterType>(objective.Parameter, out _) == false)
                errors.Add($"{scenario.Id}: parametro desconhecido '{objective.Parameter}'");
            if (TryParseEnum<ObjectiveComparison>(objective.Comparison, out _) == false)
                errors.Add($"{scenario.Id}: comparacao desconhecida '{objective.Comparison}'");
            if (objective.Threshold < MinThreshold || objective.Threshold > MaxThreshold)
                errors.Add($"{scenario.Id}: limite de {objective.Parameter} fora de {MinThreshold} a {MaxThreshold}");
            if (objective.ByTurn < 1 || objective.ByTurn > totalTurns)
                errors.Add($"{scenario.Id}: turno de {objective.Parameter} fora de 1 a {totalTurns}");
            if (seen.Add($"{objective.Parameter}|{objective.MustHold}") == false)
                errors.Add($"{scenario.Id}: objetivo repetido de {objective.Parameter}");
        }
    }

    public static bool TryParseEnum<T>(string text, out T value) where T : struct
    {
        return Enum.TryParse(text, out value) && Enum.IsDefined(typeof(T), value) && int.TryParse(text, out _) == false;
    }
}
