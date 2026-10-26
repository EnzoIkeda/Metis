using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Relatorio de gestao no fim da fase: grafico dos parametros, decisoes de maior impacto, Colapsos e outra jogada possivel.
public class PhaseReportView : MonoBehaviour
{
    // Tons claros, legiveis sobre o fundo azul do popup.
    private const string PositiveColor = "#C6FF6B";
    private const string NegativeColor = "#FFC2C2";

    private const int DecisionCount = 3;
    private const int EffectsPerDecision = 3;

    // Efeitos menores que isso (no indice) nao aparecem na linha da decisao.
    private const float MinimumShownAmount = 0.05f;

    private static readonly CityParameterType[] Parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));

    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private ParameterDisplayData _displayData;

    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _summaryText;
    [SerializeField] private TMP_Text _hintText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private ScrollRect _bodyScroll;
    [SerializeField] private Button _continueButton;

    [Header("Grafico")]
    [SerializeField] private UILineGraphic _chartLines;
    [SerializeField] private TMP_Text _axisLabelTemplate;
    [SerializeField] private RectTransform _legendRoot;
    [SerializeField] private Button _legendButtonTemplate;
    [SerializeField] private float _anchorLineWidth = 7f;
    [SerializeField] private float _lineWidth = 3.5f;
    [SerializeField] private float _markerHeight = 14f;
    [SerializeField] private float _decisionMarkerHeight = 34f;

    [Header("Cores")]
    [Tooltip("Uma cor por parametro, na ordem do enum.")]
    [SerializeField] private Color[] _parameterColors =
    {
        new Color(0.91f, 0.73f, 0.29f),
        new Color(1f, 0.54f, 0.24f),
        new Color(0.5f, 0.66f, 1f),
        new Color(0.78f, 0.61f, 1f),
        new Color(0.37f, 0.89f, 0.89f),
        new Color(0.42f, 0.84f, 0.42f),
        new Color(1f, 1f, 1f),
        new Color(1f, 0.42f, 0.48f),
        new Color(0.69f, 0.72f, 0.77f),
    };
    [SerializeField] private Color _gridColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color _criticalColor = new Color(0.95f, 0.33f, 0.3f, 0.8f);
    [SerializeField] private Color _markerColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color _decisionColor = new Color(1f, 0.83f, 0.29f);
    [SerializeField, Range(0f, 1f)] private float _hiddenLegendAlpha = 0.3f;

    private readonly HashSet<CityParameterType> _hidden = new HashSet<CityParameterType>();
    private readonly Dictionary<CityParameterType, Button> _legendButtons = new Dictionary<CityParameterType, Button>();
    private readonly List<TMP_Text> _axisLabels = new List<TMP_Text>();
    private readonly List<UILine> _lineBuffer = new List<UILine>();

    private PhaseHistory _history;
    private List<DecisionImpact> _decisions = new List<DecisionImpact>();
    private Action _onContinue;
    private bool _showing;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    // Ultimo texto montado, pra quem precisa conferir o que o relatorio disse.
    public string BodyText { get; private set; } = string.Empty;

    public event Action Opened;

    private void Start()
    {
        if (_continueButton != null)
            _continueButton.onClick.AddListener(Continue);
        if (_axisLabelTemplate != null)
            _axisLabelTemplate.gameObject.SetActive(false);
        BuildLegend();

        // Pode ter aberto antes do proprio Start, quando a fase acaba no primeiro quadro.
        if (_showing == false)
            Hide();
    }

    public void Show(GameOutcome outcome, Action onContinue)
    {
        _onContinue = onContinue;
        _showing = true;
        _history = _turnManager != null ? _turnManager.History : null;
        if (_history == null)
        {
            Continue();
            return;
        }

        var evaluator = _turnManager.CreateEvaluator();
        _decisions = PhaseReport.TopDecisions(_history, DecisionCount, ImpactCriterion.VersusPass, evaluator);

        var totalTurns = _turnManager.Machine != null ? _turnManager.Machine.VictoryTurnCount : TurnMachine.DefaultVictoryTurnCount;
        var turnsPlayed = _turnManager.Machine != null ? _turnManager.Machine.TurnIndex : _history.Turns.Count;

        if (_titleText != null)
            _titleText.text = UIStrings.ReportTitle;
        if (_summaryText != null)
            _summaryText.text = UIStrings.ReportSummary(outcome, turnsPlayed, totalTurns);
        if (_hintText != null)
            _hintText.text = UIStrings.ReportChartHint;
        if (_continueButton != null)
        {
            var label = _continueButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = UIStrings.ReportContinueButton;
        }

        BodyText = BuildBody(outcome, evaluator, turnsPlayed);
        if (_bodyText != null)
            _bodyText.text = BodyText;

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        if (_bodyScroll != null)
            _bodyScroll.verticalNormalizedPosition = 1f;

        Canvas.ForceUpdateCanvases();
        RefreshLegend();
        DrawChart(totalTurns);
        Opened?.Invoke();
    }

    private void Continue()
    {
        Hide();
        var callback = _onContinue;
        _onContinue = null;
        callback?.Invoke();
    }

    private void Hide()
    {
        _showing = false;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void BuildLegend()
    {
        if (_legendRoot == null || _legendButtonTemplate == null)
            return;

        _legendButtonTemplate.gameObject.SetActive(false);
        foreach (var parameter in Parameters)
        {
            var button = Instantiate(_legendButtonTemplate, _legendRoot);
            button.name = "Legend_" + parameter;
            button.gameObject.SetActive(true);
            if (button.image != null && _displayData != null)
                button.image.sprite = _displayData.GetIcon(parameter);

            var swatch = button.transform.Find("Swatch");
            if (swatch != null && swatch.TryGetComponent<Image>(out var swatchImage))
                swatchImage.color = ColorOf(parameter);

            var captured = parameter;
            button.onClick.AddListener(() => ToggleParameter(captured));
            _legendButtons[parameter] = button;
        }
    }

    private void ToggleParameter(CityParameterType parameter)
    {
        if (_hidden.Remove(parameter) == false)
            _hidden.Add(parameter);
        RefreshLegend();
        DrawChart(_turnManager != null && _turnManager.Machine != null ? _turnManager.Machine.VictoryTurnCount : TurnMachine.DefaultVictoryTurnCount);
    }

    private void RefreshLegend()
    {
        foreach (var entry in _legendButtons)
        {
            var group = entry.Value.GetComponent<CanvasGroup>();
            if (group == null)
                group = entry.Value.gameObject.AddComponent<CanvasGroup>();
            group.alpha = _hidden.Contains(entry.Key) ? _hiddenLegendAlpha : 1f;
        }
    }

    private Color ColorOf(CityParameterType parameter)
    {
        var index = (int)parameter;
        return _parameterColors != null && index < _parameterColors.Length ? _parameterColors[index] : Color.white;
    }

    private void DrawChart(int totalTurns)
    {
        if (_chartLines == null || _history == null)
            return;

        var rect = _chartLines.rectTransform.rect;
        totalTurns = Math.Max(1, totalTurns);
        Vector2 Point(int turn, float normalized) => new Vector2(
            rect.xMin + rect.width * turn / totalTurns,
            rect.yMin + rect.height * Mathf.Clamp01(normalized));

        _lineBuffer.Clear();
        foreach (var level in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            AddSegment(Point(0, level), Point(totalTurns, level), 2f, _gridColor, dashed: false);

        var anchorCritical = CriticalLevel(CityStats.AnchorParameter);
        if (anchorCritical >= 0f)
        {
            var level = Normalized(CityStats.AnchorParameter, anchorCritical);
            AddSegment(Point(0, level), Point(totalTurns, level), 3f, _criticalColor, dashed: true);
        }

        // Cartas jogadas marcadas no eixo, e as decisoes de maior impacto mais altas.
        foreach (var turn in _history.Turns)
        {
            if (turn.PlayedCard)
                AddSegment(Point(turn.TurnIndex, 0f), Point(turn.TurnIndex, 0f) + Vector2.up * _markerHeight, 3f, _markerColor, dashed: false);
        }
        foreach (var decision in _decisions)
            AddSegment(Point(decision.TurnIndex, 0f), Point(decision.TurnIndex, 0f) + Vector2.up * _decisionMarkerHeight, 5f, _decisionColor, dashed: false);

        // Bem-estar por ultimo, por cima das outras linhas.
        foreach (var parameter in Parameters)
        {
            if (parameter != CityStats.AnchorParameter)
                AddSeries(parameter, Point, _lineWidth);
        }
        AddSeries(CityStats.AnchorParameter, Point, _anchorLineWidth);

        _chartLines.SetLines(_lineBuffer);
        DrawAxisLabels(totalTurns, Point);
    }

    private void AddSeries(CityParameterType parameter, Func<int, float, Vector2> point, float width)
    {
        if (_hidden.Contains(parameter))
            return;

        var series = PhaseReport.Series(_history, parameter);
        for (var i = 1; i < series.Count; i++)
        {
            var from = point(series[i - 1].Turn, Normalized(parameter, series[i - 1].Value));
            var to = point(series[i].Turn, Normalized(parameter, series[i].Value));
            AddSegment(from, to, width, ColorOf(parameter), dashed: false);
        }
    }

    private void AddSegment(Vector2 from, Vector2 to, float width, Color color, bool dashed)
    {
        _lineBuffer.Add(new UILine { Start = from, End = to, Control = (from + to) * 0.5f, Width = width, Color = color, Dashed = dashed });
    }

    private void DrawAxisLabels(int totalTurns, Func<int, float, Vector2> point)
    {
        if (_axisLabelTemplate == null)
            return;

        var labels = new List<(Vector2 Position, string Text, Color Color)>();
        foreach (var turn in AxisTurns(totalTurns))
            labels.Add((point(turn, 0f) + Vector2.down * 26f, turn.ToString(), Color.white));
        for (var i = 0; i < _decisions.Count; i++)
            labels.Add((point(_decisions[i].TurnIndex, 0f) + Vector2.up * (_decisionMarkerHeight + 18f), (i + 1).ToString(), _decisionColor));

        while (_axisLabels.Count < labels.Count)
        {
            var label = Instantiate(_axisLabelTemplate, _chartLines.rectTransform);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _axisLabels.Add(label);
        }
        for (var i = 0; i < _axisLabels.Count; i++)
        {
            var visible = i < labels.Count;
            _axisLabels[i].gameObject.SetActive(visible);
            if (visible == false)
                continue;
            _axisLabels[i].rectTransform.anchoredPosition = labels[i].Position + _chartLines.rectTransform.rect.center * -1f;
            _axisLabels[i].text = labels[i].Text;
            _axisLabels[i].color = labels[i].Color;
        }
    }

    private static IEnumerable<int> AxisTurns(int totalTurns)
    {
        yield return 1;
        for (var turn = 5; turn < totalTurns; turn += 5)
            yield return turn;
        if (totalTurns > 1)
            yield return totalTurns;
    }

    private CityParameterConfig? ConfigOf(CityParameterType parameter)
    {
        if (_cityStatsManager == null)
            return null;
        foreach (var config in _cityStatsManager.ParameterConfigs)
        {
            if (config.Parameter == parameter)
                return config;
        }
        return null;
    }

    private float CriticalLevel(CityParameterType parameter)
    {
        var config = ConfigOf(parameter);
        return config.HasValue ? config.Value.CriticalLevel : -1f;
    }

    // Posicao do valor entre o minimo e o maximo da escala.
    private float Normalized(CityParameterType parameter, float value)
    {
        var config = ConfigOf(parameter);
        var min = config.HasValue ? config.Value.MinValue : 0f;
        var max = config.HasValue ? config.Value.MaxValue : 100f;
        return max > min ? (value - min) / (max - min) : 0f;
    }

    private string BuildBody(GameOutcome outcome, TurnEvaluator evaluator, int turnsPlayed)
    {
        var body = new StringBuilder();

        body.Append("<b>").Append(UIStrings.ReportDecisionsHeader).Append("</b>\n");
        if (_decisions.Count == 0)
            body.Append(UIStrings.ReportNoDecisions).Append('\n');
        for (var i = 0; i < _decisions.Count; i++)
        {
            var decision = _decisions[i];
            body.Append(UIStrings.ReportDecisionLine(i + 1, decision.TurnIndex, CardName(decision.CardId), decision.Impact >= 0.0)).Append('\n');
            var effects = EffectsLine(FindTurn(decision.TurnIndex));
            if (effects.Length > 0)
                body.Append("<indent=6%>").Append(effects).Append("</indent>\n");
        }

        body.Append("\n<b>").Append(UIStrings.ReportCollapseHeader(outcome)).Append("</b>\n");
        var chain = PhaseReport.CollapseChain(_history, evaluator.Rules);
        if (chain.Count == 0)
            body.Append(UIStrings.ReportNoCollapse).Append('\n');
        foreach (var span in chain)
        {
            var penalty = Magnitude(CityStats.AnchorParameter, span.WellbeingPenalty);
            body.Append("• ").Append(UIStrings.ReportCollapseLine(span.Parameter, span.FirstTurn, span.TurnsPenalized, penalty)).Append('\n');
        }
        if (outcome == GameOutcome.GameOver && _cityStatsManager != null && _cityStatsManager.Stats.IsAnchorCritical())
            body.Append("• ").Append(UIStrings.ReportAnchorCritical(turnsPlayed)).Append('\n');

        body.Append("\n<b>").Append(UIStrings.ReportAlternativeHeader).Append("</b>\n");
        var miss = PhaseReport.BiggestMiss(_history, evaluator);
        body.Append(miss.HasValue
            ? UIStrings.ReportAlternative(miss.Value.TurnIndex, CardName(miss.Value.BetterCardId), CardName(miss.Value.PlayedCardId), miss.Value.AvoidsGameOver)
            : UIStrings.ReportNoAlternative).Append('\n');

        body.Append("\n<size=80%>").Append(UIStrings.ReportExplanation).Append("</size>");
        return body.ToString();
    }

    private PhaseTurnRecord FindTurn(int turnIndex)
    {
        foreach (var turn in _history.Turns)
        {
            if (turn.TurnIndex == turnIndex)
                return turn;
        }
        return null;
    }

    // Os maiores efeitos diretos da carta, custo incluido.
    private string EffectsLine(PhaseTurnRecord turn)
    {
        if (turn == null)
            return string.Empty;

        var effects = new List<(CityParameterType Parameter, float Amount)>();
        foreach (var parameter in Parameters)
        {
            var amount = turn.GetCardEffect(parameter);
            if (Math.Abs(amount) >= MinimumShownAmount)
                effects.Add((parameter, amount));
        }
        effects.Sort((a, b) => Math.Abs(b.Amount).CompareTo(Math.Abs(a.Amount)));
        if (effects.Count > EffectsPerDecision)
            effects.RemoveRange(EffectsPerDecision, effects.Count - EffectsPerDecision);

        var parts = new List<string>();
        foreach (var effect in effects)
            parts.Add($"{UIStrings.ParameterIcon(effect.Parameter)} {Colored(effect.Parameter, effect.Amount)}");
        return string.Join("  ", parts);
    }

    private string CardName(string cardId)
    {
        if (_turnManager != null)
        {
            foreach (var card in _turnManager.CardPool)
            {
                if (card != null && card.name == cardId)
                    return card.CardName;
            }
        }
        return cardId;
    }

    private string Colored(CityParameterType parameter, float amount)
    {
        var sign = amount < 0f ? "−" : "+";
        var color = amount < 0f ? NegativeColor : PositiveColor;
        return $"<b><color={color}>{sign}{Magnitude(parameter, amount)}</color></b>";
    }

    private string Magnitude(CityParameterType parameter, float amount)
    {
        return _displayData != null
            ? _displayData.FormatAmount(parameter, Math.Abs(amount), smallUnit: true, extraDecimals: 1)
            : Math.Abs(amount).ToString("0.0");
    }
}
