using System;
using System.Collections.Generic;
using UnityEngine;

// Redacao do jornal na cidade: publica uma edicao por turno, so lendo o que aconteceu.
public class NewsRoom : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private NewsLibraryData _library;

    private NewsRules _rules;

    public NewsArchive Archive => _turnManager != null ? _turnManager.News : null;

    public NewsLibraryData Library => _library;

    private void Start()
    {
        _rules = BuildRules();
        _turnManager.OnTurnFinished += HandleTurnFinished;

        // Fase nova, ou save de antes do jornal: a primeira edicao e a de posse.
        if (Archive != null && Archive.Editions.Count == 0)
            PublishInauguration();
    }

    private void OnDestroy()
    {
        if (_turnManager != null)
            _turnManager.OnTurnFinished -= HandleTurnFinished;
    }

    private NewsRules BuildRules()
    {
        var stats = _cityStatsManager.Stats;
        var crisis = stats.Interactions != null ? stats.Interactions.CrisisThreshold : 25f;
        var excess = stats.Interactions != null ? stats.Interactions.ExcessThreshold : 75f;

        var tiers = new List<float>();
        foreach (var card in _turnManager.CardPool)
        {
            if (card != null && card.RequiredPesquisa > 0f && tiers.Contains(card.RequiredPesquisa) == false)
                tiers.Add(card.RequiredPesquisa);
        }
        tiers.Sort();
        return new NewsRules(_cityStatsManager.ParameterConfigs, crisis, excess, tiers);
    }

    private IReadOnlyList<NewsStoryData> Stories => _library != null ? _library.Stories : Array.Empty<NewsStoryData>();

    private IReadOnlyList<NewsColumnData> Columns => _library != null ? _library.Columns : Array.Empty<NewsColumnData>();

    private void PublishInauguration()
    {
        var start = _turnManager.History != null ? _turnManager.History.StartValues : Array.Empty<float>();
        var wellbeing = start.Length == PhaseHistory.Parameters.Length
            ? PhaseHistory.Read(start, CityStats.AnchorParameter)
            : _cityStatsManager.Stats.GetValue(CityStats.AnchorParameter);
        var random = NewsEditor.EditionRandom(Archive.Seed, 0);
        var edition = NewsEditor.Inauguration(Stories, random, wellbeing);
        var sustainability = start.Length == PhaseHistory.Parameters.Length
            ? PhaseHistory.Read(start, CityParameterType.Sustentabilidade)
            : _cityStatsManager.Stats.GetValue(CityParameterType.Sustentabilidade);
        NewsEditor.ComposeColumns(edition, Columns, Archive.Editions, Archive.Seed, sustainability, _rules);
        Archive.Add(edition);
    }

    private void HandleTurnFinished(PhaseTurnRecord turn)
    {
        var edition = NewsEditor.EditionForTurn(_turnManager.History, turn, _rules, Archive, Stories, Columns);

        // A causa so existe pro turno que acabou de ser registrado.
        var breakdown = _turnManager.LastBreakdown;
        if (breakdown != null && breakdown.TurnIndex == turn.TurnIndex)
        {
            foreach (var item in edition.Items)
            {
                if (NewsRules.UsesParameter(item.Trigger) && NewsEditor.TryMainCause(breakdown, item.Parameter, item.Delta, out var source, out var from))
                {
                    item.HasCause = true;
                    item.CauseSource = source;
                    item.CauseFrom = from;
                }
            }
        }

        Archive.Add(edition);
    }
}
