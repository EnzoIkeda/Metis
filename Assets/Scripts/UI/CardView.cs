using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Uma carta na mao, com nome, imagem e requisitos, que abre um popup de detalhe ao ser clicada. Com a mao revelada, mostra os efeitos exatos no lugar da imagem.
public class CardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static readonly Color PlayableColor = Color.white;
    private static readonly Color UnplayableColor = new Color(1f, 0.3f, 0.3f);

    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Image _artworkImage;
    [SerializeField] private GameObject _artworkPlaceholder;
    [SerializeField] private TMP_Text _requirementsText;
    [SerializeField] private TMP_Text _effectsText;
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private float _swipeUpThreshold = 100f;
    [SerializeField] private float _dragDirectionThreshold = 12f;

    private CardData _card;
    private Action<CardData> _onPlay;
    private Action<CardData> _onExpand;
    private Vector2 _dragStartAnchoredPosition;
    private ScrollRect _scrollRect;
    private bool? _isHorizontalDrag;

    private void Awake()
    {
        _scrollRect = GetComponentInParent<ScrollRect>();
    }

    public void Bind(CardData card, Action<CardData> onPlay, Action<CardData> onExpand, bool isPlayable, bool isRevealed = false)
    {
        _card = card;
        _onPlay = onPlay;
        _onExpand = onExpand;

        if (_nameText != null)
            _nameText.text = card.CardName;

        var showEffects = isRevealed && _effectsText != null;
        var hasArtwork = card.Artwork != null;
        if (_artworkImage != null)
        {
            _artworkImage.sprite = card.Artwork;
            _artworkImage.gameObject.SetActive(hasArtwork && showEffects == false);
        }
        if (_artworkPlaceholder != null)
            _artworkPlaceholder.SetActive(hasArtwork == false && showEffects == false);
        if (_effectsText != null)
        {
            _effectsText.gameObject.SetActive(showEffects);
            if (showEffects)
                _effectsText.text = UIStrings.CardEffects(card);
        }

        if (_requirementsText != null)
        {
            _requirementsText.text = UIStrings.CardRequirementsShort(card.RequiredPesquisa, card.Cost);
            // Fica vermelho se a carta nao puder ser jogada agora, so como aviso visual antecipado.
            _requirementsText.color = isPlayable ? PlayableColor : UnplayableColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging)
            return;

        _onExpand?.Invoke(_card);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragStartAnchoredPosition = _rectTransform.anchoredPosition;
        _isHorizontalDrag = null;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rectTransform == null)
            return;

        if (_isHorizontalDrag == null)
        {
            var totalDelta = eventData.position - eventData.pressPosition;
            if (totalDelta.magnitude < _dragDirectionThreshold)
                return;

            _isHorizontalDrag = Mathf.Abs(totalDelta.x) > Mathf.Abs(totalDelta.y);
            if (_isHorizontalDrag == true && _scrollRect != null)
                _scrollRect.OnBeginDrag(eventData);
        }

        if (_isHorizontalDrag == true)
        {
            _scrollRect?.OnDrag(eventData);
            return;
        }

        // Segue o dedo/mouse so pra cima, ja que so essa direcao dispara a jogada.
        var deltaY = Mathf.Max(0f, eventData.position.y - eventData.pressPosition.y);
        _rectTransform.anchoredPosition = _dragStartAnchoredPosition + new Vector2(0f, deltaY);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_isHorizontalDrag == true)
        {
            _scrollRect?.OnEndDrag(eventData);
            _isHorizontalDrag = null;
            return;
        }

        var deltaY = eventData.position.y - eventData.pressPosition.y;

        if (_rectTransform != null)
            _rectTransform.anchoredPosition = _dragStartAnchoredPosition;

        if (deltaY >= _swipeUpThreshold)
            _onPlay?.Invoke(_card);

        _isHorizontalDrag = null;
    }
}
