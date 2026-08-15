using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Factory para criar a mao de cartas a partir do prefab
/// </summary>
public class CardHandView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CardView _cardPrefab;
    [SerializeField] private Transform _cardContainer;

    private readonly List<CardView> _spawnedCards = new List<CardView>();

    private void Start()
    {
        _turnManager.Hand.OnHandChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_turnManager != null && _turnManager.Hand != null)
            _turnManager.Hand.OnHandChanged -= Refresh;
    }

    private void Refresh()
    {
        foreach (var card in _spawnedCards)
            Destroy(card.gameObject);
        _spawnedCards.Clear();

        foreach (var card in _turnManager.Hand.Cards)
        {
            var view = Instantiate(_cardPrefab, _cardContainer);
            view.Bind(card, HandleCardClicked);
            _spawnedCards.Add(view);
        }
    }

    private void HandleCardClicked(CardData card)
    {
        _turnManager.PlayCard(card);
    }
}
