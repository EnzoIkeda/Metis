using System.Collections;
using UnityEngine;

// Toca a trilha de fundo do jogo, rotacionando entre as faixas da playlist ativa.
// Singleton persistente entre cenas (DontDestroyOnLoad); uma instancia em cada cena garante que sempre exista uma, sem duplicar.
// A playlist ativa pode ser trocada em runtime, ponto de extensao pra usar uma trilha especifica numa aba/cena no futuro.
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    [SerializeField] private MusicPlaylistData _defaultPlaylist;

    private AudioSource _audioSource;
    private MusicPlaylistData _currentPlaylist;
    private readonly MusicRotation _rotation = new MusicRotation();
    private readonly System.Random _random = new System.Random();
    private Coroutine _playbackRoutine;

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
        _currentPlaylist = _defaultPlaylist;
    }

    private void Start()
    {
        // Uma instancia duplicada so e destruida no fim do frame: sem essa checagem ela ainda tentaria tocar musica antes disso.
        if (Instance != this)
            return;

        PlayNextTrack();
    }

    // Troca a playlist ativa e reinicia a rotacao nela.
    public void SetPlaylist(MusicPlaylistData playlist)
    {
        _currentPlaylist = playlist;
        PlayNextTrack();
    }

    private void PlayNextTrack()
    {
        if (_playbackRoutine != null)
        {
            StopCoroutine(_playbackRoutine);
            _playbackRoutine = null;
        }

        var tracks = _currentPlaylist != null ? _currentPlaylist.Tracks : null;
        if (tracks == null || tracks.Count == 0)
        {
            _audioSource.Stop();
            return;
        }

        var clip = tracks[_rotation.Next(tracks.Count, _random)];
        _audioSource.clip = clip;
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
