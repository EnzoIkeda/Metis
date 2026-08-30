using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup de detalhe de uma carta, com scrim de fundo e titulo, descricao e custo.
public class CardDetailPopupView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _scrimButton;

    private Action _onPlay;

    private void Start()
    {
        if (_playButton != null)
            _playButton.onClick.AddListener(HandlePlayClicked);
        if (_backButton != null)
            _backButton.onClick.AddListener(Hide);
        if (_scrimButton != null)
            _scrimButton.onClick.AddListener(Hide);

        Hide();
    }

    public void Show(CardData card, Action onPlay)
    {
        _onPlay = onPlay;

        if (_titleText != null)
            _titleText.text = card.CardName;
        if (_descriptionText != null)
            _descriptionText.text = card.Description;
        if (_costText != null)
            _costText.text = $"Custo: {card.Cost:0}";

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private void HandlePlayClicked()
    {
        var onPlay = _onPlay;
        Hide();
        onPlay?.Invoke();
    }

    private void Hide()
    {
        _onPlay = null;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
