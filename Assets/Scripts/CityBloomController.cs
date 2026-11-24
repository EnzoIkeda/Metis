using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Liga ou desliga o brilho das luzes da cidade conforme a preferencia do jogador.
public class CityBloomController : MonoBehaviour
{
    [Tooltip("Camera da cidade; o pos-processamento so roda com o brilho ligado.")]
    [SerializeField] private Camera _camera;

    [Tooltip("Volume com o Bloom.")]
    [SerializeField] private Volume _volume;

    private void OnEnable()
    {
        BloomSettings.OnChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        BloomSettings.OnChanged -= Apply;
    }

    private void Apply()
    {
        var enabled = BloomSettings.Enabled;
        if (_volume != null)
            _volume.enabled = enabled;
        if (_camera != null)
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = enabled;
    }
}
