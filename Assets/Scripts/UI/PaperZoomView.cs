using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Zoom da folha por pinca ou roda do mouse, e arrasto com um dedo ou o mouse enquanto ela esta ampliada.
public class PaperZoomView : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // Janela com mascara; a folha estica nela e e ampliada por escala.
    [SerializeField] private RectTransform _viewport;
    [SerializeField] private RectTransform _content;
    [SerializeField] private float _maxScale = 3f;

    // Zoom por unidade da roda do mouse.
    [SerializeField] private float _scrollSpeed = 0.03f;

    private readonly HashSet<int> _dragging = new HashSet<int>();
    private PaperZoom _zoom;
    private float? _previousPinchDistance;
    private Vector2 _previousPinchCenter;

    public float Scale => _zoom != null ? _zoom.Scale : 1f;

    private PaperZoom Zoom => _zoom ??= new PaperZoom(1f, _maxScale);

    private void OnEnable()
    {
        ResetZoom();
    }

    private void OnDisable()
    {
        _dragging.Clear();
        _previousPinchDistance = null;
    }

    public void ResetZoom()
    {
        Zoom.Reset();
        Apply();
    }

    // Zoom em torno de um ponto da tela, como a pinca e a roda fazem.
    public void ZoomAt(Vector2 screenPoint, Camera eventCamera, float factor)
    {
        if (TryLocal(screenPoint, eventCamera, out var point) == false)
            return;
        Zoom.ZoomAround(point.x, point.y, factor);
        Apply();
    }

    public void OnScroll(PointerEventData eventData)
    {
        var scroll = Mathf.Clamp(eventData.scrollDelta.y, -40f, 40f);
        if (Mathf.Approximately(scroll, 0f))
            return;
        ZoomAt(eventData.position, eventData.enterEventCamera, Mathf.Exp(scroll * _scrollSpeed));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragging.Add(eventData.pointerId);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Com dois dedos quem manda e a pinca.
        if (_dragging.Count > 1 || Zoom.IsZoomed == false)
            return;
        var camera = eventData.pressEventCamera;
        if (TryLocal(eventData.position, camera, out var now) && TryLocal(eventData.position - eventData.delta, camera, out var before))
        {
            Zoom.Pan(now.x - before.x, now.y - before.y);
            Apply();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _dragging.Remove(eventData.pointerId);
    }

    private void Update()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return;

        TouchControl first = null;
        TouchControl second = null;
        foreach (var touch in touchscreen.touches)
        {
            if (touch.press.isPressed == false)
                continue;
            if (first == null)
                first = touch;
            else if (second == null)
                second = touch;
        }
        if (first == null || second == null)
        {
            _previousPinchDistance = null;
            return;
        }

        var a = first.position.ReadValue();
        var b = second.position.ReadValue();
        var center = (a + b) / 2f;
        var camera = EventCamera();
        if (_previousPinchDistance.HasValue == false)
        {
            // Pinca so conta se comecar em cima da folha.
            if (RectTransformUtility.RectangleContainsScreenPoint(_viewport, center, camera) == false)
                return;
            _previousPinchDistance = Vector2.Distance(a, b);
            _previousPinchCenter = center;
            return;
        }

        var distance = Vector2.Distance(a, b);
        if (_previousPinchDistance.Value > 1f && TryLocal(center, camera, out var point) && TryLocal(_previousPinchCenter, camera, out var previous))
        {
            Zoom.ZoomAround(point.x, point.y, distance / _previousPinchDistance.Value);
            // Os dois dedos andando juntos arrastam a folha.
            Zoom.Pan(point.x - previous.x, point.y - previous.y);
            Apply();
        }
        _previousPinchDistance = distance;
        _previousPinchCenter = center;
    }

    private Camera EventCamera()
    {
        var canvas = _viewport != null ? _viewport.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            return null;
        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // Ponto da tela em coordenadas da janela, a partir do canto superior esquerdo.
    private bool TryLocal(Vector2 screenPoint, Camera eventCamera, out Vector2 point)
    {
        point = Vector2.zero;
        if (_viewport == null || RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPoint, eventCamera, out var local) == false)
            return false;
        var rect = _viewport.rect;
        point = new Vector2(local.x - rect.xMin, local.y - rect.yMax);
        return true;
    }

    private void Apply()
    {
        if (_viewport == null || _content == null)
            return;
        var rect = _viewport.rect;
        Zoom.Resize(rect.width, rect.height);
        // A folha estica na janela com pivo no canto de cima, entao a posicao e a do canto.
        _content.anchorMin = Vector2.zero;
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0f, 1f);
        _content.sizeDelta = Vector2.zero;
        _content.localScale = new Vector3(Zoom.Scale, Zoom.Scale, 1f);
        _content.anchoredPosition = new Vector2(Zoom.OffsetX, Zoom.OffsetY);
    }
}
