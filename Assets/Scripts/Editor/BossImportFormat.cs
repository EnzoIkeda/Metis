using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Formato do arquivo de chefes e a validacao dele, sem dependencia da Unity.
public static class BossImportFormat
{
    public const int MinCards = 6;
    public const int MaxCards = 7;
    public const int MaxNameLength = 40;
    public const int MaxSummaryLength = 240;
    public const int MaxIntroLength = 420;
    public const int MaxVictoryLength = 300;
    public const int MaxCardDescriptionLength = 160;

    private static readonly Regex IdPattern = new Regex("^[A-Za-z0-9_-]+$");

    [Serializable]
    public class ModifierDto
    {
        public string Parameter;
        public float Amount;
    }

    [Serializable]
    public class CardDto
    {
        public string Id;
        public string NamePt;
        public string NameEn;
        public string DescriptionPt;
        public string DescriptionEn;
        public int MomentumCost;
        public ModifierDto[] Effects = Array.Empty<ModifierDto>();
    }

    [Serializable]
    public class BossDto
    {
        public string Id;
        public string NamePt;
        public string NameEn;
        public string SummaryPt;
        public string SummaryEn;
        public string IntroPt;
        public string IntroEn;
        public string VictoryPt;
        public string VictoryEn;

        // Ficha pelo nome do asset; vazia ate a ficha existir.
        public string KnowledgeEntry;
        public string TargetArchetype;
        public int StartingMomentum;
        public int MomentumPerTurn;
        public string Mechanic;
        public float MechanicAmount;

        // Carta posta no baralho do jogador, so nas cartas extras.
        public string MechanicCardId;
        public CardDto[] Cards = Array.Empty<CardDto>();
    }

    [Serializable]
    public class FileDto
    {
        public BossDto[] Bosses = Array.Empty<BossDto>();
    }

    // knownExtraCards: cartas que so entram por fora do baralho, as unicas que o chefe pode por nele.
    public static List<string> Validate(FileDto file, ICollection<string> knownEntries = null, ICollection<string> knownExtraCards = null)
    {
        var errors = new List<string>();
        if (file == null || file.Bosses == null)
        {
            errors.Add("arquivo sem a lista Bosses");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var archetypes = new HashSet<CardArchetype>();
        foreach (var boss in file.Bosses)
        {
            if (boss == null || string.IsNullOrWhiteSpace(boss.Id) || IdPattern.IsMatch(boss.Id) == false)
            {
                errors.Add($"chefe com Id invalido: '{boss?.Id}'");
                continue;
            }
            if (ids.Add(boss.Id) == false)
                errors.Add($"{boss.Id}: Id repetido");

            CheckText(errors, boss.Id, "Name", boss.NamePt, boss.NameEn, MaxNameLength);
            CheckText(errors, boss.Id, "Summary", boss.SummaryPt, boss.SummaryEn, MaxSummaryLength);
            CheckText(errors, boss.Id, "Intro", boss.IntroPt, boss.IntroEn, MaxIntroLength);
            CheckText(errors, boss.Id, "Victory", boss.VictoryPt, boss.VictoryEn, MaxVictoryLength);

            if (TryParseArchetype(boss.TargetArchetype, out var archetype) == false || archetype == CardArchetype.Geral)
                errors.Add($"{boss.Id}: baralho desconhecido '{boss.TargetArchetype}'");
            else if (archetypes.Add(archetype) == false)
                errors.Add($"{boss.Id}: o baralho {archetype} ja tem chefe");

            if (TryParseMechanic(boss.Mechanic, out var mechanic) == false)
                errors.Add($"{boss.Id}: mecanica desconhecida '{boss.Mechanic}'");
            else
                CheckMechanic(errors, boss, mechanic, knownExtraCards);

            if (boss.StartingMomentum < 0)
                errors.Add($"{boss.Id}: Impeto inicial negativo");
            if (boss.MomentumPerTurn <= 0)
                errors.Add($"{boss.Id}: ganho de Impeto por turno precisa ser positivo");
            if (string.IsNullOrEmpty(boss.KnowledgeEntry) == false && knownEntries != null && knownEntries.Contains(boss.KnowledgeEntry) == false)
                errors.Add($"{boss.Id}: ficha desconhecida '{boss.KnowledgeEntry}'");

            CheckCards(errors, boss);
        }
        return errors;
    }

    private static void CheckMechanic(List<string> errors, BossDto boss, BossMechanic mechanic, ICollection<string> knownExtraCards)
    {
        var hasCard = string.IsNullOrEmpty(boss.MechanicCardId) == false;
        if (mechanic != BossMechanic.ExtraCards && hasCard)
            errors.Add($"{boss.Id}: MechanicCardId so vale nas cartas extras");

        switch (mechanic)
        {
            case BossMechanic.GrowingDamage:
                if (boss.MechanicAmount <= 0f)
                    errors.Add($"{boss.Id}: dano crescente sem aumento positivo");
                break;
            case BossMechanic.CascadeSpread:
                if (boss.MechanicAmount <= 0f || boss.MechanicAmount > 1f)
                    errors.Add($"{boss.Id}: cascata precisa de fracao entre 0 e 1");
                foreach (var card in boss.Cards ?? Array.Empty<CardDto>())
                {
                    var hitsEnergy = false;
                    foreach (var effect in card?.Effects ?? Array.Empty<ModifierDto>())
                        hitsEnergy |= effect.Parameter == nameof(CityParameterType.Energia) && effect.Amount < 0f;
                    if (card != null && hitsEnergy == false)
                        errors.Add($"{boss.Id}.{card.Id}: na cascata toda carta tira Energia");
                }
                break;
            case BossMechanic.ExtraCards:
                if (boss.MechanicAmount < 1f || Math.Abs(boss.MechanicAmount - Math.Round(boss.MechanicAmount)) > 0.0001f)
                    errors.Add($"{boss.Id}: cartas extras precisam de um numero inteiro de copias, a partir de 1");
                if (hasCard == false)
                    errors.Add($"{boss.Id}: cartas extras sem MechanicCardId");
                else if (knownExtraCards != null && knownExtraCards.Contains(boss.MechanicCardId) == false)
                    errors.Add($"{boss.Id}: carta '{boss.MechanicCardId}' nao existe ou entra em baralho");
                break;
            case BossMechanic.CostIncrease:
                if (boss.MechanicAmount <= 0f || boss.MechanicAmount > 1f)
                    errors.Add($"{boss.Id}: encarecimento precisa de aumento entre 0 e 1 por carta");
                break;
        }
    }

    public static List<string> Warnings(FileDto file)
    {
        var warnings = new List<string>();
        foreach (var boss in file.Bosses)
        {
            if (string.IsNullOrEmpty(boss.KnowledgeEntry))
                warnings.Add($"{boss.Id}: sem ficha");
        }
        return warnings;
    }

    public static bool TryParseArchetype(string text, out CardArchetype archetype)
    {
        return Enum.TryParse(text, out archetype) && Enum.IsDefined(typeof(CardArchetype), archetype);
    }

    public static bool TryParseMechanic(string text, out BossMechanic mechanic)
    {
        return Enum.TryParse(text, out mechanic) && Enum.IsDefined(typeof(BossMechanic), mechanic);
    }

    public static bool TryParseParameter(string text, out CityParameterType parameter)
    {
        return Enum.TryParse(text, out parameter) && Enum.IsDefined(typeof(CityParameterType), parameter);
    }

    // Cartas distintas, so de dano, pagas com Impeto.
    private static void CheckCards(List<string> errors, BossDto boss)
    {
        var cards = boss.Cards ?? Array.Empty<CardDto>();
        if (cards.Length < MinCards || cards.Length > MaxCards)
            errors.Add($"{boss.Id}: {cards.Length} cartas, precisa de {MinCards} a {MaxCards}");

        var cardIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Id) || IdPattern.IsMatch(card.Id) == false)
            {
                errors.Add($"{boss.Id}: carta com Id invalido: '{card?.Id}'");
                continue;
            }
            var where = $"{boss.Id}.{card.Id}";
            if (cardIds.Add(card.Id) == false)
                errors.Add($"{where}: Id repetido");
            CheckText(errors, where, "Name", card.NamePt, card.NameEn, MaxNameLength);
            CheckText(errors, where, "Description", card.DescriptionPt, card.DescriptionEn, MaxCardDescriptionLength);
            if (card.MomentumCost <= 0)
                errors.Add($"{where}: custo de Impeto precisa ser positivo");

            var effects = card.Effects ?? Array.Empty<ModifierDto>();
            if (effects.Length == 0)
                errors.Add($"{where}: sem efeitos");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var effect in effects)
            {
                if (TryParseParameter(effect.Parameter, out var parameter) == false)
                    errors.Add($"{where}: parametro desconhecido '{effect.Parameter}'");
                else if (parameter == CityParameterType.BemEstar)
                    errors.Add($"{where}: dano no Bem-estar ficaria para o resto da fase; o chefe ataca os outros indicadores");
                else if (seen.Add(effect.Parameter) == false)
                    errors.Add($"{where}: {effect.Parameter} repetido");
                if (effect.Amount >= 0f)
                    errors.Add($"{where}: {effect.Parameter} {effect.Amount} nao e dano");
            }
        }
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
        if (text.IndexOf('\u2014') >= 0)
            errors.Add($"{id}.{field}: tem travessao");
    }
}
