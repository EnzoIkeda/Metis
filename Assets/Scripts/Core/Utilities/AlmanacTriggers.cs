using System.Collections.Generic;

// Grupo da ficha no almanaque. So acrescentar no fim: a Unity grava enum pelo numero.
public enum KnowledgeCategory
{
    Card,
    Event,
    Concept,
    Parameter
}

// Momento do jogo que desbloqueia uma ficha. As de carta e evento usam None e liberam quando a carta
// e jogada ou o evento acontece. So acrescentar no fim: a Unity grava enum pelo numero.
public enum KnowledgeUnlockTrigger
{
    None,
    GameStart,
    FirstCrisis,
    FirstExcess,
    FirstCollapse,
    FirstDefeat,
    FirstVictory,
    TierCidadeDigital,
    TierCidadeConectada,
    TierSmartCity,
    OpenRelationMap,
    OpenChangeBreakdown
}

// Regras de quais momentos do jogo desbloqueiam fichas de conceito. So leitura: nao muda nenhum numero.
public static class AlmanacTriggers
{
    // Menor exigencia de Pesquisa entre as cartas de cada tier acima do basico.
    public static Dictionary<CardTier, float> TierGates(IEnumerable<ICardDefinition> pool)
    {
        var gates = new Dictionary<CardTier, float>();
        if (pool == null)
            return gates;

        foreach (var card in pool)
        {
            if (card == null || card.Tier == CardTier.Basica)
                continue;
            if (gates.TryGetValue(card.Tier, out var current) == false || card.RequiredPesquisa < current)
                gates[card.Tier] = card.RequiredPesquisa;
        }
        return gates;
    }

    public static KnowledgeUnlockTrigger ForTier(CardTier tier)
    {
        switch (tier)
        {
            case CardTier.CidadeDigital:
                return KnowledgeUnlockTrigger.TierCidadeDigital;
            case CardTier.CidadeConectada:
                return KnowledgeUnlockTrigger.TierCidadeConectada;
            case CardTier.SmartCity:
                return KnowledgeUnlockTrigger.TierSmartCity;
            default:
                return KnowledgeUnlockTrigger.None;
        }
    }

    public static KnowledgeUnlockTrigger ForOutcome(GameOutcome outcome)
    {
        switch (outcome)
        {
            case GameOutcome.Victory:
                return KnowledgeUnlockTrigger.FirstVictory;
            case GameOutcome.GameOver:
                return KnowledgeUnlockTrigger.FirstDefeat;
            default:
                return KnowledgeUnlockTrigger.None;
        }
    }

    // Gatilhos que o estado atual de um parametro dispara: zona de crise ou excesso, Colapso e,
    // pra Pesquisa, cada tier ja alcancado.
    public static List<KnowledgeUnlockTrigger> ForParameter(CityStats stats, CityParameterType parameter, IReadOnlyDictionary<CardTier, float> tierGates)
    {
        var triggers = new List<KnowledgeUnlockTrigger>();
        if (stats == null)
            return triggers;

        var value = stats.GetValue(parameter);
        if (stats.Interactions != null)
        {
            var zone = stats.Interactions.GetZone(value);
            if (zone == InteractionZone.Crise)
                triggers.Add(KnowledgeUnlockTrigger.FirstCrisis);
            else if (zone == InteractionZone.Excesso)
                triggers.Add(KnowledgeUnlockTrigger.FirstExcess);
        }

        if (stats.IsInCollapse(parameter))
            triggers.Add(KnowledgeUnlockTrigger.FirstCollapse);

        if (parameter == CityParameterType.Pesquisa && tierGates != null)
        {
            foreach (var gate in tierGates)
            {
                if (value >= gate.Value)
                    triggers.Add(ForTier(gate.Key));
            }
        }
        return triggers;
    }
}
