using System;
using System.Collections.Generic;

// Uma escolha feita na fase e o que aconteceu por causa dela.
public struct DilemmaEntry
{
    public int Turn;
    public string EventId;
    public int ChoiceIndex;
    public string FollowUpEventId;

    // Turno em que a consequencia aconteceu; zero enquanto nao aconteceu.
    public int FollowUpTurn;

    public bool HasFollowUp => string.IsNullOrEmpty(FollowUpEventId) == false;
}

// Liga cada escolha a sua consequencia, so lendo o historico da fase.
public static class DilemmaLog
{
    public static List<DilemmaEntry> Entries(PhaseHistory history, Func<string, IRandomEventDefinition> findEvent)
    {
        var entries = new List<DilemmaEntry>();
        var turns = new List<PhaseTurnRecord>(history.Turns);
        if (history.Current != null)
            turns.Add(history.Current);

        var claimed = new HashSet<int>();
        for (var i = 0; i < turns.Count; i++)
        {
            var turn = turns[i];
            if (turn.HasEventChoice == false)
                continue;

            var eventData = findEvent(turn.EventId);
            if (eventData == null || eventData.Choices == null || turn.EventChoice < 0 || turn.EventChoice >= eventData.Choices.Count)
                continue;

            var followUpId = eventData.Choices[turn.EventChoice].FollowUpEventId;
            var entry = new DilemmaEntry { Turn = turn.TurnIndex, EventId = turn.EventId, ChoiceIndex = turn.EventChoice, FollowUpEventId = followUpId };
            if (entry.HasFollowUp)
                entry.FollowUpTurn = FirstOccurrence(turns, i + 1, followUpId, claimed);
            entries.Add(entry);
        }
        return entries;
    }

    // A escolha mais recente que leva a esta consequencia, se houver.
    public static DilemmaEntry? OriginOf(PhaseHistory history, string followUpEventId, Func<string, IRandomEventDefinition> findEvent)
    {
        DilemmaEntry? origin = null;
        foreach (var entry in Entries(history, findEvent))
        {
            if (entry.FollowUpEventId == followUpEventId)
                origin = entry;
        }
        return origin;
    }

    private static int FirstOccurrence(List<PhaseTurnRecord> turns, int start, string eventId, HashSet<int> claimed)
    {
        for (var i = start; i < turns.Count; i++)
        {
            if (turns[i].EventId == eventId && claimed.Add(i))
                return turns[i].TurnIndex;
        }
        return 0;
    }
}
