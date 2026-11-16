using System;
using System.Collections.Generic;
using UnityEngine;

// Carta do chefe: so dano, paga com o Impeto dele.
[Serializable]
public class BossCardEntry : IBossCardDefinition
{
    [SerializeField] private string _id;
    [SerializeField] private string _cardName;
    [SerializeField] private string _cardNameEn;
    [SerializeField, TextArea(2, 6)] private string _description;
    [SerializeField, TextArea(2, 6)] private string _descriptionEn;
    [SerializeField, Min(1)] private int _momentumCost = 1;
    [SerializeField] private StatModifier[] _effects = Array.Empty<StatModifier>();

    public string Id => _id;
    public string CardName => BossData.Pick(_cardName, _cardNameEn);
    public string Description => BossData.Pick(_description, _descriptionEn);
    public int MomentumCost => _momentumCost;
    public IReadOnlyList<StatModifier> Effects => _effects ?? Array.Empty<StatModifier>();
}

// Chefe de fim de rodada: ataca a fraqueza de um baralho com cartas pagas pelo Impeto.
[CreateAssetMenu(fileName = "New Boss", menuName = "Metis/Boss Data")]
public class BossData : ScriptableObject, IBossDefinition
{
    [SerializeField] private string _bossName;
    [SerializeField] private string _bossNameEn;
    [SerializeField, TextArea(2, 6)] private string _summary;
    [SerializeField, TextArea(2, 6)] private string _summaryEn;
    [SerializeField, TextArea(3, 10)] private string _intro;
    [SerializeField, TextArea(3, 10)] private string _introEn;
    [SerializeField, TextArea(2, 6)] private string _victory;
    [SerializeField, TextArea(2, 6)] private string _victoryEn;
    [SerializeField] private KnowledgeEntryData _knowledgeEntry;
    [SerializeField] private CardArchetype _targetArchetype;
    [SerializeField, Min(0)] private int _startingMomentum;
    [SerializeField, Min(1)] private int _momentumPerTurn = 1;
    [SerializeField] private BossMechanic _mechanic;
    [SerializeField] private float _mechanicAmount;
    [SerializeField] private string _mechanicCardId;
    [SerializeField] private BossCardEntry[] _cards = Array.Empty<BossCardEntry>();

    public string BossName => Pick(_bossName, _bossNameEn);
    public string Summary => Pick(_summary, _summaryEn);
    public string Intro => Pick(_intro, _introEn);
    public string Victory => Pick(_victory, _victoryEn);
    public KnowledgeEntryData KnowledgeEntry => _knowledgeEntry;
    public IReadOnlyList<BossCardEntry> BossCards => _cards ?? Array.Empty<BossCardEntry>();

    string IBossDefinition.Id => name;
    public CardArchetype TargetArchetype => _targetArchetype;
    public int StartingMomentum => _startingMomentum;
    public int MomentumPerTurn => _momentumPerTurn;
    public BossMechanic Mechanic => _mechanic;
    public float MechanicAmount => _mechanicAmount;
    public string MechanicCardId => _mechanicCardId ?? string.Empty;
    public IReadOnlyList<IBossCardDefinition> Cards => BossCards;

    public BossCardEntry FindCard(string cardId)
    {
        return BossRules.FindCard(this, cardId) as BossCardEntry;
    }

    public static string Pick(string portuguese, string english)
    {
        return LocalizationManager.Current == Language.English && string.IsNullOrEmpty(english) == false ? english : portuguese;
    }
}
