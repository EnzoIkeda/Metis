using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup da carta de busca: mostra as cartas que podem vir do baralho, e a tocada vai pra mao.
public class SearchPopupView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private CardView _cardPrefab;
    [SerializeField] private Transform _cardContainer;
    [SerializeField] private ScrollRect _scrollRect;

    private readonly List<CardView> _spawnedCards = new List<CardView>();

    private void Start()
    {
        _turnManager.OnSearchRequested += Show;
        Hide();
    }

    private void OnDisable()
    {
        if (_turnManager != null)
            _turnManager.OnSearchRequested -= Show;
    }

    private void Show(IReadOnlyList<CardData> candidates)
    {
        Clear();
        if (_titleText != null)
            _titleText.text = UIStrings.SearchTitle;

        foreach (var card in candidates)
        {
            var view = Instantiate(_cardPrefab, _cardContainer);
            view.Bind(card, Choose, Choose, true, _turnManager.Hand.IsRevealed);
            _spawnedCards.Add(view);
        }

        if (_scrollRect != null)
            _scrollRect.horizontalNormalizedPosition = 0f;
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private void Choose(CardData card)
    {
        if (_turnManager.CompleteSearch(card))
            Hide();
    }

    private void Hide()
    {
        Clear();
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void Clear()
    {
        foreach (var view in _spawnedCards)
            Destroy(view.gameObject);
        _spawnedCards.Clear();
    }
}
