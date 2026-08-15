using System;
using System.Collections.Generic;

/// <summary>
/// Sorteia e resolve o evento aleatorio da fase de evento do turno
/// </summary>
public class RandomEventPool
{
    private readonly IReadOnlyList<RandomEventData> _pool;
    private readonly Random _random = new Random();

    public RandomEventPool(IReadOnlyList<RandomEventData> pool)
    {
        _pool = pool;
    }

    public bool IsEligible(RandomEventData eventData, CityStats stats)
    {
        if (eventData.TriggerConditions == null)
            return true;

        foreach (var condition in eventData.TriggerConditions)
        {
            var value = stats.GetValue(condition.Parameter);
            if (condition.Comparison == ComparisonType.GreaterThanOrEqual && value < condition.Threshold)
                return false;
            if (condition.Comparison == ComparisonType.LessThanOrEqual && value > condition.Threshold)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Sorteia um evento do pool e aplica seus efeitos. Retorna null se o pool estiver vazio ou nenhum evento estiver elegível agora.
    /// </summary>
    public RandomEventData TryTriggerEvent(CityStats stats)
    {
        var eligible = new List<RandomEventData>();
        foreach (var eventData in _pool)
        {
            if (IsEligible(eventData, stats))
                eligible.Add(eventData);
        }

        if (eligible.Count == 0)
            return null;

        var chosen = eligible[_random.Next(eligible.Count)];
        stats.ApplyModifiers(chosen.StatEffects);
        return chosen;
    }
}
