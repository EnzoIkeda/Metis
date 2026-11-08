using System;
using System.Collections.Generic;
using UnityEngine;

// Todas as fases tematicas que o mapa pode oferecer.
[CreateAssetMenu(fileName = "ThemedPhaseLibrary", menuName = "Metis/Themed Phase Library")]
public class ThemedPhaseLibraryData : ScriptableObject
{
    [SerializeField] private ThemedPhaseData[] _themes = Array.Empty<ThemedPhaseData>();

    public IReadOnlyList<ThemedPhaseData> Themes => _themes;

    public List<string> Names()
    {
        var names = new List<string>();
        foreach (var theme in _themes)
        {
            if (theme != null)
                names.Add(theme.name);
        }
        return names;
    }

    public ThemedPhaseData Find(string themeName)
    {
        if (string.IsNullOrEmpty(themeName))
            return null;
        foreach (var theme in _themes)
        {
            if (theme != null && theme.name == themeName)
                return theme;
        }
        return null;
    }
}
