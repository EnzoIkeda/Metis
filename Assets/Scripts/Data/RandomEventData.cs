using System.Collections.Generic;
using UnityEngine;

// Comparacao de uma condicao contra o valor atual do parametro.
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

// Definicao orientada a dados de um evento aleatorio.
// TODO: adicionar o field para alterar visualmente o grid.
[CreateAssetMenu(fileName = "New Event", menuName = "Metis/Random Event Data")]
public class RandomEventData : ScriptableObject
{
    [SerializeField] private string _title;
    [SerializeField] private string _description;
    [SerializeField] private string _titleEn;
    [SerializeField, TextArea(2, 6)] private string _descriptionEn;
    [SerializeField] private TriggerCondition[] _triggerConditions;
    [SerializeField] private StatModifier[] _statEffects;
    [SerializeField] private int _minTurn = 1;
    [SerializeField] private int _maxTurn = 999;

    // Titulo no idioma atual, com fallback pro portugues se a traducao em ingles nao existir.
    public string Title => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_titleEn) == false
        ? _titleEn
        : _title;

    public string Description => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_descriptionEn) == false
        ? _descriptionEn
        : _description;
    public IReadOnlyList<TriggerCondition> TriggerConditions => _triggerConditions;
    public IReadOnlyList<StatModifier> StatEffects => _statEffects;

    // Turno minimo e maximo, inclusive, em que o evento pode ser sorteado.
    public int MinTurn => _minTurn;
    public int MaxTurn => _maxTurn;
}
