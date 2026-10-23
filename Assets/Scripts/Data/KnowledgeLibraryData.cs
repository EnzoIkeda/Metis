using System;
using System.Collections.Generic;
using UnityEngine;

// Todas as fichas do almanaque, na ordem em que aparecem na grade.
[CreateAssetMenu(fileName = "KnowledgeLibrary", menuName = "Metis/Knowledge Library")]
public class KnowledgeLibraryData : ScriptableObject
{
    [SerializeField] private KnowledgeEntryData[] _entries = Array.Empty<KnowledgeEntryData>();

    public IReadOnlyList<KnowledgeEntryData> Entries => _entries;

    public int Count => _entries.Length;

    public KnowledgeEntryData Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var entry in _entries)
        {
            if (entry != null && entry.Id == id)
                return entry;
        }
        return null;
    }

    public IEnumerable<KnowledgeEntryData> EntriesFor(KnowledgeUnlockTrigger trigger)
    {
        if (trigger == KnowledgeUnlockTrigger.None)
            yield break;

        foreach (var entry in _entries)
        {
            if (entry != null && entry.UnlockTrigger == trigger)
                yield return entry;
        }
    }

    public IEnumerable<KnowledgeEntryData> EntriesIn(KnowledgeCategory category)
    {
        foreach (var entry in _entries)
        {
            if (entry != null && entry.Category == category)
                yield return entry;
        }
    }
}
