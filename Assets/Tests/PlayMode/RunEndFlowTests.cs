using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Fim de rodada: derrota e vitoria contra o chefe passam pelas telas e pelo resumo; vitoria de fase segue como antes.
public class RunEndFlowTests
{
    private string _directory;
    private GameObject _host;
    private GameObject _ui;
    private GameObject _musicHost;
    private TurnManager _turnManager;
    private RunEndFlow _flow;
    private SlideSequenceView _sequence;
    private GameObject _sequenceRoot;
    private RunSummaryPopupView _summary;
    private GameObject _summaryPanel;
    private GameObject _defeatPanel;
    private GameObject _victoryPanel;
    private MusicPlayer _player;
    private AudioClip _defeatClip;
    private AudioClip _victoryClip;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "metis-runend-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_directory, "save.json"));
        SessionMode.EnterRoguelike();
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in new[] { _ui, _host, _musicHost })
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private static GameObject Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetPlain(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static BossLibraryData BossLibrary()
    {
        var cards = Enumerable.Range(0, 6).Select(i =>
        {
            var card = new BossCardEntry();
            SetPlain(card, "_id", "Golpe" + i);
            SetPlain(card, "_cardName", "Golpe " + i);
            SetPlain(card, "_description", "Descricao");
            SetPlain(card, "_momentumCost", 99);
            SetPlain(card, "_effects", new[] { new StatModifier { Parameter = CityParameterType.Saude, Amount = -1f } });
            return card;
        }).ToArray();
        var boss = ScriptableObject.CreateInstance<BossData>();
        boss.name = "Chefe";
        TestDataFactory.SetField(boss, "_bossName", "Chefe do teste");
        TestDataFactory.SetField(boss, "_intro", "Abertura");
        TestDataFactory.SetField(boss, "_victory", "Vitoria da rodada");
        TestDataFactory.SetField(boss, "_targetArchetype", CardArchetype.Industria);
        TestDataFactory.SetField(boss, "_momentumPerTurn", 1);
        TestDataFactory.SetField(boss, "_mechanic", BossMechanic.GrowingDamage);
        TestDataFactory.SetField(boss, "_mechanicAmount", 0.5f);
        TestDataFactory.SetField(boss, "_cards", cards);
        var library = ScriptableObject.CreateInstance<BossLibraryData>();
        TestDataFactory.SetField(library, "_bosses", new[] { boss });
        return library;
    }

    private void CreatePlayer()
    {
        _defeatClip = TestDataFactory.CreateSilentClip("Defeat", 5f);
        _victoryClip = TestDataFactory.CreateSilentClip("Victory", 5f);
        var themes = ScriptableObject.CreateInstance<ThemeMusicData>();
        TestDataFactory.SetField(themes, "_defeat", _defeatClip);
        TestDataFactory.SetField(themes, "_victory", _victoryClip);
        _musicHost = new GameObject("MusicPlayer");
        _musicHost.SetActive(false);
        _player = _musicHost.AddComponent<MusicPlayer>();
        TestDataFactory.SetField(_player, "_defaultPlaylist", TestDataFactory.CreatePlaylist(TestDataFactory.CreateSilentClip("Track", 30f)));
        TestDataFactory.SetField(_player, "_themes", themes);
        _musicHost.SetActive(true);
    }

    private GameOutcomePopupView OutcomePopup(GameOutcome outcome, GameObject panel, PhaseEndFlow endFlow)
    {
        var popup = Ui("Outcome" + outcome, _ui.transform).AddComponent<GameOutcomePopupView>();
        TestDataFactory.SetField(popup, "_turnManager", _turnManager);
        TestDataFactory.SetField(popup, "_endFlow", endFlow);
        TestDataFactory.SetField(popup, "_outcomeToShowFor", outcome);
        TestDataFactory.SetField(popup, "_panelRoot", panel);
        TestDataFactory.SetField(popup, "_runEnd", _flow);
        return popup;
    }

    private void BuildCity(BossLibraryData bosses = null)
    {
        CreatePlayer();
        var card = TestDataFactory.CreateCard("Neutra");
        var advantage = ScriptableObject.CreateInstance<PassiveAdvantageData>();
        advantage.name = "VantagemTeste";
        TestDataFactory.SetField(advantage, "_advantageName", "Vantagem de teste");
        TestDataFactory.SetField(advantage, "_statEffects", new StatModifier[0]);

        _host = new GameObject("Host");
        _host.SetActive(false);
        var stats = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(stats, "Reset");
        _turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(_turnManager, "_cityStatsManager", stats);
        TestDataFactory.SetField(_turnManager, "_cardPool", new[] { card });
        TestDataFactory.SetField(_turnManager, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(_turnManager, "_advantagePool", new[] { advantage });
        TestDataFactory.SetField(_turnManager, "_bosses", bosses);

        _ui = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _ui.SetActive(false);
        var endFlow = Ui("PhaseEndFlow", _ui.transform).AddComponent<PhaseEndFlow>();
        TestDataFactory.SetField(endFlow, "_turnManager", _turnManager);

        _sequenceRoot = Ui("RunEndSequence", _ui.transform);
        _sequence = _sequenceRoot.AddComponent<SlideSequenceView>();
        var area = Ui("Area", _sequenceRoot.transform).GetComponent<RectTransform>();
        var texture = new Texture2D(4, 4);
        TestDataFactory.SetField(_sequence, "_canvasRect", (RectTransform)_ui.transform);
        TestDataFactory.SetField(_sequence, "_slideArea", area);
        TestDataFactory.SetField(_sequence, "_backImage", Ui("Back", area).AddComponent<Image>());
        TestDataFactory.SetField(_sequence, "_frontImage", Ui("Front", area).AddComponent<Image>());
        TestDataFactory.SetField<TMP_Text>(_sequence, "_captionText", Ui("Caption", _sequenceRoot.transform).AddComponent<TextMeshProUGUI>());
        TestDataFactory.SetField(_sequence, "_skipButton", Ui("Skip", _sequenceRoot.transform).AddComponent<Button>());
        TestDataFactory.SetField(_sequence, "_slides", Enumerable.Range(0, RunIntroController.SlideCount)
            .Select(_ => Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one / 2f)).ToArray());

        _summaryPanel = Ui("SummaryPanel", _ui.transform);
        _summary = Ui("SummaryController", _ui.transform).AddComponent<RunSummaryPopupView>();
        TestDataFactory.SetField(_summary, "_turnManager", _turnManager);
        TestDataFactory.SetField(_summary, "_panelRoot", _summaryPanel);
        TestDataFactory.SetField<TMP_Text>(_summary, "_titleText", Ui("Title", _summaryPanel.transform).AddComponent<TextMeshProUGUI>());
        TestDataFactory.SetField<TMP_Text>(_summary, "_linesText", Ui("Lines", _summaryPanel.transform).AddComponent<TextMeshProUGUI>());
        TestDataFactory.SetField(_summary, "_menuButton", Ui("Menu", _summaryPanel.transform).AddComponent<Button>());
        TestDataFactory.SetField(_summary, "_bosses", bosses);
        TestDataFactory.SetField(_summary, "_loadMenu", false);

        _flow = Ui("RunEndController", _ui.transform).AddComponent<RunEndFlow>();
        TestDataFactory.SetField(_flow, "_turnManager", _turnManager);
        TestDataFactory.SetField(_flow, "_endFlow", endFlow);
        TestDataFactory.SetField(_flow, "_sequenceRoot", _sequenceRoot);
        TestDataFactory.SetField(_flow, "_sequence", _sequence);
        TestDataFactory.SetField(_flow, "_summary", _summary);

        _defeatPanel = Ui("DefeatPanel", _ui.transform);
        _victoryPanel = Ui("VictoryPanel", _ui.transform);
        OutcomePopup(GameOutcome.GameOver, _defeatPanel, endFlow);
        OutcomePopup(GameOutcome.Victory, _victoryPanel, endFlow);

        _host.SetActive(true);
        _ui.SetActive(true);
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds = 5f)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        while (condition() == false && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    [UnityTest]
    public IEnumerator Defeat_ShowsTheRuinSlidesThenTheSummaryAndGoesBackToTheMenu()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Sustentabilidade);
        MetaProgressionManager.AddAdvantageReward("VantagemTeste");
        MetaProgressionManager.IncrementPhasesCompleted();
        BuildCity();
        yield return null;
        yield return null;
        Assert.That(_sequenceRoot.activeSelf, Is.False);
        Assert.That(_summaryPanel.activeSelf, Is.False);

        _turnManager.Machine.DebugForceGameOver();
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.False, "a rodada e apagada na hora");
        Assert.That(_turnManager.EndedRun, Is.Not.Null);
        Assert.That(_turnManager.EndedRun.EndPhase, Is.EqualTo(2));

        Assert.That(_defeatPanel.activeSelf, Is.False, "o popup de derrota nao abre no fim de rodada");
        Assert.That(_sequenceRoot.activeSelf, Is.True);
        Assert.That(_sequence.Sequence.Steps.Select(step => step.ImageIndex), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(_sequence.CurrentCaption, Is.EqualTo(UIStrings.SlideCaption("defeat.1", CardArchetype.Sustentabilidade)));
        Assert.That(_player.Mood, Is.EqualTo(MusicMood.Sequence));
        Assert.That(_player.CurrentTheme, Is.SameAs(_defeatClip));

        _sequence.Skip();
        Assert.That(_sequenceRoot.activeSelf, Is.False);
        Assert.That(_summary.IsOpen, Is.True);
        Assert.That(_summary.Summary.Ending, Is.EqualTo(RunEnding.Defeat));
        Assert.That(_summary.LinesText, Does.Contain("Vantagem de teste"));
        Assert.That(_summary.LinesText, Does.Contain(UIStrings.RunSummaryLine(RunSummaryLineKind.DefeatPhase, 2, CardArchetype.Sustentabilidade, "")));
        Assert.That(_player.Mood, Is.EqualTo(MusicMood.Sequence), "silencio ate o resumo fechar");

        _summaryPanel.GetComponentInChildren<Button>().onClick.Invoke();
        Assert.That(_summary.ReturnedToMenu, Is.True);
        Assert.That(_summary.IsOpen, Is.False);
        Assert.That(_player.Mood, Is.EqualTo(MusicMood.Playlist));
    }

    [UnityTest]
    public IEnumerator Defeat_SlidesAdvanceByTimeToTheSummary()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Industria);
        BuildCity();
        yield return null;
        yield return null;

        _turnManager.Machine.DebugForceGameOver();
        _sequence.Tap();
        _sequence.Tap();
        Assert.That(_sequence.Sequence.CurrentIndex, Is.EqualTo(1));
        yield return WaitUntil(() => _summary.IsOpen, 10f);
        Assert.That(_summary.IsOpen, Is.True, "a ultima tela acaba sozinha");
    }

    [UnityTest]
    public IEnumerator BossVictory_ShowsTheDreamSlidesAndTheBossInTheSummary()
    {
        var bosses = BossLibrary();
        MetaProgressionManager.SetArchetype(CardArchetype.Industria);
        MetaProgressionManager.ChooseMapNode(PhaseMapProvider.BossOffer("Chefe")[0]);
        BuildCity(bosses);
        yield return null;
        yield return null;
        Assert.That(_turnManager.Boss, Is.Not.Null);

        _turnManager.Machine.DebugForceVictory();
        Assert.That(_victoryPanel.activeSelf, Is.False, "o popup de vitoria nao abre depois do chefe");
        Assert.That(_sequence.Sequence.Steps.Select(step => step.ImageIndex), Is.EqualTo(new[] { 5, 6, 7, 8, 9 }));
        Assert.That(_player.CurrentTheme, Is.SameAs(_victoryClip));

        _sequence.Skip();
        Assert.That(_summary.Summary.Ending, Is.EqualTo(RunEnding.BossVictory));
        Assert.That(_summary.LinesText, Does.Contain("Chefe do teste"));
    }

    [UnityTest]
    public IEnumerator PhaseVictory_KeepsTheUsualPopup()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Automacao);
        BuildCity();
        yield return null;
        yield return null;

        _turnManager.Machine.DebugForceVictory();
        Assert.That(_turnManager.EndedRun, Is.Null);
        Assert.That(_victoryPanel.activeSelf, Is.True);
        Assert.That(_sequenceRoot.activeSelf, Is.False);
        Assert.That(_summary.IsOpen, Is.False);
        Assert.That(_player.Mood, Is.EqualTo(MusicMood.Playlist));
    }
}
