using System;
using System.Collections;
using UnityEngine;

// Toca efeitos visuais de particula em resposta ao loop de turno, sem logica de jogo.
public class CityEffectsController : MonoBehaviour
{
    [SerializeField] private ParticleSystem _impactGlowingPrefab;
    [SerializeField] private ParticleSystem _magicPoofPrefab;
    [SerializeField] private ParticleSystem _explosionPrefab;
    [SerializeField] private GameObject _ambientGlows;
    [SerializeField] private Transform _effectSpawnPoint;

    [Tooltip("Duração total do CFXR Impact Glowing (duration + lifetime das partículas) — quanto tempo esperar antes de considerar a animação terminada.")]
    [SerializeField] private float _impactGlowingDuration = 1.6f;

    [Tooltip("Duração total do CFXR Magic Poof (duration + lifetime das partículas).")]
    [SerializeField] private float _magicPoofDuration = 2.6f;

    [Tooltip("Duração total do CFXR Explosion 1 (duration + lifetime das partículas).")]
    [SerializeField] private float _explosionDuration = 2.1f;

    // Toca ao jogar uma carta, atrasando o popup de evento ate o efeito terminar.
    public void PlayImpactGlowing(Action onComplete)
    {
        StartCoroutine(PlayOneShot(_impactGlowingPrefab, _impactGlowingDuration, onComplete));
    }

    // Toca ao fechar um popup de evento positivo ou misto, atrasando o inicio do proximo turno.
    public void PlayMagicPoof(Action onComplete)
    {
        StartCoroutine(PlayOneShot(_magicPoofPrefab, _magicPoofDuration, onComplete));
    }

    // Toca ao fechar um popup de evento negativo, atrasando o inicio do proximo turno.
    public void PlayExplosion(Action onComplete)
    {
        StartCoroutine(PlayOneShot(_explosionPrefab, _explosionDuration, onComplete));
    }

    // Liga ou desliga o loop ambiente, visivel durante a fase de acao.
    public void SetAmbientGlowsVisible(bool visible)
    {
        if (_ambientGlows != null)
            _ambientGlows.SetActive(visible);
    }

    private IEnumerator PlayOneShot(ParticleSystem prefab, float duration, Action onComplete)
    {
        if (prefab != null)
        {
            var spawnPosition = _effectSpawnPoint != null ? _effectSpawnPoint.position : transform.position;
            var instance = Instantiate(prefab, spawnPosition, Quaternion.identity);
            instance.Play();
            // Margem de seguranca pra garantir que toda particula ja sumiu antes de destruir.
            Destroy(instance.gameObject, duration + 0.5f);
        }

        yield return new WaitForSeconds(duration);
        onComplete?.Invoke();
    }
}
