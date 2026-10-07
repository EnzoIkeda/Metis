using UnityEngine;

// Toca o alerta quando um parametro entra em Colapso, uma vez por entrada.
public class CollapseAlertSound : MonoBehaviour
{
    [SerializeField] private CityStatsManager _cityStatsManager;

    private readonly CollapseTracker _tracker = new CollapseTracker();
    private CityStats _stats;

    // Os parametros da cidade so existem depois da inicializacao do gerenciador.
    private void Start()
    {
        if (_cityStatsManager == null || _cityStatsManager.Stats == null)
            return;

        _stats = _cityStatsManager.Stats;
        _stats.OnParameterChanged += HandleParameterChanged;
    }

    private void OnDestroy()
    {
        if (_stats != null)
            _stats.OnParameterChanged -= HandleParameterChanged;
    }

    private void HandleParameterChanged(CityParameterType parameter, float value)
    {
        if (_tracker.Update(parameter, _stats.IsInCollapse(parameter)))
            SfxPlayer.Play(SoundEffect.ParameterCollapse);
    }
}
