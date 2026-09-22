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

        // MusicPlayer.Instance e estatico e sobrevive normalmente entre cenas, entao limpa entre testes pra nao vazar pro proximo.
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

    // Start() e chamado so no proximo ciclo de Update do Unity, entao um unico yield return null nao garante
    // que ja rodou; espera ate PlayNextTrack ja ter atribuido um clipe (ou desiste depois de alguns frames).
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

    // Recria o cenario do bug original: a instancia duplicada e destruida no fim do frame,
    // mas o Start() dela ainda roda antes disso; sem a guarda isso lancava NullReferenceException.
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

    // Regressao do bug de pausa: com WaitForSeconds (escalado) a musica silenciava porque o AudioSource
    // ignora Time.timeScale mas a coroutine ficava travada; com WaitForSecondsRealtime ela acompanha.
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
        yield return new WaitForSecondsRealtime(0.5f);

        Assert.That(audioSource.clip, Is.Not.SameAs(initialClip));
    }
}
