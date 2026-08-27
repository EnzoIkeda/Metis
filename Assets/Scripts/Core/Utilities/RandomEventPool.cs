using System;
using System.Collections.Generic;

// Sorteia e resolve o evento aleatorio da fase de evento do turno.
public class RandomEventPool
{
    private readonly IReadOnlyList<RandomEventData> _pool;
    private readonly Random _random = new Random();

    public RandomEventPool(IReadOnlyList<RandomEventData> pool)
    {
        _pool = pool;
    }

    public bool IsEligible(RandomEventData eventData, CityStats stats, int turnIndex)
    {
        if (turnIndex < eventData.MinTurn || turnIndex > eventData.MaxTurn)
            return false;

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

    // Sorteia um evento elegivel do pool e aplica seus efeitos, ou retorna null se nenhum for elegivel.
    public RandomEventData TryTriggerEvent(CityStats stats, int turnIndex)
    {
        var eligible = new List<RandomEventData>();
        foreach (var eventData in _pool)
        {
            if (IsEligible(eventData, stats, turnIndex))
                eligible.Add(eventData);
        }

        if (eligible.Count == 0)
            return null;

        var chosen = eligible[_random.Next(eligible.Count)];
        stats.ApplyModifiers(chosen.StatEffects);
        return chosen;
    }
}
