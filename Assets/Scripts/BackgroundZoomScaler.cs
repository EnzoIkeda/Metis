using UnityEngine;

// Mantem o fundo filho da camera com o mesmo tamanho na tela em qualquer zoom, sem ampliar a textura ao aproximar.
public class BackgroundZoomScaler : MonoBehaviour
{
    // Tamanho ortografico em que a escala salva na cena ja cobre a tela inteira.
    [SerializeField, Min(0.01f)] private float _referenceOrthographicSize = 19f;

    private Camera _camera;
    private Vector3 _baseScale;

    private void Awake()
    {
        _camera = GetComponentInParent<Camera>();
        _baseScale = transform.localScale;
    }

    private void LateUpdate()
    {
        if (_camera == null)
            return;

        var factor = _camera.orthographicSize / _referenceOrthographicSize;
        transform.localScale = new Vector3(_baseScale.x * factor, _baseScale.y * factor, _baseScale.z);
    }
}
