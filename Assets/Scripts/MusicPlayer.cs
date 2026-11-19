using System.Collections;
using UnityEngine;

// Toca a trilha de fundo, alternando as faixas da playlist ativa, que pode ser trocada em runtime.
// Persistente entre cenas; cada cena tem uma instancia pra garantir que sempre exista uma, sem duplicar.
// Tambem toca as faixas proprias das sequencias, com fusao, e volta a trilha quando elas acabam.
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    [SerializeField] private MusicPlaylistData _defaultPlaylist;
    [SerializeField] private ThemeMusicData _themes;

    private AudioSource _audioSource;
    private MusicPlaylistData _currentPlaylist;
    private readonly MusicRotation _rotation = new MusicRotation();
    private readonly MusicMoodSelector _moods = new MusicMoodSelector();
    private readonly System.Random _random = new System.Random();
    private Coroutine _playbackRoutine;
    private Coroutine _fadeRoutine;
    private float _baseVolume = 1f;
    private AudioClip _themeClip;
    private AudioClip _sequenceClip;
    private bool _sequenceLoop;

    public ThemeMusicData Themes => _themes;

    // Faixa propria tocando agora (sequencia ou crise); nula com a trilha de fundo.
    public AudioClip CurrentTheme => _themeClip;
    public MusicMood Mood => _moods.Current;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _audioSource = GetComponent<AudioSource>();
        _baseVolume = _audioSource.volume;
        _currentPlaylist = _defaultPlaylist;
    }

    private void Start()
    {
        // Uma instancia duplicada so e destruida no fim do frame: sem essa checagem ela ainda tentaria tocar musica antes disso.
        if (Instance != this)
            return;

        // Uma sequencia pedida antes do primeiro quadro ja escolheu a faixa.
        if (_themeClip == null)
            PlayNextTrack();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Troca a playlist ativa e reinicia a rotacao nela.
    public void SetPlaylist(MusicPlaylistData playlist)
    {
        _currentPlaylist = playlist;
        if (_themeClip == null)
            PlayNextTrack();
    }

    // Toca a faixa de uma sequencia, que vence a crise e a trilha; sem faixa, a trilha continua.
    public void PlaySequence(AudioClip clip, bool loop)
    {
        if (clip == null)
            return;
        _sequenceClip = clip;
        _sequenceLoop = loop;
        _moods.SetSequence(true);
        ApplyMood();
    }

    public void EndSequence()
    {
        _sequenceClip = null;
        if (_moods.SetSequence(false))
            ApplyMood();
    }

    // Crise na fase: a faixa tensa entra no lugar da trilha, mas nunca por cima de uma sequencia.
    public void SetCrisis(bool active)
    {
        if (_moods.SetCrisis(active))
            ApplyMood();
    }

    // Fase nova carregada: a crise volta a valer a partir da proxima avaliacao.
    public void StartPhase()
    {
        if (_moods.StartPhase())
            ApplyMood();
    }

    // Fim da fase ou saida da cidade: a crise para e nao volta ate a proxima fase.
    public void EndPhase()
    {
        if (_moods.EndPhase())
            ApplyMood();
    }

    // Toca uma faixa propria no lugar da trilha, em loop ou uma vez so; a mesma faixa tocando nao recomeca.
    public void PlayTheme(AudioClip clip, bool loop, float fade = MusicFade.DefaultDuration)
    {
        if (clip == null)
            return;
        StopRotation();
        _audioSource.loop = loop;
        if (_themeClip == clip && _audioSource.clip == clip)
            return;

        _themeClip = clip;
        CrossFade(fade, () =>
        {
            _audioSource.clip = clip;
            _audioSource.loop = loop;
            _audioSource.Play();
        });
    }

    // Volta a playlist com a rotacao de sempre.
    public void ResumePlaylist(float fade = MusicFade.DefaultDuration)
    {
        if (_themeClip == null)
            return;
        _themeClip = null;
        CrossFade(fade, PlayNextTrack);
    }

    private void ApplyMood()
    {
        if (_moods.Current == MusicMood.Sequence)
            PlayTheme(_sequenceClip, _sequenceLoop);
        else if (_moods.Current == MusicMood.Crisis && _themes != null && _themes.Crisis != null)
            PlayTheme(_themes.Crisis, true);
        else
            ResumePlaylist();
    }

    private void CrossFade(float duration, System.Action swap)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        // Sem nada tocando nao ha o que esmaecer: entra direto subindo o volume.
        var playing = _audioSource.isPlaying && _audioSource.clip != null;
        if (duration <= 0f || isActiveAndEnabled == false)
        {
            swap();
            _audioSource.volume = _baseVolume;
            _fadeRoutine = null;
            return;
        }
        _fadeRoutine = StartCoroutine(FadeRoutine(playing ? duration / 2f : 0f, duration / 2f, swap));
    }

    private IEnumerator FadeRoutine(float outDuration, float inDuration, System.Action swap)
    {
        // Tempo real: a pausa zera o tempo do jogo, e a musica nao pode travar no meio da fusao.
        var start = _audioSource.volume;
        for (var elapsed = 0f; elapsed < outDuration; elapsed += Time.unscaledDeltaTime)
        {
            _audioSource.volume = MusicFade.VolumeAt(elapsed, outDuration, start, 0f);
            yield return null;
        }

        _audioSource.volume = 0f;
        swap();
        for (var elapsed = 0f; elapsed < inDuration; elapsed += Time.unscaledDeltaTime)
        {
            _audioSource.volume = MusicFade.VolumeAt(elapsed, inDuration, 0f, _baseVolume);
            yield return null;
        }

        _audioSource.volume = _baseVolume;
        _fadeRoutine = null;
    }

    private void StopRotation()
    {
        if (_playbackRoutine != null)
        {
            StopCoroutine(_playbackRoutine);
            _playbackRoutine = null;
        }
    }

    private void PlayNextTrack()
    {
        StopRotation();

        var tracks = _currentPlaylist != null ? _currentPlaylist.Tracks : null;
        if (tracks == null || tracks.Count == 0)
        {
            _audioSource.Stop();
            return;
        }

        var clip = tracks[_rotation.Next(tracks.Count, _random)];
        _audioSource.clip = clip;
        _audioSource.loop = false;
        _audioSource.Play();
        _playbackRoutine = StartCoroutine(WaitAndPlayNext(clip.length));
    }

    private IEnumerator WaitAndPlayNext(float delaySeconds)
    {
        // Tempo real, nao escalado: o AudioSource ignora Time.timeScale, entao a espera precisa acompanhar.
        yield return new WaitForSecondsRealtime(delaySeconds);
        PlayNextTrack();
    }
}
