using UnityEngine;
using UnityEngine.EventSystems;

// Joystick virtual do passeio: tocar em qualquer ponto da area poe a base ali, e arrastar move o pino.
// O valor vai de -1 a 1 em cada eixo (Y para a frente).
public class WalkStickView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform _base;
    [SerializeField] private RectTransform _knob;

    [Tooltip("Distancia (em unidades do canvas) que leva o valor a 1.")]
    [SerializeField, Min(1f)] private float _radius = 130f;

    [SerializeField, Range(0f, 0.9f)] private float _deadZone = StickInput.DefaultDeadZone;

    private Vector2 _restPosition;
    private int _pointerId = int.MinValue;

    public Vector2 Value { get; private set; }

    public bool IsHeld => _pointerId != int.MinValue;

    private void Awake()
    {
        if (_base != null)
            _restPosition = _base.anchoredPosition;
    }

    private void OnDisable()
    {
        Release();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsHeld)
            return;
        _pointerId = eventData.pointerId;
        if (_base != null && ToLocal(eventData, (RectTransform)_base.parent, out var local))
            _base.anchoredPosition = local;
        UpdateValue(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == _pointerId)
            UpdateValue(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == _pointerId)
            Release();
    }

    // Solta o pino e volta a base ao lugar de repouso.
    public void Release()
    {
        _pointerId = int.MinValue;
        Value = Vector2.zero;
        if (_knob != null)
            _knob.anchoredPosition = Vector2.zero;
        if (_base != null)
            _base.anchoredPosition = _restPosition;
    }

    private void UpdateValue(PointerEventData eventData)
    {
        if (_base == null || ToLocal(eventData, _base, out var offset) == false)
            return;
        var (x, y) = StickInput.FromDrag(offset.x, offset.y, _radius, _deadZone);
        Value = new Vector2(x, y);
        if (_knob != null)
            _knob.anchoredPosition = Vector2.ClampMagnitude(offset, _radius);
    }

    private static bool ToLocal(PointerEventData eventData, RectTransform target, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(target, eventData.position, eventData.pressEventCamera, out local);
    }
}
