using System;
using System.Collections.Generic;

// Comparacao de uma condicao contra o valor atual do parametro.
public enum ComparisonType
{
    GreaterThanOrEqual,
    LessThanOrEqual
}

[Serializable]
public struct TriggerCondition
{
    public CityParameterType Parameter;
    public ComparisonType Comparison;
    public float Threshold;
}

// Regras de jogo de um evento aleatorio, sem nada de apresentacao.
public interface IRandomEventDefinition
{
    string Id { get; }
    IReadOnlyList<TriggerCondition> TriggerConditions { get; }
    IReadOnlyList<StatModifier> StatEffects { get; }

    // Turno minimo e maximo, inclusive, em que o evento pode ser sorteado.
    int MinTurn { get; }
    int MaxTurn { get; }
}
