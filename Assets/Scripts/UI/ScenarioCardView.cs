using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cartao de um cenario na tela de selecao.
public class ScenarioCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _starsText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _playButton;
    [SerializeField] private TMP_Text _playButtonText;
    [SerializeField] private Button _restartButton;
    [SerializeField] private TMP_Text _restartButtonText;

    public void Bind(ScenarioData scenario, ParameterDisplayData display, int bestStars, bool inProgress, Action play, Action restart)
    {
        _nameText.text = scenario.Theme.ThemeName;
        _starsText.text = UIStrings.ScenarioBestStars(bestStars, ScenarioObjectiveEvaluator.MaxStars(scenario.Objectives.Count));

        var lines = new System.Text.StringBuilder();
        lines.Append(scenario.Theme.Summary).Append("\n<size=85%><i>").Append(scenario.Theme.Inspiration).Append("</i></size>\n");
        foreach (var objective in scenario.Objectives)
            lines.Append("\n• ").Append(UIStrings.ScenarioObjectiveLine(display, objective));
        lines.Append("\n\n<size=85%>")
            .Append(scenario.FreeArchetype ? UIStrings.ScenarioFreeDeck : UIStrings.ScenarioFixedDeck(scenario.Archetype))
            .Append("</size>");
        _bodyText.text = lines.ToString();

        _playButtonText.text = inProgress ? UIStrings.MainMenuContinue : UIStrings.ScenarioPlayButton;
        _playButton.onClick.AddListener(() => play());

        _restartButton.gameObject.SetActive(inProgress);
        _restartButtonText.text = UIStrings.ScenarioRestartButton;
        _restartButton.onClick.AddListener(() => restart());
    }
}
