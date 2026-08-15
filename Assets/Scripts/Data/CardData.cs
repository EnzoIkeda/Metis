using System.Collections.Generic;
using UnityEngine;

public enum CardTier
{
    Basica,
    Avancada,
    SmartCity
}

[CreateAssetMenu(fileName = "New Card", menuName = "Metis/Card Data")]
public class CardData : ScriptableObject
{
    [SerializeField] private string _cardName;
    [SerializeField] private string _description;
    [SerializeField] private float _cost;
    [SerializeField] private CardTier _tier;
    [SerializeField] private float _requiredPesquisa;
    [SerializeField] private StatModifier[] _statEffects;
    [SerializeField] private StructureData _structureToPlace;

    public string CardName => _cardName;
    public string Description => _description;

    public float Cost => _cost;

    public CardTier Tier => _tier;

    public float RequiredPesquisa => _requiredPesquisa;

    public IReadOnlyList<StatModifier> StatEffects => _statEffects;

    public StructureData StructureToPlace => _structureToPlace;
}
