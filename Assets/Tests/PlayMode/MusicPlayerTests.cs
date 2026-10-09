using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MusicPlayerTests
{
    private GameObject _extraHost;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;

        // A instancia unica e estatica e sobrevive entre cenas, entao e destruida entre testes.
        if (MusicPlayer.Instance != null)
            Object.DestroyImmediate(MusicPlayer.Instance.gameObject);
        if (_extraHost != null)
            Object.DestroyImmediate(_extraHost);
    }

    private static MusicPlayer CreatePlayer(MusicPlaylistData playlist)
    {
        var host = new GameObject("MusicPlayer");
        host.SetActive(false);
        var player = host.AddComponent<MusicPlayer>();
        TestDataFactory.SetField(player, "_defaultPlaylist", playlist);
        host.SetActive(true);
        return player;
    }

    // O Start so roda no proximo Update: espera o primeiro clipe ser atribuido, com limite de frames.
    private static IEnumerator WaitUntilClipAssigned(AudioSource audioSource, int maxFrames = 10)
    {
        var frames = 0;
        while (audioSource.clip == null && frames < maxFrames)
        {
            yield return null;
            frames++;
        }
    }

    [UnityTest]
    public IEnumerator Awake_StartsPlayingATrackFromTheDefaultPlaylist()
    {
        var clipA = TestDataFactory.CreateSilentClip("A", 0.2f);
        var clipB = TestDataFactory.CreateSilentClip("B", 0.2f);
        var player = CreatePlayer(TestDataFactory.CreatePlaylist(clipA, clipB));
        var audioSource = player.GetComponent<AudioSource>();
        yield return WaitUntilClipAssigned(audioSource);

        Assert.That(MusicPlayer.Instance, Is.SameAs(player));
        Assert.That(audioSource.clip == clipA || audioSource.clip == clipB, Is.True);
    }

    // Bug original: a duplicata so e destruida no fim do frame, mas o Start dela ainda rodava e lancava excecao.
    [UnityTest]
    public IEnumerator DuplicateInstance_ActivatedRightAfterTheFirst_DoesNotThrowAndOnlyOneSurvives()
    {
        var playlist = TestDataFactory.CreatePlaylist(TestDataFactory.CreateSilentClip("A", 0.2f));
        var first = CreatePlayer(playlist);
        var firstAudioSource = first.GetComponent<AudioSource>();
        yield return WaitUntilClipAssigned(firstAudioSource);

        _extraHost = new GameObject("MusicPlayerDuplicate");
        _extraHost.SetActive(false);
        var duplicate = _extraHost.AddComponent<MusicPlayer>();
        TestDataFactory.SetField(duplicate, "_defaultPlaylist", playlist);
        _extraHost.SetActive(true);

        yield return null;
        yield return null;

        var survivors = Object.FindObjectsByType<MusicPlayer>(FindObjectsSortMode.None);
        Assert.That(survivors, Has.Length.EqualTo(1));
        Assert.That(MusicPlayer.Instance, Is.SameAs(first));
        Assert.That(firstAudioSource.clip, Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator SetPlaylist_SwitchesToATrackFromTheNewPlaylist()
    {
        var oldClip = TestDataFactory.CreateSilentClip("Old", 0.2f);
        var newClip = TestDataFactory.CreateSilentClip("New", 0.2f);
        var player = CreatePlayer(TestDataFactory.CreatePlaylist(oldClip));
        yield return WaitUntilClipAssigned(player.GetComponent<AudioSource>());

        player.SetPlaylist(TestDataFactory.CreatePlaylist(newClip));

        Assert.That(player.GetComponent<AudioSource>().clip, Is.SameAs(newClip));
    }

    // Regressao da pausa: a espera escalada travava com o tempo zerado e a musica silenciava.
    // Observa a troca a cada frame; checar num instante fixo falhava quando a faixa trocava duas vezes.
    [UnityTest]
    public IEnumerator WhilePausedWithZeroTimeScale_StillAdvancesToNextTrackInRealTime()
    {
        var clipA = TestDataFactory.CreateSilentClip("A", 0.2f);
        var clipB = TestDataFactory.CreateSilentClip("B", 0.2f);
        var player = CreatePlayer(TestDataFactory.CreatePlaylist(clipA, clipB));
        var audioSource = player.GetComponent<AudioSource>();
        yield return WaitUntilClipAssigned(audioSource);
        var initialClip = audioSource.clip;

        Time.timeScale = 0f;
        var deadline = Time.realtimeSinceStartup + 2f;
        while (audioSource.clip == initialClip && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.That(audioSource.clip, Is.Not.SameAs(initialClip), "a faixa nao trocou em 2 s de tempo real com o jogo pausado");
    }
}
