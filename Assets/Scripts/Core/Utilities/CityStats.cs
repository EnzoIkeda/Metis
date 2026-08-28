using System;
using System.Collections.Generic;

// Configuracao inicial de um parametro: valor de partida, limites, nivel critico e deriva por turno.
[Serializable]
public struct CityParameterConfig
{
    public CityParameterType Parameter;
    public float InitialValue;
    public float MinValue;
    public float MaxValue;

    // Nivel critico real do parametro, sendo um valor negativo equivalente a nunca ficar critico.
    public float CriticalLevel;

    // Deriva de manutencao aplicada a cada turno na fase de resolucao.
    public float Deriva;
}

// Estado dos 9 parametros da cidade, com clamp, nivel critico e notificacao de mudancas.
public class CityStats
{
    // Unico parametro cujo nivel critico termina o jogo, os demais entram em colapso.
    public const CityParameterType AnchorParameter = CityParameterType.BemEstar;

    private readonly Dictionary<CityParameterType, float> _values = new Dictionary<CityParameterType, float>();
    private readonly Dictionary<CityParameterType, float> _minValues = new Dictionary<CityParameterType, float>();
    private readonly Dictionary<CityParameterType, float> _maxValues = new Dictionary<CityParameterType, float>();
    private readonly Dictionary<CityParameterType, float> _criticalLevels = new Dictionary<CityParameterType, float>();
    private readonly Dictionary<CityParameterType, float> _derivas = new Dictionary<CityParameterType, float>();

    private readonly InteractionMatrix _interactions;
    private readonly float _colapsoPenaltyPerParameter;

    private bool _gameOverRaised;

    public event Action<CityParameterType, float> OnParameterChanged;
    public event Action<CityParameterType> OnParameterCritical;
    public event Action OnGameOver;

    // interactions e opcional, null reduz a resolucao do turno a so recalcular Bem-estar.
    public CityStats(
        IEnumerable<CityParameterConfig> config,
        InteractionMatrix interactions = null,
        float colapsoPenaltyPerParameter = 0f)
    {
        _interactions = interactions;
        _colapsoPenaltyPerParameter = colapsoPenaltyPerParameter;

        foreach (var entry in config)
        {
            _minValues[entry.Parameter] = entry.MinValue;
            _maxValues[entry.Parameter] = entry.MaxValue;
            _criticalLevels[entry.Parameter] = entry.CriticalLevel;
            _derivas[entry.Parameter] = entry.Deriva;
            _values[entry.Parameter] = Clamp(entry.Parameter, entry.InitialValue);
        }
    }

    public float GetValue(CityParameterType parameter)
    {
        return _values.TryGetValue(parameter, out var value) ? value : 0f;
    }

    public float GetDeriva(CityParameterType parameter)
    {
        return _derivas.TryGetValue(parameter, out var deriva) ? deriva : 0f;
    }

    // O parametro cruzou o proprio nivel critico por baixo, quando este nao e negativo.
    public bool IsCritical(CityParameterType parameter)
    {
        if (_values.ContainsKey(parameter) == false)
            return false;

        var critico = _criticalLevels[parameter];
        if (critico < 0f)
            return false;

        return _values[parameter] <= critico;
    }

    // Unico gatilho real de Game Over.
    public bool IsAnchorCritical()
    {
        return IsCritical(AnchorParameter);
    }

    // Em colapso: critico, mas nao e a ancora nem Pesquisa.
    public bool IsInCollapse(CityParameterType parameter)
    {
        if (parameter == AnchorParameter || parameter == CityParameterType.Pesquisa)
            return false;

        return IsCritical(parameter);
    }

    public int CountCollapsedParameters()
    {
        var count = 0;
        foreach (var parameter in _values.Keys)
        {
            if (IsInCollapse(parameter))
                count++;
        }
        return count;
    }

    // Aplica um unico modificador a um parametro.
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

    // Recalcula Bem-estar a partir dos outros parametros, chamado na fase de resolucao do turno.
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

    // Fase de resolucao do turno inteira: interacoes, deriva, Bem-estar e penalidade de colapso.
    public void ResolveTurn()
    {
        var colapsosAntes = CountCollapsedParameters();

        _interactions?.Resolve(this);
        RecomputeDerivedParameters();

        if (colapsosAntes > 0)
        {
            ApplyModifier(new StatModifier
            {
                Parameter = AnchorParameter,
                Amount = -_colapsoPenaltyPerParameter * colapsosAntes,
            });
        }
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
