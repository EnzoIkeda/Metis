using System.Collections.Generic;
using System.Linq;

// Monta o pool de cartas que a mao compra, combinando arquetipo escolhido e recompensas carregadas de fases anteriores.
public static class DeckBuilder
{
    public static List<TCard> Build<TCard>(IReadOnlyList<TCard> fullPool, CardArchetype archetype, IReadOnlyList<string> loadedCardNames)
        where TCard : class, ICardDefinition
    {
        var pool = new List<TCard>();
        foreach (var card in fullPool)
        {
            if (card.Tier == CardTier.Basica || card.Archetype == CardArchetype.Geral || card.Archetype == archetype)
                pool.Add(card);
        }

        foreach (var name in loadedCardNames)
        {
            var loadedCard = fullPool.FirstOrDefault(card => card.Id == name);
            if (loadedCard != null && pool.Contains(loadedCard) == false)
                pool.Add(loadedCard);
        }

        return pool;
    }
}
