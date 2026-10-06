using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Botao de passar a vez, visivel so quando nenhuma carta comum da mao pode ser jogada.
public class PassTurnButtonView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;

    private void Start()
    {
        if (_label != null)
            _label.text = UIStrings.PassTurnButton;
        _button.onClick.AddListener(HandleClicked);
        _turnManager.Hand.OnHandChanged += Refresh;
        _turnManager.Machine.OnPhaseChanged += HandlePhaseChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClicked);
        if (_turnManager != null && _turnManager.Hand != null)
            _turnManager.Hand.OnHandChanged -= Refresh;
        if (_turnManager != null && _turnManager.Machine != null)
            _turnManager.Machine.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(TurnPhase phase)
    {
        Refresh();
    }

    private void HandleClicked()
    {
        _turnManager.PassTurn();
        Refresh();
    }

    private void Refresh()
    {
        _button.gameObject.SetActive(_turnManager.CanPassTurn);
    }
}
