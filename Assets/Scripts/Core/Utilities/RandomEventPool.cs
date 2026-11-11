using System;
using System.Collections.Generic;

// Sorteia e resolve o evento aleatorio da fase de evento do turno.
public class RandomEventPool<TEvent> where TEvent : class, IRandomEventDefinition
{
    private readonly IReadOnlyList<TEvent> _pool;
    private readonly IReadOnlyList<TEvent> _allEvents;
    private readonly Random _random;
    private readonly int _lastTurn;

    public EventSchedule Schedule { get; }

    // random e opcional, passar um com seed fixa deixa o sorteio reproduzivel.
    // allEvents e onde as consequencias sao buscadas, porque o pool pode vir filtrado.
    public RandomEventPool(IReadOnlyList<TEvent> pool, Random random = null, IReadOnlyList<TEvent> allEvents = null,
        EventSchedule schedule = null, int lastTurn = int.MaxValue)
    {
        _pool = pool;
        _allEvents = allEvents ?? pool;
        _random = random ?? new Random();
        Schedule = schedule ?? new EventSchedule();
        _lastTurn = lastTurn;
    }

    public bool IsEligible(TEvent eventData, CityStats stats, int turnIndex)
    {
        if (eventData.IsFollowUpOnly)
            return false;

        if (turnIndex < eventData.MinTurn || turnIndex > eventData.MaxTurn)
            return false;

        if (IsBlockedDilemma(eventData, turnIndex))
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

    // Consequencia vencida vem primeiro e nao gasta sorteio; sem ela, sorteia um evento elegivel do pool.
    // Aplica os efeitos do evento escolhido, ou retorna null se nenhum for elegivel.
    public TEvent TryTriggerEvent(CityStats stats, int turnIndex)
    {
        var forced = FindDue(turnIndex);
        if (forced != null)
        {
            stats.ApplyModifiers(forced.StatEffects);
            return forced;
        }

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
        if (IsDilemma(chosen))
            Schedule.MarkSeen(chosen.Id);
        return chosen;
    }

    // Aplica a opcao escolhida e agenda a consequencia dela, se houver.
    public void ApplyChoice(CityStats stats, TEvent eventData, int choiceIndex, int turnIndex)
    {
        var choices = eventData.Choices;
        if (choices == null || choiceIndex < 0 || choiceIndex >= choices.Count)
            throw new ArgumentOutOfRangeException(nameof(choiceIndex));

        var choice = choices[choiceIndex];
        if (choice.Effects != null)
            stats.ApplyModifiers(choice.Effects);
        if (choice.HasFollowUp)
            Schedule.Add(choice.FollowUpEventId, turnIndex + choice.EffectiveDelay);
    }

    private static bool IsDilemma(TEvent eventData)
    {
        return eventData.Choices != null && eventData.Choices.Count > 0;
    }

    // Dilema sai uma vez por fase e so quando a consequencia mais distante ainda cabe na fase.
    private bool IsBlockedDilemma(TEvent eventData, int turnIndex)
    {
        if (IsDilemma(eventData) == false)
            return false;
        if (Schedule.WasSeen(eventData.Id))
            return true;

        var longest = 0;
        foreach (var choice in eventData.Choices)
        {
            if (choice.HasFollowUp && choice.EffectiveDelay > longest)
                longest = choice.EffectiveDelay;
        }
        return longest > 0 && turnIndex + longest + Schedule.PendingCount > _lastTurn;
    }

    private TEvent FindDue(int turnIndex)
    {
        var dueId = Schedule.TakeDue(turnIndex);
        if (dueId == null)
            return null;

        foreach (var eventData in _allEvents)
        {
            if (eventData.Id == dueId)
                return eventData;
        }
        return null;
    }
}
