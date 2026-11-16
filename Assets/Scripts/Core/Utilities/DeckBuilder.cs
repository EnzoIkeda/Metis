using System;
using System.Collections.Generic;
using System.Linq;

// Monta o baralho, com as copias de cada carta, combinando arquetipo escolhido e recompensas carregadas de fases anteriores.
public static class DeckBuilder
{
    // extraCardIds sao as cartas acrescentadas por troca: uma copia de cada, mesmo repetida.
    public static List<TCard> Build<TCard>(IReadOnlyList<TCard> fullPool, CardArchetype archetype, IReadOnlyList<string> loadedCardNames,
        IReadOnlyList<string> extraCardIds = null)
        where TCard : class, ICardDefinition
    {
        var pool = new List<TCard>();
        foreach (var card in fullPool)
        {
            if (card.IsExtraOnly == false && (IsShared(card) || card.Archetype == archetype))
                AddCopies(pool, card);
        }

        foreach (var name in loadedCardNames)
        {
            var loadedCard = fullPool.FirstOrDefault(card => card.Id == name);
            if (loadedCard != null && loadedCard.IsExtraOnly == false && pool.Contains(loadedCard) == false)
                AddCopies(pool, loadedCard);
        }

        if (extraCardIds != null)
        {
            foreach (var id in extraCardIds)
            {
                var extra = fullPool.FirstOrDefault(card => card.Id == id);
                if (extra != null)
                    pool.Add(extra);
            }
        }

        return pool;
    }

    // Tier 0 entra em todo baralho, menos o do Comunitario, que e exclusivo dele.
    private static bool IsShared(ICardDefinition card)
    {
        if (card.Archetype == CardArchetype.Geral)
            return true;
        return card.Tier == CardTier.Basica && card.Archetype != CardArchetype.Comunitario;
    }

    private static void AddCopies<TCard>(List<TCard> pool, TCard card) where TCard : class, ICardDefinition
    {
        for (int i = 0; i < Math.Max(1, card.Copies); i++)
            pool.Add(card);
    }
}
