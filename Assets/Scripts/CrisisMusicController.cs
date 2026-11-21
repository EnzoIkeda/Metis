using UnityEngine;

// Troca a trilha pela faixa de crise enquanto algum parametro estiver na zona Crise.
// So avalia em momentos estaveis: ao carregar a fase e no fim de cada turno, nunca no meio da resolucao.
public class CrisisMusicController : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CityStatsManager _cityStatsManager;

    private readonly CrisisMusicRule _rule = new CrisisMusicRule();
    private TurnMachine _machine;

    public bool IsCrisis => _rule.IsActive;

    // Roda depois do gerenciador de turno, que cria a maquina e aplica o comeco da fase no proprio Start.
    private void Start()
    {
        var player = MusicPlayer.Instance;
        if (player != null)
            player.StartPhase();

        if (_turnManager != null)
        {
            _turnManager.OnTurnFinished += HandleTurnFinished;
            _machine = _turnManager.Machine;
            if (_machine != null)
                _machine.OnGameEnded += HandleGameEnded;
        }
        if (_machine == null || _machine.Outcome == GameOutcome.None)
            Evaluate();
    }

    private void OnDestroy()
    {
        if (_turnManager != null)
            _turnManager.OnTurnFinished -= HandleTurnFinished;
        if (_machine != null)
            _machine.OnGameEnded -= HandleGameEnded;

        // Saindo da cidade pela pausa, a crise nao pode seguir tocando no menu.
        _rule.Reset();
        var player = MusicPlayer.Instance;
        if (player != null)
            player.EndPhase();
    }

    private void HandleTurnFinished(PhaseTurnRecord record)
    {
        // O turno que encerra a fase passa direto ao desfecho, sem entrar na crise por um instante.
        var stats = _cityStatsManager != null ? _cityStatsManager.Stats : null;
        if (_machine != null && stats != null && (stats.IsAnchorCritical() || _machine.TurnIndex >= _machine.VictoryTurnCount))
            return;
        Evaluate();
    }

    private void HandleGameEnded(GameOutcome outcome)
    {
        _rule.Reset();
        var player = MusicPlayer.Instance;
        if (player != null)
            player.EndPhase();
    }

    private void Evaluate()
    {
        var stats = _cityStatsManager != null ? _cityStatsManager.Stats : null;
        if (stats == null)
            return;

        var active = _rule.Update(stats.GetValue, stats.Interactions.CrisisThreshold);
        var player = MusicPlayer.Instance;
        if (player != null)
            player.SetCrisis(active);
    }
}
