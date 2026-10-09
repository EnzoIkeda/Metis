using UnityEngine;

// Sorteia um dos fundos nao animados a cada fase (recarga da City_Scene), aplicado no proprio SpriteRenderer.
[RequireComponent(typeof(SpriteRenderer))]
public class PhaseBackgroundRandomizer : MonoBehaviour
{
    [SerializeField] private Sprite[] _backgrounds;

    // Indice do fundo sorteado, salvo pra fase retomada mostrar o mesmo fundo; -1 sem fundo.
    public int BackgroundIndex { get; private set; } = -1;

    private void Awake()
    {
        if (_backgrounds == null || _backgrounds.Length == 0)
            return;

        var savedPhase = MetaProgressionManager.SavedPhase;
        var savedIndex = savedPhase != null ? savedPhase.BackgroundIndex : -1;
        BackgroundIndex = savedIndex >= 0 && savedIndex < _backgrounds.Length
            ? savedIndex
            : Random.Range(0, _backgrounds.Length);
        GetComponent<SpriteRenderer>().sprite = _backgrounds[BackgroundIndex];
    }
}
