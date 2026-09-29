using System;
using System.Collections.Generic;

// Sorteia e resolve o evento aleatorio da fase de evento do turno.
public class RandomEventPool<TEvent> where TEvent : class, IRandomEventDefinition
{
    private readonly IReadOnlyList<TEvent> _pool;
    private readonly Random _random;

    // random e opcional, passar um com seed fixa deixa o sorteio reproduzivel.
    public RandomEventPool(IReadOnlyList<TEvent> pool, Random random = null)
    {
        _pool = pool;
        _random = random ?? new Random();
    }

    public bool IsEligible(TEvent eventData, CityStats stats, int turnIndex)
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
    public TEvent TryTriggerEvent(CityStats stats, int turnIndex)
    {
        var eligible = new List<TEvent>();
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
