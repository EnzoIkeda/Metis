using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Metis/Card Data")]
public class CardData : ScriptableObject, ICardDefinition
{
    [SerializeField] private string _cardName;
    [SerializeField] private string _description;
    [SerializeField] private string _cardNameEn;
    [SerializeField, TextArea(2, 6)] private string _descriptionEn;
    [SerializeField] private float _cost;
    [SerializeField] private CardTier _tier;
    [SerializeField] private CardArchetype _archetype;
    [SerializeField] private float _requiredPesquisa;
    [SerializeField] private StatModifier[] _statEffects;
    [SerializeField] private CardAbility _ability;
    [SerializeField] private StructureData _structureToPlace;

    // Arte opcional da carta, mostrada no espaco reservado de imagem quando definida.
    [SerializeField] private Sprite _artwork;

    // Nome no idioma atual, com fallback pro portugues se a traducao em ingles nao existir.
    public string CardName => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_cardNameEn) == false
        ? _cardNameEn
        : _cardName;

    public string Description => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_descriptionEn) == false
        ? _descriptionEn
        : _description;

    string ICardDefinition.Id => name;

    public float Cost => _cost;

    public CardTier Tier => _tier;

    public CardArchetype Archetype => _archetype;

    public float RequiredPesquisa => _requiredPesquisa;

    public IReadOnlyList<StatModifier> StatEffects => _statEffects;

    public CardAbility Ability => _ability;

    public StructureData StructureToPlace => _structureToPlace;

    public Sprite Artwork => _artwork;
}
