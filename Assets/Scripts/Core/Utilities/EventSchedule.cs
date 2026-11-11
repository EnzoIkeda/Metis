using System;
using System.Collections.Generic;

// Consequencia adiada de uma escolha, marcada para um turno.
[Serializable]
public struct ScheduledFollowUp
{
    public string EventId;
    public int Turn;
}

// Consequencias pendentes e dilemas que ja sairam na fase.
public class EventSchedule
{
    private readonly List<ScheduledFollowUp> _followUps = new List<ScheduledFollowUp>();
    private readonly HashSet<string> _seen = new HashSet<string>();

    public IReadOnlyList<ScheduledFollowUp> FollowUps => _followUps;
    public int PendingCount => _followUps.Count;

    public EventSchedule()
    {
    }

    public EventSchedule(IEnumerable<ScheduledFollowUp> followUps, IEnumerable<string> seen)
    {
        if (followUps != null)
            _followUps.AddRange(followUps);
        if (seen == null)
            return;
        foreach (var id in seen)
            MarkSeen(id);
    }

    public EventSchedule Clone()
    {
        return new EventSchedule(_followUps, _seen);
    }

    // Mesma fase para quem nao sabe das consequencias ocultas: so o que ja saiu fica.
    public EventSchedule WithoutFollowUps()
    {
        return new EventSchedule(null, _seen);
    }

    public bool WasSeen(string eventId)
    {
        return _seen.Contains(eventId);
    }

    public void MarkSeen(string eventId)
    {
        if (string.IsNullOrEmpty(eventId) == false)
            _seen.Add(eventId);
    }

    // Turno ja ocupado empurra a consequencia para o primeiro turno livre depois dele.
    public int Add(string eventId, int turn)
    {
        while (IsTaken(turn))
            turn++;
        _followUps.Add(new ScheduledFollowUp { EventId = eventId, Turn = turn });
        return turn;
    }

    // Retira a consequencia vencida mais antiga; null quando nao ha nenhuma.
    public string TakeDue(int turn)
    {
        var found = -1;
        for (var i = 0; i < _followUps.Count; i++)
        {
            if (_followUps[i].Turn <= turn && (found < 0 || _followUps[i].Turn < _followUps[found].Turn))
                found = i;
        }
        if (found < 0)
            return null;

        var eventId = _followUps[found].EventId;
        _followUps.RemoveAt(found);
        return eventId;
    }

    private bool IsTaken(int turn)
    {
        foreach (var followUp in _followUps)
        {
            if (followUp.Turn == turn)
                return true;
        }
        return false;
    }
}
