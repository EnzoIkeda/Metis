using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Uma carta na mao, com nome e imagem, que abre um popup de detalhe ou joga direto com swipe pra cima.
public class CardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Image _artworkImage;
    [SerializeField] private GameObject _artworkPlaceholder;
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private float _swipeUpThreshold = 100f;

    private CardData _card;
    private Action<CardData> _onPlay;
    private Action<CardData> _onExpand;
    private Vector2 _dragStartAnchoredPosition;

    public void Bind(CardData card, Action<CardData> onPlay, Action<CardData> onExpand)
    {
        _card = card;
        _onPlay = onPlay;
        _onExpand = onExpand;

        if (_nameText != null)
            _nameText.text = card.CardName;

        var hasArtwork = card.Artwork != null;
        if (_artworkImage != null)
        {
            _artworkImage.sprite = card.Artwork;
            _artworkImage.gameObject.SetActive(hasArtwork);
        }
        if (_artworkPlaceholder != null)
            _artworkPlaceholder.SetActive(hasArtwork == false);
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
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rectTransform == null)
            return;

        // Segue o dedo/mouse so pra cima, ja que so essa direcao dispara a jogada.
        var deltaY = Mathf.Max(0f, eventData.position.y - eventData.pressPosition.y);
        _rectTransform.anchoredPosition = _dragStartAnchoredPosition + new Vector2(0f, deltaY);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        var deltaY = eventData.position.y - eventData.pressPosition.y;

        if (_rectTransform != null)
            _rectTransform.anchoredPosition = _dragStartAnchoredPosition;

        if (deltaY >= _swipeUpThreshold)
            _onPlay?.Invoke(_card);
    }
}
