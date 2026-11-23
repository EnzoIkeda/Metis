using UnityEngine;
using UnityEngine.EventSystems;

// Area de olhar do passeio: soma o arraste em pixels de tela; quem le zera a soma a cada quadro.
public class WalkLookArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Vector2 _pending;
    private int _pointerId = int.MinValue;

    public bool IsHeld => _pointerId != int.MinValue;

    private void OnDisable()
    {
        _pending = Vector2.zero;
        _pointerId = int.MinValue;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsHeld == false)
            _pointerId = eventData.pointerId;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == _pointerId)
            _pending += eventData.delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == _pointerId)
            _pointerId = int.MinValue;
    }

    public Vector2 ConsumeDelta()
    {
        var delta = _pending;
        _pending = Vector2.zero;
        return delta;
    }
}
