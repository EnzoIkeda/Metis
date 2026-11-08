using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Painel dos objetivos do cenario na cidade; so existe em modo cenario.
public class ScenarioObjectivesView : MonoBehaviour
{
    // Tons escuros, legiveis sobre a janela clara do popup.
    private const string AchievedColor = "#1E7A1E";
    private const string FailedColor = "#B3261E";
    private const string PendingColor = "#444444";

    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private ParameterDisplayData _displayData;
    [SerializeField] private WelcomePopupView _welcome;

    [SerializeField] private Button _openButton;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _hintText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _closeButton;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    public string BodyText => _bodyText != null ? _bodyText.text : string.Empty;

    private void Start()
    {
        var scenario = _turnManager != null ? _turnManager.Scenario : null;
        if (_openButton != null)
            _openButton.gameObject.SetActive(scenario != null);
        Hide();
        if (scenario == null)
            return;

        if (_openButton != null)
            _openButton.onClick.AddListener(Show);
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);
        if (_titleText != null)
            _titleText.text = UIStrings.ScenarioObjectivesTitle;
        if (_hintText != null)
            _hintText.text = UIStrings.ScenarioObjectivesHint;

        // Ao comecar, os objetivos aparecem logo depois das boas-vindas.
        if (_welcome != null && _turnManager.ResumedFromSave == false)
            _welcome.Closed += Show;
        _turnManager.OnTurnFinished += HandleTurnFinished;
    }

    private void OnDestroy()
    {
        if (_welcome != null)
            _welcome.Closed -= Show;
        if (_turnManager != null)
            _turnManager.OnTurnFinished -= HandleTurnFinished;
    }

    public void Show()
    {
        Refresh();
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void HandleTurnFinished(PhaseTurnRecord turn)
    {
        if (IsOpen)
            Refresh();
    }

    private void Refresh()
    {
        var scenario = _turnManager.Scenario;
        if (scenario == null || _bodyText == null)
            return;

        var states = ScenarioObjectiveEvaluator.EvaluateAll(scenario.Objectives, _turnManager.History.Turns, GameOutcome.None);
        var body = new StringBuilder();
        for (var i = 0; i < states.Count; i++)
        {
            var objective = scenario.Objectives[i];
            var current = _displayData != null
                ? _displayData.FormatValue(objective.Parameter, _cityStatsManager.Stats.GetValue(objective.Parameter), smallUnit: false)
                : _cityStatsManager.Stats.GetValue(objective.Parameter).ToString("0");
            body.Append(UIStrings.ScenarioObjectiveLine(_displayData, objective)).Append('\n');
            body.Append("<size=85%>").Append(UIStrings.ScenarioCurrentValue(current)).Append("  <b><color=").Append(ColorOf(states[i])).Append('>')
                .Append(UIStrings.ScenarioStateLabel(states[i])).Append("</color></b></size>\n\n");
        }
        _bodyText.text = body.ToString().TrimEnd();
    }

    private static string ColorOf(ObjectiveState state)
    {
        switch (state)
        {
            case ObjectiveState.Achieved: return AchievedColor;
            case ObjectiveState.Failed: return FailedColor;
            default: return PendingColor;
        }
    }
}
