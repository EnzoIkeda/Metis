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
}
