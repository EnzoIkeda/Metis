using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup de detalhe de uma carta, com scrim de fundo e titulo, descricao e requisitos.
public class CardDetailPopupView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Image _artworkImage;
    [SerializeField] private GameObject _artworkPlaceholder;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _scrimButton;

    private Action _onPlay;

    private void Start()
    {
        if (_playButton != null)
        {
            _playButton.onClick.AddListener(HandlePlayClicked);
            var playText = _playButton.GetComponentInChildren<TMP_Text>();
            if (playText != null)
                playText.text = UIStrings.PlayButton;
        }
        if (_backButton != null)
        {
            _backButton.onClick.AddListener(Hide);
            var backText = _backButton.GetComponentInChildren<TMP_Text>();
            if (backText != null)
                backText.text = UIStrings.BackButton;
        }
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
            _costText.text = UIStrings.CardRequirementsFull(card.RequiredPesquisa, card.Cost);

        // Mostra a arte se a carta tiver uma, senao so o placeholder.
        var hasArtwork = card.Artwork != null;
        if (_artworkImage != null)
        {
            _artworkImage.sprite = card.Artwork;
            _artworkImage.gameObject.SetActive(hasArtwork);
        }
        if (_artworkPlaceholder != null)
            _artworkPlaceholder.SetActive(hasArtwork == false);

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
