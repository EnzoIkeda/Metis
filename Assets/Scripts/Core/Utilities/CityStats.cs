using System;
using System.Collections.Generic;

/// <summary>
/// Configuracao inicial de um parametro: valor de partida e limites inferiores e superiores
/// Por enquanto MinValue/MaxValue tambem sao o limite critico
/// </summary>
[Serializable]
public struct CityParameterConfig
{
    public CityParameterType Parameter;
    public float InitialValue;
    public float MinValue;
    public float MaxValue;
}

/// <summary>
/// Estado dos 9 parametros da cidade: valores, clamp e nivel critico
/// Notifica mudancas via Observer
/// </summary>
public class CityStats
{
    private readonly Dictionary<CityParameterType, float> _values = new Dictionary<CityParameterType, float>();
    private readonly Dictionary<CityParameterType, float> _minValues = new Dictionary<CityParameterType, float>();
    private readonly Dictionary<CityParameterType, float> _maxValues = new Dictionary<CityParameterType, float>();

    private bool _gameOverRaised;

    public event Action<CityParameterType, float> OnParameterChanged;
    public event Action<CityParameterType> OnParameterCritical;
    public event Action OnGameOver;

    public CityStats(IEnumerable<CityParameterConfig> config)
    {
        foreach (var entry in config)
        {
            _minValues[entry.Parameter] = entry.MinValue;
            _maxValues[entry.Parameter] = entry.MaxValue;
            _values[entry.Parameter] = Clamp(entry.Parameter, entry.InitialValue);
        }
    }

    public float GetValue(CityParameterType parameter)
    {
        return _values.TryGetValue(parameter, out var value) ? value : 0f;
    }

    public bool IsCritical(CityParameterType parameter)
    {
        if (_values.ContainsKey(parameter) == false)
            return false;

        var value = _values[parameter];
        return value <= _minValues[parameter] || value >= _maxValues[parameter];
    }

    public bool AnyParameterCritical()
    {
        foreach (var parameter in _values.Keys)
        {
            if (IsCritical(parameter))
                return true;
        }
        return false;
    }

    /// <summary> 
    /// Aplica um unico modificador a um parametro
    /// </summary>
    public void ApplyModifier(StatModifier modifier)
    {
        if (_values.ContainsKey(modifier.Parameter) == false)
            return;

        SetValue(modifier.Parameter, _values[modifier.Parameter] + modifier.Amount);
    }

    public void ApplyModifiers(IEnumerable<StatModifier> modifiers)
    {
        foreach (var modifier in modifiers)
            ApplyModifier(modifier);
    }

    /// <summary>
    /// Formulas de interacao entre parametros, chamado na fase de resolucao do turno
    /// </summary>
    public void RecomputeDerivedParameters()
    {
        var mediaPositivos = (GetValue(CityParameterType.Mobilidade)
            + GetValue(CityParameterType.Saude)
            + GetValue(CityParameterType.Seguranca)
            + GetValue(CityParameterType.Sustentabilidade)) / 4f;

        var populacaoBaseline = (_minValues[CityParameterType.Populacao] + _maxValues[CityParameterType.Populacao]) / 2f;
        var adensamentoPopulacional = Math.Max(0f, GetValue(CityParameterType.Populacao) - populacaoBaseline);

        SetValue(CityParameterType.BemEstar, mediaPositivos - adensamentoPopulacional);
    }

    private void SetValue(CityParameterType parameter, float rawValue)
    {
        if (_values.ContainsKey(parameter) == false)
            return;

        var clamped = Clamp(parameter, rawValue);
        _values[parameter] = clamped;
        OnParameterChanged?.Invoke(parameter, clamped);

        if (IsCritical(parameter))
        {
            OnParameterCritical?.Invoke(parameter);
            if (_gameOverRaised == false)
            {
                _gameOverRaised = true;
                OnGameOver?.Invoke();
            }
        }
    }

    private float Clamp(CityParameterType parameter, float value)
    {
        var min = _minValues[parameter];
        var max = _maxValues[parameter];
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
