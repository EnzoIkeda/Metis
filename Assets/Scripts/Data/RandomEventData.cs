using System;
using System.Collections.Generic;
using UnityEngine;

// Opcao de dilema como fica guardada no asset.
[Serializable]
public class EventChoiceData
{
    public EventChoiceDefinition Rule;
    public string Label;
    public string LabelEn;
    [TextArea(2, 4)] public string Detail;
    [TextArea(2, 4)] public string DetailEn;

    public string DisplayLabel => Localized(Label, LabelEn);
    public string DisplayDetail => Localized(Detail, DetailEn);

    private static string Localized(string portuguese, string english)
    {
        return LocalizationManager.Current == Language.English && string.IsNullOrEmpty(english) == false ? english : portuguese;
    }
}


// Definicao orientada a dados de um evento aleatorio.
// TODO: adicionar o field para alterar visualmente o grid.
[CreateAssetMenu(fileName = "New Event", menuName = "Metis/Random Event Data")]
public class RandomEventData : ScriptableObject, IRandomEventDefinition
{
    [SerializeField] private string _title;
    [SerializeField] private string _description;
    [SerializeField] private string _titleEn;
    [SerializeField, TextArea(2, 6)] private string _descriptionEn;
    [SerializeField] private TriggerCondition[] _triggerConditions;
    [SerializeField] private StatModifier[] _statEffects;
    [SerializeField] private int _minTurn = 1;
    [SerializeField] private int _maxTurn = 999;

    // Com opcoes, o evento e um dilema e o popup so fecha com uma escolha.
    [SerializeField] private EventChoiceData[] _choices;
    [SerializeField] private bool _isFollowUpOnly;

    // Ficha "Saiba mais" do evento; so conteudo educativo, fora das regras de jogo.
    [SerializeField] private KnowledgeEntryData _knowledgeEntry;

    // Titulo no idioma atual, com fallback pro portugues se a traducao em ingles nao existir.
    public string Title => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_titleEn) == false
        ? _titleEn
        : _title;

    public string Description => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_descriptionEn) == false
        ? _descriptionEn
        : _description;

    string IRandomEventDefinition.Id => name;

    public IReadOnlyList<TriggerCondition> TriggerConditions => _triggerConditions;
    public IReadOnlyList<StatModifier> StatEffects => _statEffects;

    // Turno minimo e maximo, inclusive, em que o evento pode ser sorteado.
    public int MinTurn => _minTurn;
    public int MaxTurn => _maxTurn;

    public IReadOnlyList<EventChoiceDefinition> Choices => _choices != null
        ? Array.ConvertAll(_choices, choice => choice.Rule)
        : Array.Empty<EventChoiceDefinition>();

    public bool IsFollowUpOnly => _isFollowUpOnly;

    // Opcoes com os textos, para a tela; as regras saem de Choices.
    public IReadOnlyList<EventChoiceData> ChoiceData => (IReadOnlyList<EventChoiceData>)_choices ?? Array.Empty<EventChoiceData>();

    public KnowledgeEntryData KnowledgeEntry => _knowledgeEntry;
}
