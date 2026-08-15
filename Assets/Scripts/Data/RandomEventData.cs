using System.Collections.Generic;
using UnityEngine;

/// <summary> Comparacao de uma condicao contra o valor atual do parametro.</summary>
public enum ComparisonType
{
    GreaterThanOrEqual,
    LessThanOrEqual
}

[System.Serializable]
public struct TriggerCondition
{
    public CityParameterType Parameter;
    public ComparisonType Comparison;
    public float Threshold;
}

/// <summary>
/// Definicao orientada a dados de um evento aleatorio, como um ScriptableObject
/// TODO: adicionar o field para alterar visualmente o grid
/// </summary>
[CreateAssetMenu(fileName = "New Event", menuName = "Metis/Random Event Data")]
public class RandomEventData : ScriptableObject
{
    [SerializeField] private string _title;
    [SerializeField] private string _description;
    [SerializeField] private TriggerCondition[] _triggerConditions;
    [SerializeField] private StatModifier[] _statEffects;

    public string Title => _title;
    public string Description => _description;
    public IReadOnlyList<TriggerCondition> TriggerConditions => _triggerConditions;
    public IReadOnlyList<StatModifier> StatEffects => _statEffects;
}
