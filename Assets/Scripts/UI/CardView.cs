using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Carta da mao: toque abre o detalhe; com a mao revelada, mostra os efeitos no lugar da imagem.
// Arrastar na horizontal rola a mao; segurar ou arrastar em outra direcao carrega a carta: soltar fora da mao joga, sobre ela cancela.
public class CardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static readonly Color PlayableColor = Color.white;
    private static readonly Color UnplayableColor = new Color(1f, 0.3f, 0.3f);

    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Image _artworkImage;
    [SerializeField] private GameObject _artworkPlaceholder;
    [SerializeField] private TMP_Text _requirementsText;
    [SerializeField] private TMP_Text _effectsText;
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private float _dragDirectionThreshold = 12f;

    [Tooltip("Tempo segurando a carta parada ate ela se soltar da mao e passar a seguir o dedo.")]
    [SerializeField] private float _holdToCarryDuration = 0.25f;

    [Tooltip("Escala da carta enquanto e carregada pela tela.")]
    [SerializeField] private float _carryScale = 1.15f;

    private CardData _card;
    private Action<CardData> _onPlay;
    private Action<CardData> _onExpand;
    private ScrollRect _scrollRect;
    private bool? _isHorizontalDrag;

    // Estado do gesto de segurar e carregar.
    private bool _pointerDown;
    private float _pointerDownTime;
    private Vector2 _pointerDownPosition;
    private Camera _pressCamera;
    private bool _isCarrying;
    private bool _suppressClick;
    private Transform _originalParent;
    private int _originalSiblingIndex;
    private GameObject _placeholder;
    private Vector3 _originalScale;

    private void Awake()
    {
        _scrollRect = GetComponentInParent<ScrollRect>();
    }

    private void OnDisable()
    {
        if (_isCarrying)
            ReturnToHand();
        _pointerDown = false;
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

    private void Update()
    {
        if (_pointerDown == false || _isCarrying || _isHorizontalDrag != null)
            return;

        // Tempo real, pra continuar funcionando mesmo com o jogo pausado.
        if (Time.unscaledTime - _pointerDownTime >= _holdToCarryDuration)
            StartCarrying(_pointerDownPosition);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pointerDown = true;
        _pointerDownTime = Time.unscaledTime;
        _pointerDownPosition = eventData.position;
        _pressCamera = eventData.pressEventCamera;
        _suppressClick = false;
        _isHorizontalDrag = null;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pointerDown = false;
        if (_isCarrying == false)
            return;

        // Evento de clique vem logo depois do pointer up e nao pode abrir o detalhe.
        _suppressClick = true;
        var releasedOverHand = IsOverHand(eventData.position);
        ReturnToHand();

        if (releasedOverHand == false)
            _onPlay?.Invoke(_card);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging || _suppressClick)
            return;

        _onExpand?.Invoke(_card);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isCarrying == false)
            _isHorizontalDrag = null;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rectTransform == null)
            return;

        if (_isCarrying)
        {
            FollowPointer(eventData.position);
            return;
        }

        if (_isHorizontalDrag == null)
        {
            var totalDelta = eventData.position - eventData.pressPosition;
            if (totalDelta.magnitude < _dragDirectionThreshold)
                return;

            // Arrasto na horizontal rola a mao; em qualquer outra direcao a carta ja se solta pra ser carregada.
            _isHorizontalDrag = Mathf.Abs(totalDelta.x) > Mathf.Abs(totalDelta.y);
            if (_isHorizontalDrag == true)
            {
                _scrollRect?.OnBeginDrag(eventData);
            }
            else
            {
                StartCarrying(eventData.position);
                return;
            }
        }

        if (_isHorizontalDrag == true)
            _scrollRect?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_isCarrying || _suppressClick)
            return;

        if (_isHorizontalDrag == true)
            _scrollRect?.OnEndDrag(eventData);

        _isHorizontalDrag = null;
    }

    // Tira a carta da mao rolavel (que recorta o que sai dela) e a coloca solta no canvas, deixando um espaco vazio no lugar.
    private void StartCarrying(Vector2 screenPosition)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null || _rectTransform == null)
            return;

        _isCarrying = true;
        _originalParent = transform.parent;
        _originalSiblingIndex = transform.GetSiblingIndex();
        _originalScale = transform.localScale;

        _placeholder = new GameObject("CardPlaceholder", typeof(RectTransform), typeof(LayoutElement));
        var placeholderLayout = _placeholder.GetComponent<LayoutElement>();
        placeholderLayout.preferredWidth = LayoutUtility.GetPreferredWidth(_rectTransform);
        placeholderLayout.preferredHeight = LayoutUtility.GetPreferredHeight(_rectTransform);
        _placeholder.transform.SetParent(_originalParent, false);
        _placeholder.transform.SetSiblingIndex(_originalSiblingIndex);

        transform.SetParent(canvas.rootCanvas.transform, true);
        transform.SetAsLastSibling();
        transform.localScale = _originalScale * _carryScale;
        FollowPointer(screenPosition);
    }

    private void FollowPointer(Vector2 screenPosition)
    {
        var parent = (RectTransform)transform.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, _pressCamera, out var local))
            _rectTransform.localPosition = local;
    }

    private void ReturnToHand()
    {
        _isCarrying = false;
        if (_originalParent != null)
        {
            transform.SetParent(_originalParent, false);
            transform.SetSiblingIndex(_originalSiblingIndex);
        }
        transform.localScale = _originalScale;

        if (_placeholder != null)
            Destroy(_placeholder);
        _placeholder = null;
    }

    private bool IsOverHand(Vector2 screenPosition)
    {
        if (_scrollRect == null)
            return false;

        var handArea = (RectTransform)_scrollRect.transform;
        return RectTransformUtility.RectangleContainsScreenPoint(handArea, screenPosition, _pressCamera);
    }
}
