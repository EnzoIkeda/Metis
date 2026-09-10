using System.Collections.Generic;
using System.Linq;

// Monta o pool de cartas que o CardHand compra, combinando arquetipo escolhido e recompensas carregadas de fases anteriores.
public static class DeckBuilder
{
    public static List<CardData> Build(IReadOnlyList<CardData> fullPool, CardArchetype archetype, IReadOnlyList<string> loadedCardNames)
    {
        var pool = new List<CardData>();
        foreach (var card in fullPool)
        {
            if (card.Tier == CardTier.Basica || card.Archetype == CardArchetype.Geral || card.Archetype == archetype)
                pool.Add(card);
        }

        foreach (var name in loadedCardNames)
        {
            var loadedCard = fullPool.FirstOrDefault(card => card.name == name);
            if (loadedCard != null && pool.Contains(loadedCard) == false)
                pool.Add(loadedCard);
        }

        return pool;
    }
}
