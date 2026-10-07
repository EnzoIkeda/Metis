using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Toca os efeitos sonoros do jogo pelo grupo SFX do mixer, com algumas fontes pra sons sobrepostos.
// Singleton persistente entre cenas, junto da musica de fundo; tempo real, entao a pausa nao corta nenhum som.
public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer Instance { get; private set; }

    [SerializeField] private SoundEffectLibraryData _library;
    [SerializeField] private AudioMixerGroup _outputGroup;
    [SerializeField] private int _sourceCount = 6;
    [SerializeField] private float _fadeOutDuration = 0.25f;

    private readonly List<AudioSource> _sources = new List<AudioSource>();
    private readonly Dictionary<SoundEffect, MusicRotation> _rotations = new Dictionary<SoundEffect, MusicRotation>();
    private readonly Dictionary<SoundEffect, float> _lastPlayTimes = new Dictionary<SoundEffect, float>();
    private readonly Dictionary<AudioSource, Coroutine> _cutRoutines = new Dictionary<AudioSource, Coroutine>();
    private readonly System.Random _random = new System.Random();
    private int _nextSource;

    // Atalho pros chamadores: sem instancia na cena (ex. num teste) simplesmente nao toca nada.
    public static void Play(SoundEffect effect)
    {
        if (Instance != null)
            Instance.PlayEffect(effect);
    }

    private void Awake()
    {
        // A musica de fundo no mesmo objeto ja destroi a copia duplicada; aqui so nao registra a copia.
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        DontDestroyOnLoad(gameObject);

        var sourcesRoot = new GameObject("SfxSources");
        sourcesRoot.transform.SetParent(transform, false);
        for (int i = 0; i < Mathf.Max(1, _sourceCount); i++)
        {
            var source = sourcesRoot.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.outputAudioMixerGroup = _outputGroup;
            _sources.Add(source);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayEffect(SoundEffect effect)
    {
        var entry = _library != null ? _library.Find(effect) : null;
        if (entry == null || entry.Clips == null || entry.Clips.Count == 0 || _sources.Count == 0)
            return;

        if (entry.MinInterval > 0f && _lastPlayTimes.TryGetValue(effect, out var last) && Time.unscaledTime - last < entry.MinInterval)
            return;
        _lastPlayTimes[effect] = Time.unscaledTime;

        if (_rotations.TryGetValue(effect, out var rotation) == false)
        {
            rotation = new MusicRotation();
            _rotations[effect] = rotation;
        }
        var clip = entry.Clips[rotation.Next(entry.Clips.Count, _random)];
        if (clip == null)
            return;

        var source = NextSource();
        source.clip = clip;
        source.volume = entry.Volume;
        source.Play();

        if (entry.MaxDuration > 0f && entry.MaxDuration < clip.length)
            _cutRoutines[source] = StartCoroutine(CutAfter(source, entry.MaxDuration, entry.Volume));
    }

    // Prefere uma fonte livre; com todas ocupadas, reaproveita a mais antiga em rodizio.
    private AudioSource NextSource()
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            var candidate = _sources[(_nextSource + i) % _sources.Count];
            if (candidate.isPlaying == false)
            {
                _nextSource = (_sources.IndexOf(candidate) + 1) % _sources.Count;
                StopCut(candidate);
                return candidate;
            }
        }

        var source = _sources[_nextSource];
        _nextSource = (_nextSource + 1) % _sources.Count;
        StopCut(source);
        return source;
    }

    private void StopCut(AudioSource source)
    {
        if (_cutRoutines.TryGetValue(source, out var routine) && routine != null)
            StopCoroutine(routine);
        _cutRoutines.Remove(source);
    }

    private IEnumerator CutAfter(AudioSource source, float duration, float volume)
    {
        var fade = Mathf.Min(_fadeOutDuration, duration);
        yield return new WaitForSecondsRealtime(duration - fade);

        var elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(volume, 0f, elapsed / fade);
            yield return null;
        }

        source.Stop();
        source.volume = volume;
        _cutRoutines.Remove(source);
    }
}
