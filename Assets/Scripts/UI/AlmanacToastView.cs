using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Aviso curto no topo da tela quando uma ficha nova entra no almanaque. Usa tempo real, pra
// aparecer tambem com o jogo pausado.
public class AlmanacToastView : MonoBehaviour
{
    [SerializeField] private KnowledgeLibraryData _library;

    // Com o almanaque aberto a ficha nova ja aparece na grade; o aviso so atrapalharia.
    [SerializeField] private AlmanacView _almanac;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField, Min(0.1f)] private float _visibleSeconds = 2.2f;
    [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.3f;

    // Fichas desbloqueadas no mesmo quadro (inicio da fase) viram um aviso so.
    private readonly List<string> _pending = new List<string>();
    private Coroutine _routine;

    private void OnEnable()
    {
        AlmanacProgress.OnUnlocked += HandleUnlocked;
        if (_group != null)
            _group.alpha = 0f;
    }

    private void OnDisable()
    {
        AlmanacProgress.OnUnlocked -= HandleUnlocked;
        _pending.Clear();
        _routine = null;
    }

    private void HandleUnlocked(string id)
    {
        if (_almanac != null && _almanac.IsOpen)
            return;
        var entry = _library != null ? _library.Find(id) : null;
        if (entry == null)
            return;

        _pending.Add(entry.Title);
        if (_routine == null && isActiveAndEnabled)
            _routine = StartCoroutine(ShowQueue());
    }

    private IEnumerator ShowQueue()
    {
        while (_pending.Count > 0)
        {
            yield return null;
            if (_messageText != null)
                _messageText.text = _pending.Count == 1
                    ? UIStrings.AlmanacNewEntry(_pending[0])
                    : UIStrings.AlmanacNewEntries(_pending.Count);
            _pending.Clear();
            yield return Fade(0f, 1f);
            yield return new WaitForSecondsRealtime(_visibleSeconds);
            yield return Fade(1f, 0f);
        }
        _routine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_group == null)
            yield break;

        var elapsed = 0f;
        while (elapsed < _fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, to, elapsed / _fadeSeconds);
            yield return null;
        }
        _group.alpha = to;
    }
}
