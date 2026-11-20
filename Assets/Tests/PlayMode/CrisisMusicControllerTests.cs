using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CrisisMusicControllerTests
{
    private GameObject _host;
    private GameObject _musicHost;
    private string _saveDirectory;

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-crisis-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_saveDirectory, "run_save.json"));
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null)
            Object.DestroyImmediate(_host);
        if (_musicHost != null)
            Object.DestroyImmediate(_musicHost);
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_saveDirectory))
            Directory.Delete(_saveDirectory, true);
    }

    private AudioClip _track;
    private AudioClip _crisis;
    private AudioClip _opening;

    private MusicPlayer CreatePlayer()
    {
        _track = TestDataFactory.CreateSilentClip("Track", 30f);
        _crisis = TestDataFactory.CreateSilentClip("Crisis", 30f);
        _opening = TestDataFactory.CreateSilentClip("Opening", 30f);
        var themes = ScriptableObject.CreateInstance<ThemeMusicData>();
        TestDataFactory.SetField(themes, "_opening", _opening);
        TestDataFactory.SetField(themes, "_crisis", _crisis);

        _musicHost = new GameObject("MusicPlayer");
        _musicHost.SetActive(false);
        var player = _musicHost.AddComponent<MusicPlayer>();
        TestDataFactory.SetField(player, "_defaultPlaylist", TestDataFactory.CreatePlaylist(_track));
        TestDataFactory.SetField(player, "_themes", themes);
        _musicHost.SetActive(true);
        return player;
    }

    // Monta a fase com os valores dados ja aplicados, como se viessem de um save.
    private (TurnManager turnManager, CityStatsManager stats, CrisisMusicController crisis) CreatePhase(float energia, CardData card)
    {
        _host = new GameObject("Phase");
        _host.SetActive(false);
        var statsManager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");
        var effects = _host.AddComponent<CityEffectsController>();
        var turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(turnManager, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(turnManager, "_cardPool", new[] { card });
        TestDataFactory.SetField(turnManager, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(turnManager, "_advantagePool", new PassiveAdvantageData[0]);
        TestDataFactory.SetField(turnManager, "_effects", effects);
        TestDataFactory.SetField(turnManager, "_handSize", 1);
        var crisis = _host.AddComponent<CrisisMusicController>();
        TestDataFactory.SetField(crisis, "_turnManager", turnManager);
        TestDataFactory.SetField(crisis, "_cityStatsManager", statsManager);
        _host.SetActive(true);
        SetAll(statsManager.Stats, 55f, energia);
        return (turnManager, statsManager, crisis);
    }

    private static void SetAll(CityStats stats, float value, float energia)
    {
        var values = new Dictionary<CityParameterType, float>();
        foreach (CityParameterType parameter in System.Enum.GetValues(typeof(CityParameterType)))
            values[parameter] = parameter == CityParameterType.Energia ? energia : value;
        stats.RestoreState(values, stats.AnchorBonus);
        stats.RecomputeDerivedParameters();
    }

    private static IEnumerator WaitForClip(AudioSource source, AudioClip clip, float seconds = 4f)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        while ((source.clip != clip || source.volume < 0.999f) && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    private static IEnumerator PlayTurn(TurnManager turnManager, CardData card)
    {
        var turn = turnManager.Machine.TurnIndex;
        Assert.That(turnManager.PlayCard(card), Is.True);
        var deadline = Time.realtimeSinceStartup + 6f;
        while (turnManager.Machine.TurnIndex == turn && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(turn + 1));
    }

    [UnityTest]
    public IEnumerator EnteringCrisis_SwitchesToTheCrisisTrack_AndLeavingWithMarginReturns()
    {
        var player = CreatePlayer();
        var source = player.GetComponent<AudioSource>();
        var card = TestDataFactory.CreateCard();
        var phase = CreatePhase(60f, card);
        yield return null;
        yield return null;
        Assert.That(player.Mood, Is.EqualTo(MusicMood.Playlist));

        SetAll(phase.stats.Stats, 55f, 10f);
        yield return PlayTurn(phase.turnManager, card);
        Assert.That(phase.crisis.IsCrisis, Is.True);
        Assert.That(player.Mood, Is.EqualTo(MusicMood.Crisis));
        yield return WaitForClip(source, _crisis);
        Assert.That(source.clip, Is.SameAs(_crisis));
        Assert.That(source.loop, Is.True);

        SetAll(phase.stats.Stats, 60f, 60f);
        yield return PlayTurn(phase.turnManager, card);
        Assert.That(phase.crisis.IsCrisis, Is.False);
        Assert.That(player.Mood, Is.EqualTo(MusicMood.Playlist));
        yield return WaitForClip(source, _track);
        Assert.That(source.clip, Is.SameAs(_track));
    }

    [UnityTest]
    public IEnumerator PhaseLoadedInCrisis_StartsOnTheCrisisTrack()
    {
        var player = CreatePlayer();
        var source = player.GetComponent<AudioSource>();
        var card = TestDataFactory.CreateCard();

        // Valores aplicados antes do Start, como os de um save restaurado no Awake.
        var (_, _, crisis) = CreatePhase(12f, card);
        yield return null;
        yield return null;

        Assert.That(crisis.IsCrisis, Is.True);
        yield return WaitForClip(source, _crisis);
        Assert.That(source.clip, Is.SameAs(_crisis));
    }

    [UnityTest]
    public IEnumerator Sequence_WinsOverCrisis_AndCrisisTakesOverWhenItEnds()
    {
        var player = CreatePlayer();
        var source = player.GetComponent<AudioSource>();
        yield return null;
        player.PlaySequence(_opening, true);

        var card = TestDataFactory.CreateCard();
        CreatePhase(60f, card);
        SetAll(_host.GetComponent<CityStatsManager>().Stats, 55f, 12f);
        yield return null;
        yield return null;

        Assert.That(player.Mood, Is.EqualTo(MusicMood.Sequence), "a crise nunca corta a abertura");
        yield return WaitForClip(source, _opening);
        Assert.That(source.clip, Is.SameAs(_opening));

        player.EndSequence();
        Assert.That(player.Mood, Is.EqualTo(MusicMood.Crisis));
        yield return WaitForClip(source, _crisis);
        Assert.That(source.clip, Is.SameAs(_crisis));
    }

    [UnityTest]
    public IEnumerator EndOfPhaseAndLeavingTheCity_StopTheCrisis()
    {
        var player = CreatePlayer();
        var card = TestDataFactory.CreateCard();
        var phase = CreatePhase(60f, card);
        SetAll(phase.stats.Stats, 55f, 12f);
        yield return null;
        yield return null;
        Assert.That(player.Mood, Is.EqualTo(MusicMood.Crisis));

        phase.turnManager.Machine.DebugForceVictory();
        Assert.That(player.Mood, Is.EqualTo(MusicMood.Playlist), "fim de fase sai da crise");

        Object.DestroyImmediate(_host);
        _host = null;
        player.SetCrisis(true);
        Assert.That(player.Mood, Is.EqualTo(MusicMood.Playlist), "fora da fase a crise nao volta");
        player.StartPhase();
        player.SetCrisis(true);
        Assert.That(player.Mood, Is.EqualTo(MusicMood.Crisis));
    }
}
