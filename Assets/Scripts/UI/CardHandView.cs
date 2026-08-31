using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Reconstroi a mao de cartas visivel sempre que a mao muda.
public class CardHandView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CardView _cardPrefab;
    [SerializeField] private Transform _cardContainer;
    [SerializeField] private CardDetailPopupView _detailPopup;
    [SerializeField] private ScrollRect _scrollRect;

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
            view.Bind(card, HandleCardClicked, HandleCardExpandRequested, _turnManager.CanPlay(card));
            _spawnedCards.Add(view);
        }

        // Mao nova comeca do inicio (senao a rolagem podia ficar "presa"
        // numa posicao que nao existe mais na mao seguinte).
        if (_scrollRect != null)
            _scrollRect.horizontalNormalizedPosition = 0f;
    }

    private void HandleCardClicked(CardData card)
    {
        _turnManager.PlayCard(card);
    }

    private void HandleCardExpandRequested(CardData card)
    {
        if (_detailPopup != null)
            _detailPopup.Show(card, () => HandleCardClicked(card));
    }
}
