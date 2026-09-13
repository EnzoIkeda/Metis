using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Arrasta a camera isometrica fixa segurando o mouse ou um dedo, tipo "segurar e mover o mapa".
public class CameraPanController : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private float _minX = -8.4f;
    [SerializeField] private float _maxX = 6.5f;
    [SerializeField] private float _minZ = -14.6f;
    [SerializeField] private float _maxZ = 0.3f;

    private Vector3 _groundRight;
    private Vector3 _groundForward;
    private Vector2? _lastPointerPosition;
    private bool _isDraggingWithMouse;

    private void Reset()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Awake()
    {
        // Direcoes de arrasto projetadas no plano do chao, ja que a camera fica inclinada.
        _groundRight = new Vector3(transform.right.x, 0f, transform.right.z).normalized;
        _groundForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
    }

    private void Update()
    {
        HandleMouseDrag();
        HandleTouchDrag();
    }

    private void HandleMouseDrag()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            _isDraggingWithMouse = true;
            _lastPointerPosition = Mouse.current.position.ReadValue();
            return;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            _isDraggingWithMouse = false;
            _lastPointerPosition = null;
            return;
        }

        if (_isDraggingWithMouse)
            ApplyDrag(Mouse.current.position.ReadValue());
    }

    private void HandleTouchDrag()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return;

        TouchControl active = null;
        var activeCount = 0;
        foreach (var touch in touchscreen.touches)
        {
            if (touch.press.isPressed == false)
                continue;

            activeCount++;
            if (active == null)
                active = touch;
        }

        // So arrasta com 1 dedo; 2 dedos e pinch de zoom, tratado a parte.
        if (activeCount != 1)
        {
            _lastPointerPosition = null;
            return;
        }

        if (active.press.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(active.touchId.ReadValue()))
                return;

            _lastPointerPosition = active.position.ReadValue();
            return;
        }

        if (_lastPointerPosition.HasValue)
            ApplyDrag(active.position.ReadValue());
    }

    private void ApplyDrag(Vector2 currentPointerPosition)
    {
        if (_camera == null)
            return;

        if (_lastPointerPosition.HasValue == false)
        {
            _lastPointerPosition = currentPointerPosition;
            return;
        }

        var screenDelta = currentPointerPosition - _lastPointerPosition.Value;
        _lastPointerPosition = currentPointerPosition;

        if (screenDelta.sqrMagnitude <= 0f || Screen.height <= 0)
            return;

        // Pixel de tela vira unidade de mundo de acordo com o zoom atual, entao o arrasto sempre acompanha o dedo.
        var worldUnitsPerPixel = _camera.orthographicSize * 2f / Screen.height;
        var worldDelta = (-_groundRight * screenDelta.x - _groundForward * screenDelta.y) * worldUnitsPerPixel;

        var newPosition = transform.position + worldDelta;
        newPosition.x = Mathf.Clamp(newPosition.x, _minX, _maxX);
        newPosition.z = Mathf.Clamp(newPosition.z, _minZ, _maxZ);
        transform.position = newPosition;
    }
}
