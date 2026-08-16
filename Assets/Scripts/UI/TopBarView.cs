using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Barra superior reativa, observer que sincroniza com os parametros
/// </summary>
public class TopBarView : MonoBehaviour
{
    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private StatRowView _rowPrefab;
    [SerializeField] private Transform _rowContainer;

    private readonly Dictionary<CityParameterType, StatRowView> _rows = new Dictionary<CityParameterType, StatRowView>();
    private CityStats _stats;

    private void Start()
    {
        _stats = _cityStatsManager.Stats;

        foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
        {
            var row = Instantiate(_rowPrefab, _rowContainer);
            row.SetLabel(parameter.GetDisplayName());
            _rows[parameter] = row;
            Refresh(parameter);
        }

        _stats.OnParameterChanged += HandleParameterChanged;
    }

    private void OnDisable()
    {
        if (_stats != null)
            _stats.OnParameterChanged -= HandleParameterChanged;
    }

    private void HandleParameterChanged(CityParameterType parameter, float value)
    {
        Refresh(parameter);
    }

    private void Refresh(CityParameterType parameter)
    {
        if (_rows.TryGetValue(parameter, out var row) == false)
            return;

        row.SetValue(_stats.GetValue(parameter), _stats.IsCritical(parameter));
    }
}
