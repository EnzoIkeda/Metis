using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup de fim de jogo, reutilizavel pra vitoria ou derrota conforme o desfecho configurado.
public class GameOutcomePopupView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private GameOutcome _outcomeToShowFor;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _closeButton;

    private void Start()
    {
        _turnManager.Machine.OnGameEnded += HandleGameEnded;
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);

        if (_titleText != null)
            _titleText.text = _outcomeToShowFor == GameOutcome.Victory ? UIStrings.VictoryTitle : UIStrings.GameOverTitle;
        if (_messageText != null)
            _messageText.text = _outcomeToShowFor == GameOutcome.Victory ? UIStrings.VictoryMessage : UIStrings.GameOverMessage;

        Hide();
    }

    private void OnDisable()
    {
        if (_turnManager != null && _turnManager.Machine != null)
            _turnManager.Machine.OnGameEnded -= HandleGameEnded;
    }

    private void HandleGameEnded(GameOutcome outcome)
    {
        if (outcome != _outcomeToShowFor)
            return;

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
