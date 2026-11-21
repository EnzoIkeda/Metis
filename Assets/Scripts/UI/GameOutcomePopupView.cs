using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Popup de fim de jogo, reutilizavel pra vitoria ou derrota conforme o desfecho configurado.
public class GameOutcomePopupView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;

    [Tooltip("Abre depois do relatorio de fim de fase; vazio, abre direto no fim de jogo.")]
    [SerializeField] private PhaseEndFlow _endFlow;
    [SerializeField] private GameOutcome _outcomeToShowFor;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _closeButton;

    [Tooltip("Fim de rodada (derrota ou vitoria contra o chefe) tem sequencia e resumo proprios; aqui nao abre.")]
    [SerializeField] private RunEndFlow _runEnd;

    private void Start()
    {
        if (_endFlow != null)
            _endFlow.OnContinue += HandleGameEnded;
        else
            _turnManager.Machine.OnGameEnded += HandleGameEnded;
        if (_closeButton != null)
            _closeButton.onClick.AddListener(HandleCloseClicked);

        if (_titleText != null)
            _titleText.text = OutcomeTitle();
        if (_messageText != null)
            _messageText.text = OutcomeMessage();

        Hide();
    }

    private bool IsBossVictory => _outcomeToShowFor == GameOutcome.Victory && _turnManager != null && _turnManager.Boss != null;

    private string OutcomeTitle()
    {
        if (IsBossVictory)
            return UIStrings.RunVictoryTitle;
        return _outcomeToShowFor == GameOutcome.Victory ? UIStrings.VictoryTitle : UIStrings.GameOverTitle;
    }

    private string OutcomeMessage()
    {
        if (_outcomeToShowFor != GameOutcome.Victory)
            return UIStrings.GameOverMessage;
        if (IsBossVictory)
            return _turnManager.BossDefinition.Victory;
        var theme = _turnManager != null ? _turnManager.Theme : null;
        return theme != null ? theme.Victory : UIStrings.VictoryMessage;
    }

    private void OnDisable()
    {
        if (_endFlow != null)
            _endFlow.OnContinue -= HandleGameEnded;
        if (_turnManager != null && _turnManager.Machine != null)
            _turnManager.Machine.OnGameEnded -= HandleGameEnded;
    }

    private void HandleGameEnded(GameOutcome outcome)
    {
        if (outcome != _outcomeToShowFor)
            return;
        if (_runEnd != null && _runEnd.Handles(outcome))
            return;

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        // Com o relatorio, o som ja tocou quando a fase acabou.
        if (_endFlow == null)
            SfxPlayer.Play(outcome == GameOutcome.Victory ? SoundEffect.Victory : SoundEffect.Defeat);
    }

    // Derrota encerra a rodada inteira; vitoria so fecha o popup e segue pro fluxo de recompensa.
    private void HandleCloseClicked()
    {
        Hide();

        if (SessionMode.IsScenario)
        {
            SceneManager.LoadScene(MetaProgressionManager.ScenarioSelectionSceneName);
            return;
        }

        // A rodada ja foi apagada no fim da fase do chefe.
        if (_outcomeToShowFor == GameOutcome.GameOver || IsBossVictory)
        {
            MetaProgressionManager.ResetRun();
            SceneManager.LoadScene("MainMenu");
        }
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
