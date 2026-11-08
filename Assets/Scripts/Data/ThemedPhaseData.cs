using System;
using System.Collections.Generic;
using UnityEngine;

// Fase com cidade, texto e eventos definidos, escolhida como destino no mapa.
[CreateAssetMenu(fileName = "New Themed Phase", menuName = "Metis/Themed Phase Data")]
public class ThemedPhaseData : ScriptableObject
{
    [SerializeField] private string _themeName;
    [SerializeField] private string _themeNameEn;
    [SerializeField] private string _summary;
    [SerializeField] private string _summaryEn;
    [SerializeField] private string _opening;
    [SerializeField] private string _openingEn;
    [SerializeField] private string _victory;
    [SerializeField] private string _victoryEn;
    [SerializeField] private string _inspiration;
    [SerializeField] private string _inspirationEn;
    [SerializeField] private KnowledgeEntryData _knowledgeEntry;

    [SerializeField] private CityParameterOverride[] _overrides = Array.Empty<CityParameterOverride>();
    [SerializeField, Min(0f)] private float _derivaMultiplier = 1f;

    // Nomes dos eventos que podem sair nesta fase; vazio libera todos.
    [SerializeField] private string[] _allowedEventIds = Array.Empty<string>();

    // Fundo fixo e semente do tracado; -1 e 0 deixam sortear.
    [SerializeField] private int _backgroundIndex = -1;
    [SerializeField] private int _layoutSeed;

    public string ThemeName => Pick(_themeName, _themeNameEn);
    public string Summary => Pick(_summary, _summaryEn);
    public string Opening => Pick(_opening, _openingEn);
    public string Victory => Pick(_victory, _victoryEn);
    public string Inspiration => Pick(_inspiration, _inspirationEn);
    public KnowledgeEntryData KnowledgeEntry => _knowledgeEntry;
    public IReadOnlyList<CityParameterOverride> Overrides => _overrides;
    public float DerivaMultiplier => _derivaMultiplier;
    public IReadOnlyList<string> AllowedEventIds => _allowedEventIds;
    public int BackgroundIndex => _backgroundIndex;
    public bool HasLayoutSeed => _layoutSeed != 0;
    public ulong LayoutSeed => (ulong)(uint)_layoutSeed;

    private static string Pick(string portuguese, string english)
    {
        return LocalizationManager.Current == Language.English && string.IsNullOrEmpty(english) == false ? english : portuguese;
    }
}
