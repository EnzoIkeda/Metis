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

    private readonly Dictionary<CityParameterType, StatRowView> _rows = new Dictionary<CityParameterType, StatRowView>();
    private CityStats _stats;

    private void Start()
    {
        _stats = _cityStatsManager.Stats;
        BuildRows();
        _stats.OnParameterChanged += HandleParameterChanged;
    }

    private void OnDisable()
    {
        if (_stats != null)
            _stats.OnParameterChanged -= HandleParameterChanged;
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
