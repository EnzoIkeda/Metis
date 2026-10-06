using System;
using System.Collections.Generic;
using System.Linq;

// Monta o baralho, com as copias de cada carta, combinando arquetipo escolhido e recompensas carregadas de fases anteriores.
public static class DeckBuilder
{
    public static List<TCard> Build<TCard>(IReadOnlyList<TCard> fullPool, CardArchetype archetype, IReadOnlyList<string> loadedCardNames)
        where TCard : class, ICardDefinition
    {
        var pool = new List<TCard>();
        foreach (var card in fullPool)
        {
            if (card.Tier == CardTier.Basica || card.Archetype == CardArchetype.Geral || card.Archetype == archetype)
                AddCopies(pool, card);
        }

        foreach (var name in loadedCardNames)
        {
            var loadedCard = fullPool.FirstOrDefault(card => card.Id == name);
            if (loadedCard != null && pool.Contains(loadedCard) == false)
                AddCopies(pool, loadedCard);
        }

        return pool;
    }

    private static void AddCopies<TCard>(List<TCard> pool, TCard card) where TCard : class, ICardDefinition
    {
        for (int i = 0; i < Math.Max(1, card.Copies); i++)
            pool.Add(card);
    }
}
