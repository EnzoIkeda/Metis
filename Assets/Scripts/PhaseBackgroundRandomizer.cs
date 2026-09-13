using UnityEngine;

// Sorteia um dos fundos nao animados a cada fase (recarga da City_Scene), aplicado no proprio SpriteRenderer.
[RequireComponent(typeof(SpriteRenderer))]
public class PhaseBackgroundRandomizer : MonoBehaviour
{
    [SerializeField] private Sprite[] _backgrounds;

    private void Awake()
    {
        if (_backgrounds == null || _backgrounds.Length == 0)
            return;

        var chosen = _backgrounds[Random.Range(0, _backgrounds.Length)];
        GetComponent<SpriteRenderer>().sprite = chosen;
    }
}
