using System;
using System.Collections.Generic;

// Efeito das trocas aceitas no mapa, que valem ate o fim da rodada. A mesma conta roda no jogo e no simulador.
public static class TradeOfferRules
{
    // Uma oferta por parada do mapa, sorteada entre as que ainda nao sairam na rodada.
    public static TOffer Draw<TOffer>(IReadOnlyList<TOffer> offers, IReadOnlyCollection<string> alreadyOffered, Random random)
        where TOffer : class, ITradeOfferDefinition
    {
        var candidates = new List<TOffer>();
        foreach (var offer in offers)
        {
            if (offer != null && Contains(alreadyOffered, offer.Id) == false)
                candidates.Add(offer);
        }
        return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : null;
    }

    // Ofertas aceitas, na ordem em que foram aceitas, sem as que deixaram de existir.
    public static List<TOffer> Resolve<TOffer>(IReadOnlyList<TOffer> offers, IEnumerable<string> acceptedIds)
        where TOffer : class, ITradeOfferDefinition
    {
        var accepted = new List<TOffer>();
        if (acceptedIds == null)
            return accepted;
        foreach (var id in acceptedIds)
        {
            foreach (var offer in offers)
            {
                if (offer != null && offer.Id == id && accepted.Contains(offer) == false)
                    accepted.Add(offer);
            }
        }
        return accepted;
    }

    // Aplica no inicio da fase os efeitos em parametros dos dois lados de cada troca aceita.
    public static void ApplyPhaseStart(IEnumerable<ITradeOfferDefinition> accepted, CityStats stats)
    {
        foreach (var term in Terms(accepted, TradeTermKind.PhaseStartEffect))
            stats.ApplyModifiers(term.EffectsOrEmpty());
    }

    public static int CardCostOffset(IEnumerable<ITradeOfferDefinition> accepted)
    {
        var total = 0;
        foreach (var term in Terms(accepted, TradeTermKind.CardCostOffset))
            total += term.Amount;
        return total;
    }

    // Sempre sobra ao menos uma opcao de recompensa.
    public static int RewardOptionCount(int baseCount, IEnumerable<ITradeOfferDefinition> accepted)
    {
        var total = baseCount;
        foreach (var term in Terms(accepted, TradeTermKind.RewardOptionOffset))
            total += term.Amount;
        return Math.Max(1, total);
    }

    // Cartas acrescentadas ao baralho pelas trocas, uma por lado que acrescenta.
    public static List<string> ExtraCardIds(IEnumerable<ITradeOfferDefinition> accepted)
    {
        var ids = new List<string>();
        foreach (var term in Terms(accepted, TradeTermKind.ExtraCard))
        {
            if (string.IsNullOrEmpty(term.CardId) == false)
                ids.Add(term.CardId);
        }
        return ids;
    }

    // Custo que a carta cobra com as trocas aceitas; nunca negativo.
    public static float EffectiveCost(float cost, int offset)
    {
        return Math.Max(0f, cost + offset);
    }

    private static IEnumerable<TradeTerm> Terms(IEnumerable<ITradeOfferDefinition> accepted, TradeTermKind kind)
    {
        if (accepted == null)
            yield break;
        foreach (var offer in accepted)
        {
            if (offer == null)
                continue;
            if (offer.Benefit.Kind == kind)
                yield return offer.Benefit;
            if (offer.Drawback.Kind == kind)
                yield return offer.Drawback;
        }
    }

    private static bool Contains(IReadOnlyCollection<string> ids, string id)
    {
        if (ids == null)
            return false;
        foreach (var candidate in ids)
        {
            if (candidate == id)
                return true;
        }
        return false;
    }
}
