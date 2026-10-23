using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup "Por que mudou?": de onde veio a variacao de um parametro no ultimo turno.
public class ChangeBreakdownPopupView : MonoBehaviour
{
    // Tons claros, legiveis sobre o fundo azul do popup.
    private const string PositiveColor = "#C6FF6B";
    private const string NegativeColor = "#FFC2C2";

    // Parcelas menores que isso (no indice) entram em "outras influencias".
    private const float MinimumShownAmount = 0.05f;

    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _closeButton;

    // Fundo escurecido de tela inteira; tocar nele tambem fecha.
    [SerializeField] private Button _backdropButton;
    [SerializeField] private ParameterDisplayData _displayData;

    // Abre o mapa de relacoes ja focado no parametro mostrado.
    [SerializeField] private Button _mapButton;
    [SerializeField] private RelationMapView _relationMap;

    [Tooltip("Quantas parcelas aparecem antes de agrupar o resto em outras influencias.")]
    [SerializeField, Min(1)] private int _maxLines = 6;

    private CityParameterType _shownParameter;

    public event Action Opened;

    private void Start()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Hide);
            var closeText = _closeButton.GetComponentInChildren<TMP_Text>();
            if (closeText != null)
                closeText.text = UIStrings.OkButton;
        }
        if (_backdropButton != null)
            _backdropButton.onClick.AddListener(Hide);
        if (_mapButton != null)
        {
            _mapButton.onClick.AddListener(OpenRelationMap);
            _mapButton.gameObject.SetActive(_relationMap != null);
        }

        Hide();
    }

    public void Show(CityParameterType parameter)
    {
        _shownParameter = parameter;
        if (_mapButton != null)
        {
            var mapText = _mapButton.GetComponentInChildren<TMP_Text>();
            if (mapText != null)
                mapText.text = UIStrings.BreakdownMapButton;
        }
        if (_titleText != null)
            _titleText.text = UIStrings.BreakdownTitle(parameter);
        if (_bodyText != null)
            _bodyText.text = BuildBody(parameter);
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        Opened?.Invoke();
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void OpenRelationMap()
    {
        Hide();
        _relationMap.Show(_shownParameter);
    }

    private string BuildBody(CityParameterType parameter)
    {
        var breakdown = _turnManager != null ? _turnManager.LastBreakdown : null;
        if (breakdown == null || breakdown.IsComplete == false)
            return UIStrings.BreakdownNoData;

        var body = new StringBuilder();
        body.Append(UIStrings.BreakdownTurnHeader(
            breakdown.TurnIndex,
            Level(parameter, breakdown.GetStartValue(parameter)),
            Level(parameter, breakdown.GetEndValue(parameter))));
        body.Append('\n');
        AppendLine(body, UIStrings.BreakdownTotalChange, Colored(parameter, breakdown.GetDelta(parameter)));
        body.Append('\n');

        if (parameter == CityStats.AnchorParameter && breakdown.HasWellbeing)
            AppendWellbeing(body, breakdown);
        else
            AppendContributions(body, breakdown, parameter);

        return body.ToString().TrimEnd();
    }

    private void AppendContributions(StringBuilder body, TurnBreakdown breakdown, CityParameterType parameter)
    {
        // Junta as parcelas da mesma origem (ex. varias cartas, ou custo e efeito da mesma carta).
        var grouped = new Dictionary<(ContributionSource, CityParameterType), float>();
        foreach (var contribution in breakdown.For(parameter))
        {
            var key = (contribution.Source, contribution.Source == ContributionSource.Interaction ? contribution.From : default);
            grouped.TryGetValue(key, out var sum);
            grouped[key] = sum + contribution.Amount;
        }

        var lines = new List<KeyValuePair<(ContributionSource Source, CityParameterType From), float>>(grouped);
        lines.Sort((a, b) => Math.Abs(b.Value).CompareTo(Math.Abs(a.Value)));

        var shown = 0;
        var others = 0f;
        foreach (var line in lines)
        {
            if (shown < _maxLines && IsVisible(parameter, line.Value))
            {
                AppendLine(body, UIStrings.ContributionLabel(line.Key.Source, line.Key.From, 0), Colored(parameter, line.Value));
                shown++;
            }
            else
            {
                others += line.Value;
            }
        }

        var othersVisible = IsVisible(parameter, others);
        if (othersVisible)
            AppendLine(body, UIStrings.BreakdownOthers, Colored(parameter, others));
        if (shown == 0 && othersVisible == false)
            body.Append(UIStrings.BreakdownNoChange);
    }

    private void AppendWellbeing(StringBuilder body, TurnBreakdown breakdown)
    {
        const CityParameterType anchor = CityStats.AnchorParameter;
        var composition = breakdown.Wellbeing;

        AppendLine(body, UIStrings.WellbeingAverage, Level(anchor, composition.PositiveAverage));
        if (IsVisible(anchor, composition.Crowding))
            AppendLine(body, UIStrings.WellbeingCrowding, Colored(anchor, -composition.Crowding));
        if (IsVisible(anchor, composition.AnchorBonus))
            AppendLine(body, UIStrings.WellbeingBonus, Colored(anchor, composition.AnchorBonus));
        if (composition.CollapsedCount > 0)
            AppendLine(body, UIStrings.ContributionLabel(ContributionSource.CollapsePenalty, default, composition.CollapsedCount), Colored(anchor, -composition.CollapsePenalty));
        if (IsVisible(anchor, composition.Clamp))
            AppendLine(body, UIStrings.ContributionLabel(ContributionSource.Clamp, default, 0), Colored(anchor, composition.Clamp));
        AppendLine(body, UIStrings.WellbeingAfterResolution, Level(anchor, composition.Total));

        var eventAmount = breakdown.Sum(anchor, ContributionSource.Event);
        if (IsVisible(anchor, eventAmount))
            AppendLine(body, UIStrings.WellbeingEventThisTurn, Colored(anchor, eventAmount));

        body.Append("\n<size=80%>").Append(UIStrings.WellbeingExplanation).Append("</size>");
    }

    // Rotulo a esquerda e valor alinhado numa coluna a direita.
    private static void AppendLine(StringBuilder body, string label, string value)
    {
        body.Append(label).Append("<pos=64%>").Append(value).Append('\n');
    }

    private string Level(CityParameterType parameter, float value)
    {
        return _displayData != null ? _displayData.FormatValue(parameter, value, smallUnit: true) : value.ToString("0.0");
    }

    private string Colored(CityParameterType parameter, float amount)
    {
        var sign = amount < 0f ? "−" : "+";
        var color = amount < 0f ? NegativeColor : PositiveColor;
        return $"<b><color={color}>{sign}{Magnitude(parameter, amount, smallUnit: true)}</color></b>";
    }

    private string Magnitude(CityParameterType parameter, float amount, bool smallUnit)
    {
        return _displayData != null
            ? _displayData.FormatAmount(parameter, Math.Abs(amount), smallUnit, extraDecimals: 1)
            : Math.Abs(amount).ToString("0.0");
    }

    // Esconde parcelas pequenas demais ou que arredondam pra zero na unidade escolhida.
    private bool IsVisible(CityParameterType parameter, float amount)
    {
        if (Math.Abs(amount) < MinimumShownAmount)
            return false;

        // So o primeiro numero do texto conta: unidades como "/10" tem algarismos proprios.
        var text = Magnitude(parameter, amount, smallUnit: false);
        var start = 0;
        while (start < text.Length && char.IsDigit(text[start]) == false)
            start++;
        for (var i = start; i < text.Length && (char.IsDigit(text[i]) || text[i] == ',' || text[i] == '.'); i++)
        {
            if (text[i] >= '1' && text[i] <= '9')
                return true;
        }
        return false;
    }
}
