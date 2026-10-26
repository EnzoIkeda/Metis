using System;
using UnityEngine;

// Ordem do fim de fase: o relatorio abre primeiro, e so depois dele seguem o popup de desfecho e a recompensa.
public class PhaseEndFlow : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private PhaseReportView _report;

    // O jogador saiu do relatorio; quem mostra desfecho e recompensa ouve aqui em vez do fim de jogo direto.
    public event Action<GameOutcome> OnContinue;

    private void Start()
    {
        _turnManager.Machine.OnGameEnded += HandleGameEnded;
    }

    private void OnDestroy()
    {
        if (_turnManager != null && _turnManager.Machine != null)
            _turnManager.Machine.OnGameEnded -= HandleGameEnded;
    }

    private void HandleGameEnded(GameOutcome outcome)
    {
        SfxPlayer.Play(outcome == GameOutcome.Victory ? SoundEffect.Victory : SoundEffect.Defeat);

        if (_report != null)
            _report.Show(outcome, () => OnContinue?.Invoke(outcome));
        else
            OnContinue?.Invoke(outcome);
    }
}
