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

// Uma opcao de dilema: efeitos imediatos e, se houver, a consequencia adiada.
[Serializable]
public struct EventChoiceDefinition
{
    public StatModifier[] Effects;
    public string FollowUpEventId;
    public int FollowUpDelay;

    public bool HasFollowUp => string.IsNullOrEmpty(FollowUpEventId) == false;
    public int EffectiveDelay => Math.Max(1, FollowUpDelay);
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

    // Vazio em evento comum; com opcoes, o evento e um dilema.
    IReadOnlyList<EventChoiceDefinition> Choices { get; }

    // So acontece como consequencia de uma escolha, nunca no sorteio.
    bool IsFollowUpOnly { get; }
}
