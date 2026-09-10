using System.Collections.Generic;
using UnityEngine;

// 4 tiers, alinhados aos limiares de Pesquisa e aos tiers da planilha de balanceamento.
public enum CardTier
{
    Basica,
    CidadeDigital,
    CidadeConectada,
    SmartCity
}

// Baralho tematico da carta na planilha. Geral cobre tanto as cartas sem baralho proprio quanto o tier 0 compartilhado.
public enum CardArchetype
{
    Geral,
    Sustentabilidade,
    Industria,
    Automacao
}

[CreateAssetMenu(fileName = "New Card", menuName = "Metis/Card Data")]
public class CardData : ScriptableObject
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

    public float Cost => _cost;

    public CardTier Tier => _tier;

    public CardArchetype Archetype => _archetype;

    public float RequiredPesquisa => _requiredPesquisa;

    public IReadOnlyList<StatModifier> StatEffects => _statEffects;

    public StructureData StructureToPlace => _structureToPlace;

    public Sprite Artwork => _artwork;
}
