using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup de fim de jogo
/// </summary>
public class GameOutcomePopupView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private GameOutcome _outcomeToShowFor;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _closeButton;

    [SerializeField] private string _title = "Fim de Jogo";
    [SerializeField, TextArea(3, 8)] private string _message = "";

    private void Start()
    {
        _turnManager.Machine.OnGameEnded += HandleGameEnded;
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);

        if (_titleText != null)
            _titleText.text = _title;
        if (_messageText != null)
            _messageText.text = _message;

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
