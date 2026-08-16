using TMPro;
using UnityEngine;

/// <summary>
/// Contador de turno reativo
/// </summary>
public class TurnCounterView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private TMP_Text _turnText;

    private void Start()
    {
        _turnManager.Machine.OnTurnAdvanced += HandleTurnAdvanced;
        Refresh(_turnManager.Machine.TurnIndex);
    }

    private void OnDisable()
    {
        if (_turnManager != null && _turnManager.Machine != null)
            _turnManager.Machine.OnTurnAdvanced -= HandleTurnAdvanced;
    }

    private void HandleTurnAdvanced(int turnIndex)
    {
        Refresh(turnIndex);
    }

    private void Refresh(int turnIndex)
    {
        if (_turnText != null)
            _turnText.text = $"Turno {turnIndex}/{TurnMachine.VictoryTurnCount}";
    }
}
