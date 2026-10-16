using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Barra superior reativa que distribui os parametros em varias linhas.
public class TopBarView : MonoBehaviour
{
    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private StatRowView _rowPrefab;
    [SerializeField] private RectTransform _rowsContainer;
    [SerializeField] private float _rowSpacing = 4f;
    [SerializeField] private int _rowCount = 3;
    [SerializeField] private ParameterDisplayData _displayData;

    // Fonte do registro do ultimo turno e popup que explica a variacao de cada parametro.
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private ChangeBreakdownPopupView _breakdownPopup;

    // Variacoes menores que isso (no indice) nao mostram seta.
    [SerializeField] private float _deltaThreshold = 0.05f;
    [SerializeField] private Color _riseColor = new Color(0.36f, 0.85f, 0.38f);
    [SerializeField] private Color _fallColor = new Color(0.95f, 0.33f, 0.3f);

    private readonly Dictionary<CityParameterType, StatRowView> _rows = new Dictionary<CityParameterType, StatRowView>();
    private CityStats _stats;

    private void Start()
    {
        _stats = _cityStatsManager.Stats;
        BuildRows();
        _stats.OnParameterChanged += HandleParameterChanged;
        UnitDisplaySettings.OnChanged += RefreshAll;
        LocalizationManager.OnLanguageChanged += RefreshAll;
        if (_turnManager != null)
        {
            _turnManager.OnTurnBreakdownReady += HandleBreakdownReady;
            if (_turnManager.LastBreakdown != null)
                HandleBreakdownReady(_turnManager.LastBreakdown);
        }
    }

    private void OnDisable()
    {
        if (_stats != null)
            _stats.OnParameterChanged -= HandleParameterChanged;
        UnitDisplaySettings.OnChanged -= RefreshAll;
        LocalizationManager.OnLanguageChanged -= RefreshAll;
        if (_turnManager != null)
            _turnManager.OnTurnBreakdownReady -= HandleBreakdownReady;
    }

    private void BuildRows()
    {
        var parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));
        var rowCount = Mathf.Max(1, _rowCount);
        var itemsPerRow = Mathf.CeilToInt(parameters.Length / (float)rowCount);

        Transform currentRow = null;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i % itemsPerRow == 0)
                currentRow = CreateRow();

            var row = Instantiate(_rowPrefab, currentRow);
            row.SetLabel(parameters[i].GetDisplayName());
            row.SetIcon(_displayData != null ? _displayData.GetIcon(parameters[i]) : null);
            var parameter = parameters[i];
            row.SetClickHandler(() => ShowBreakdown(parameter));
            _rows[parameters[i]] = row;
            Refresh(parameters[i]);
        }
    }

    private Transform CreateRow()
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(_rowsContainer, false);

        var group = go.GetComponent<HorizontalLayoutGroup>();
        group.childAlignment = TextAnchor.MiddleLeft;
        group.spacing = _rowSpacing;
        // Estica os itens da linha pra preencher toda a largura, sem sobra do lado direito.
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = true;
        // Precisa controlar largura e altura pra realmente aplicar o tamanho aos filhos.
        group.childControlWidth = true;
        group.childControlHeight = true;

        return go.transform;
    }

    private void ShowBreakdown(CityParameterType parameter)
    {
        if (_breakdownPopup != null)
            _breakdownPopup.Show(parameter);
    }

    // Seta de cada linha com a direcao da variacao do turno que acabou.
    private void HandleBreakdownReady(TurnBreakdown breakdown)
    {
        foreach (var entry in _rows)
        {
            var delta = breakdown.GetDelta(entry.Key);
            var direction = Mathf.Abs(delta) < _deltaThreshold ? 0 : (delta > 0f ? 1 : -1);
            entry.Value.SetDelta(direction, direction > 0 ? _riseColor : _fallColor);
        }
    }

    private void HandleParameterChanged(CityParameterType parameter, float value)
    {
        Refresh(parameter);
    }

    // Troca de unidade ou de idioma redesenha todas as linhas na hora.
    private void RefreshAll()
    {
        foreach (var parameter in _rows.Keys)
            Refresh(parameter);
    }

    private void Refresh(CityParameterType parameter)
    {
        if (_rows.TryGetValue(parameter, out var row) == false)
            return;

        var value = _stats.GetValue(parameter);
        var text = _displayData != null ? _displayData.FormatValue(parameter, value, smallUnit: true) : value.ToString("0");
        row.SetValue(text, _stats.IsCritical(parameter));
    }
}
