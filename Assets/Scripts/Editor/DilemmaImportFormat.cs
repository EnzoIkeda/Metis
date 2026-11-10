using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// Formato do arquivo de dilemas e a validacao dele, sem dependencia da Unity.
public static class DilemmaImportFormat
{
    public const int MaxTitleLength = 40;
    public const int MaxDescriptionLength = 260;
    public const int MaxLabelLength = 56;
    public const int MaxDetailLength = 120;
    public const int MinChoices = 2;
    public const int MaxChoices = 3;

    // Duracao da fase: a consequencia precisa caber nela.
    public const int PhaseTurns = 20;

    private static readonly Regex IdPattern = new Regex("^[A-Za-z0-9_-]+$");

    [Serializable]
    public class EffectDto
    {
        public string Parameter;
        public float Amount;
    }

    [Serializable]
    public class ChoiceDto
    {
        public string LabelPt;
        public string LabelEn;
        public string DetailPt;
        public string DetailEn;
        public EffectDto[] Effects = Array.Empty<EffectDto>();

        // Id da consequencia; vazio quando a opcao nao tem nenhuma.
        public string FollowUp;
        public int Delay;
    }

    [Serializable]
    public class DilemmaDto
    {
        public string Id;
        public string TitlePt;
        public string TitleEn;
        public string DescriptionPt;
        public string DescriptionEn;
        public int MinTurn = 1;
        public int MaxTurn;
        public ChoiceDto[] Choices = Array.Empty<ChoiceDto>();
    }

    [Serializable]
    public class FollowUpDto
    {
        public string Id;
        public string TitlePt;
        public string TitleEn;
        public string DescriptionPt;
        public string DescriptionEn;
        public EffectDto[] Effects = Array.Empty<EffectDto>();
    }

    [Serializable]
    public class FileDto
    {
        public DilemmaDto[] Dilemmas = Array.Empty<DilemmaDto>();
        public FollowUpDto[] FollowUps = Array.Empty<FollowUpDto>();
    }

    // commonEvents sao os eventos comuns do jogo: um dilema nao pode reusar o nome de um deles.
    public static List<string> Validate(FileDto file, ICollection<string> commonEvents = null)
    {
        var errors = new List<string>();
        if (file == null || file.Dilemmas == null || file.FollowUps == null)
        {
            errors.Add("arquivo sem as listas Dilemmas e FollowUps");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var followUps = new HashSet<string>(file.FollowUps.Where(item => item != null && item.Id != null).Select(item => item.Id), StringComparer.Ordinal);
        foreach (var followUp in file.FollowUps)
        {
            if (CheckId(errors, followUp?.Id, ids, commonEvents) == false)
                continue;
            CheckTexts(errors, followUp.Id, followUp.TitlePt, followUp.TitleEn, followUp.DescriptionPt, followUp.DescriptionEn);
            CheckEffects(errors, followUp.Id, followUp.Effects);
            if (followUp.Effects == null || followUp.Effects.Length == 0)
                errors.Add($"{followUp.Id}: consequencia sem efeito");
        }

        foreach (var dilemma in file.Dilemmas)
        {
            if (CheckId(errors, dilemma?.Id, ids, commonEvents) == false)
                continue;
            CheckTexts(errors, dilemma.Id, dilemma.TitlePt, dilemma.TitleEn, dilemma.DescriptionPt, dilemma.DescriptionEn);
            CheckDilemma(errors, dilemma, followUps);
        }
        return errors;
    }

    public static List<string> Warnings(FileDto file)
    {
        var warnings = new List<string>();
        var used = new HashSet<string>(file.Dilemmas.SelectMany(dilemma => dilemma.Choices ?? Array.Empty<ChoiceDto>()).Select(choice => choice.FollowUp ?? string.Empty));
        foreach (var followUp in file.FollowUps)
        {
            if (used.Contains(followUp.Id) == false)
                warnings.Add($"{followUp.Id}: nenhuma opcao leva a esta consequencia");
        }
        return warnings;
    }

    // Sem consequencia e com todo efeito igual ou pior que o de outra opcao sem consequencia: ninguem teria motivo para escolher.
    public static bool IsDominated(ChoiceDto choice, ChoiceDto other)
    {
        if (HasFollowUp(choice) || HasFollowUp(other))
            return false;

        var parameters = Effects(choice).Concat(Effects(other)).Select(effect => effect.Parameter).Distinct();
        return parameters.All(parameter => Amount(choice, parameter) <= Amount(other, parameter));
    }

    private static bool HasFollowUp(ChoiceDto choice)
    {
        return string.IsNullOrEmpty(choice.FollowUp) == false;
    }

    private static IEnumerable<EffectDto> Effects(ChoiceDto choice)
    {
        return choice.Effects ?? Array.Empty<EffectDto>();
    }

    private static float Amount(ChoiceDto choice, string parameter)
    {
        return Effects(choice).Where(effect => effect.Parameter == parameter).Sum(effect => effect.Amount);
    }

    private static bool CheckId(List<string> errors, string id, HashSet<string> ids, ICollection<string> commonEvents)
    {
        if (string.IsNullOrWhiteSpace(id) || IdPattern.IsMatch(id) == false)
        {
            errors.Add($"Id invalido: '{id}'");
            return false;
        }
        if (ids.Add(id) == false)
            errors.Add($"{id}: Id repetido");
        if (commonEvents != null && commonEvents.Contains(id))
            errors.Add($"{id}: ja existe um evento comum com este nome");
        return true;
    }

    private static void CheckDilemma(List<string> errors, DilemmaDto dilemma, HashSet<string> followUps)
    {
        var choices = dilemma.Choices ?? Array.Empty<ChoiceDto>();
        if (choices.Length < MinChoices || choices.Length > MaxChoices)
            errors.Add($"{dilemma.Id}: precisa de {MinChoices} ou {MaxChoices} opcoes, tem {choices.Length}");
        if (dilemma.MinTurn < 1 || dilemma.MaxTurn < dilemma.MinTurn)
            errors.Add($"{dilemma.Id}: janela de turnos invalida");

        var longest = 0;
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            var label = $"{dilemma.Id}.Choices[{i}]";
            CheckLanguage(errors, label, "LabelPt", choice.LabelPt, MaxLabelLength);
            CheckLanguage(errors, label, "LabelEn", choice.LabelEn, MaxLabelLength);
            CheckLanguage(errors, label, "DetailPt", choice.DetailPt, MaxDetailLength);
            CheckLanguage(errors, label, "DetailEn", choice.DetailEn, MaxDetailLength);
            CheckEffects(errors, label, choice.Effects);

            if (HasFollowUp(choice))
            {
                if (followUps.Contains(choice.FollowUp) == false)
                    errors.Add($"{label}: consequencia desconhecida '{choice.FollowUp}'");
                if (choice.Delay < 1)
                    errors.Add($"{label}: consequencia precisa de atraso de 1 turno ou mais");
                longest = Math.Max(longest, choice.Delay);
            }
            else if (choice.Delay != 0)
            {
                errors.Add($"{label}: atraso sem consequencia");
            }

            for (var j = 0; j < choices.Length; j++)
            {
                if (i != j && IsDominated(choice, choices[j]))
                    errors.Add($"{label}: dominada pela opcao {j}");
            }
        }

        if (dilemma.MaxTurn + longest > PhaseTurns)
            errors.Add($"{dilemma.Id}: MaxTurn {dilemma.MaxTurn} mais atraso {longest} passa dos {PhaseTurns} turnos da fase");
    }

    private static void CheckEffects(List<string> errors, string id, EffectDto[] effects)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effect in effects ?? Array.Empty<EffectDto>())
        {
            if (Enum.TryParse<CityParameterType>(effect.Parameter, out var parameter) == false || Enum.IsDefined(typeof(CityParameterType), parameter) == false)
            {
                errors.Add($"{id}: parametro desconhecido '{effect.Parameter}'");
                continue;
            }
            if (parameter == CityStats.AnchorParameter)
                errors.Add($"{id}: {effect.Parameter} e calculado a partir dos outros");
            if (seen.Add(effect.Parameter) == false)
                errors.Add($"{id}: {effect.Parameter} repetido");
            if (effect.Amount == 0f)
                errors.Add($"{id}: efeito zero em {effect.Parameter}");
        }
    }

    private static void CheckTexts(List<string> errors, string id, string titlePt, string titleEn, string descriptionPt, string descriptionEn)
    {
        CheckLanguage(errors, id, "TitlePt", titlePt, MaxTitleLength);
        CheckLanguage(errors, id, "TitleEn", titleEn, MaxTitleLength);
        CheckLanguage(errors, id, "DescriptionPt", descriptionPt, MaxDescriptionLength);
        CheckLanguage(errors, id, "DescriptionEn", descriptionEn, MaxDescriptionLength);
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
