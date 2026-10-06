using System;
using System.Collections.Generic;

// Sorteia as opcoes de recompensa de fim de fase, sem repetir item.
public static class RewardOptionPicker
{
    public static List<T> Draw<T>(IReadOnlyList<T> pool, int count, Random random)
    {
        var remaining = new List<T>(pool);
        var options = new List<T>();
        while (options.Count < count && remaining.Count > 0)
        {
            var index = random.Next(remaining.Count);
            options.Add(remaining[index]);
            remaining.RemoveAt(index);
        }
        return options;
    }

    // Cartas que ainda acrescentam algo: fora do baralho atual, que ja ignoraria a repetida.
    public static List<TCard> CardPool<TCard>(IReadOnlyList<TCard> allCards, IReadOnlyList<TCard> currentDeck)
        where TCard : class, ICardDefinition
    {
        var pool = new List<TCard>();
        foreach (var card in allCards)
        {
            if (Contains(currentDeck, card) == false)
                pool.Add(card);
        }
        return pool;
    }

    // Vantagens ainda nao carregadas, ja que a meta-progressao ignora a repetida.
    public static List<TAdvantage> AdvantagePool<TAdvantage>(IReadOnlyList<TAdvantage> allAdvantages, IReadOnlyList<string> loadedIds)
        where TAdvantage : class, IAdvantageDefinition
    {
        var pool = new List<TAdvantage>();
        foreach (var advantage in allAdvantages)
        {
            if (Contains(loadedIds, advantage.Id) == false)
                pool.Add(advantage);
        }
        return pool;
    }

    private static bool Contains<T>(IReadOnlyList<T> items, T value)
    {
        foreach (var item in items)
        {
            if (EqualityComparer<T>.Default.Equals(item, value))
                return true;
        }
        return false;
    }
}
