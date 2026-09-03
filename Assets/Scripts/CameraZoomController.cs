using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Controla o zoom da camera isometrica fixa, via scroll, pinch ou botoes de UI.
public class CameraZoomController : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private float _minOrthographicSize = 8f;
    [SerializeField] private float _maxOrthographicSize = 19f;
    [SerializeField] private float _scrollZoomSpeed = 0.02f;
    [SerializeField] private float _pinchZoomSpeed = 0.015f;
    [SerializeField] private float _buttonZoomStep = 2f;

    private float? _previousPinchDistance;

    private void Reset()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Update()
    {
        HandleScrollZoom();
        HandlePinchZoom();
    }

    private void HandleScrollZoom()
    {
        if (Mouse.current == null)
            return;

        var scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        // Scroll pra cima aproxima a camera.
        ApplyZoomDelta(-scroll * _scrollZoomSpeed);
    }

    private void HandlePinchZoom()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return;

        TouchControl first = null;
        TouchControl second = null;
        var activeCount = 0;
        foreach (var touch in touchscreen.touches)
        {
            if (touch.press.isPressed == false)
                continue;

            activeCount++;
            if (first == null) first = touch;
            else if (second == null) second = touch;
        }

        if (activeCount < 2 || first == null || second == null)
        {
            _previousPinchDistance = null;
            return;
        }

        var distance = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());
        if (_previousPinchDistance.HasValue)
        {
            // Dedos se afastando aproxima a camera.
            var delta = distance - _previousPinchDistance.Value;
            ApplyZoomDelta(-delta * _pinchZoomSpeed);
        }

        _previousPinchDistance = distance;
    }

    // Botao de aproximar a camera.
    public void ZoomIn() => ApplyZoomDelta(-_buttonZoomStep);

    // Botao de afastar a camera.
    public void ZoomOut() => ApplyZoomDelta(_buttonZoomStep);

    private void ApplyZoomDelta(float delta)
    {
        if (_camera == null)
            return;

        _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + delta, _minOrthographicSize, _maxOrthographicSize);
    }
}
