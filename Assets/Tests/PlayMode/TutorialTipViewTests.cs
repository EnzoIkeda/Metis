using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Dicas montadas de verdade: ordem do primeiro turno, popup que bloqueia, "Saiba mais" e nenhum numero mudando.
public class TutorialTipViewTests
{
    private readonly List<Object> _created = new List<Object>();
    private GameObject _host;
    private GameObject _ui;
    private string _saveDirectory;

    private TurnManager _turnManager;
    private CityStatsManager _statsManager;
    private TutorialTipView _view;
    private GameObject _balloon;
    private GameObject _blocker;
    private Button _ok;
    private Button _learnMore;
    private KnowledgePopupView _knowledge;
    private KnowledgeEntryData _entry;

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-tutorial-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_saveDirectory, "run_save.json"));
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(TutorialProgress), "_prefsKeyOverride", "Metis.Tutorial.Tests");
        TestDataFactory.SetStaticField(typeof(TutorialProgress), "_state", null);
        TutorialProgress.Reset();
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_prefsKeyOverride", "Metis.Almanac.TutorialTests");
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_state", null);
        AlmanacProgress.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        if (_ui != null)
            Object.DestroyImmediate(_ui);
        if (_host != null)
            Object.DestroyImmediate(_host);
        foreach (var created in _created)
        {
            if (created != null)
                Object.DestroyImmediate(created);
        }
        _created.Clear();
        TutorialProgress.Reset();
        TestDataFactory.SetStaticField(typeof(TutorialProgress), "_prefsKeyOverride", null);
        TestDataFactory.SetStaticField(typeof(TutorialProgress), "_state", null);
        AlmanacProgress.Reset();
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_prefsKeyOverride", null);
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_state", null);
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_saveDirectory))
            Directory.Delete(_saveDirectory, true);
    }

    private static GameObject Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private IEnumerator Build()
    {
        var card = TestDataFactory.CreateCard("Carta");
        _created.Add(card);
        _entry = ScriptableObject.CreateInstance<KnowledgeEntryData>();
        _entry.name = "Conceito_Teste";
        _created.Add(_entry);

        _host = new GameObject("Host");
        _host.SetActive(false);
        _statsManager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(_statsManager, "Reset");
        _turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(_turnManager, "_cityStatsManager", _statsManager);
        TestDataFactory.SetField(_turnManager, "_cardPool", new[] { card });
        TestDataFactory.SetField(_turnManager, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(_turnManager, "_advantagePool", new PassiveAdvantageData[0]);

        _ui = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _ui.SetActive(false);
        _blocker = Ui("Popup", _ui.transform);
        _blocker.SetActive(false);

        var knowledgePanel = Ui("KnowledgePanel", _ui.transform);
        _knowledge = Ui("KnowledgeController", _ui.transform).AddComponent<KnowledgePopupView>();
        TestDataFactory.SetField(_knowledge, "_panelRoot", knowledgePanel);

        var root = Ui("TutorialTip", _ui.transform);
        var balloon = (RectTransform)Ui("Balloon", root.transform).transform;
        balloon.sizeDelta = new Vector2(900f, 400f);
        _balloon = balloon.gameObject;
        var title = Ui("Title", balloon).AddComponent<TextMeshProUGUI>();
        var message = Ui("Message", balloon).AddComponent<TextMeshProUGUI>();
        _ok = Ui("Ok", balloon).AddComponent<Button>();
        _learnMore = Ui("LearnMore", balloon).AddComponent<Button>();

        _view = root.AddComponent<TutorialTipView>();
        TestDataFactory.SetField(_view, "_turnManager", _turnManager);
        TestDataFactory.SetField(_view, "_cityStatsManager", _statsManager);
        TestDataFactory.SetField(_view, "_knowledgePopup", _knowledge);
        TestDataFactory.SetField(_view, "_balloon", balloon);
        TestDataFactory.SetField(_view, "_titleText", (TMP_Text)title);
        TestDataFactory.SetField(_view, "_messageText", (TMP_Text)message);
        TestDataFactory.SetField(_view, "_okButton", _ok);
        TestDataFactory.SetField(_view, "_learnMoreButton", _learnMore);
        TestDataFactory.SetField(_view, "_pollSeconds", 0.01f);
        TestDataFactory.SetField(_view, "_blockingPanels", new[] { _blocker });
        TestDataFactory.SetField(_view, "_bindings", new[]
        {
            new TutorialTipView.TipBinding { Tip = TutorialTip.Parameters, Entry = _entry },
        });

        _host.SetActive(true);
        _ui.SetActive(true);
        yield return WaitFor(() => _turnManager.Machine != null && _turnManager.Machine.CurrentPhase == TurnPhase.Action);
    }

    private static IEnumerator WaitFor(System.Func<bool> condition)
    {
        var timeout = Time.realtimeSinceStartup + 20f;
        while (condition() == false)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(timeout), "tempo esgotado");
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator FirstTurn_ShowsParametersThenCards_AndDoesNotRepeat()
    {
        yield return Build();
        yield return WaitFor(() => _view.Current == TutorialTip.Parameters);
        Assert.That(_balloon.activeSelf, Is.True);

        _ok.onClick.Invoke();
        Assert.That(TutorialProgress.WasSeen(TutorialTip.Parameters), Is.True);
        yield return WaitFor(() => _view.Current == TutorialTip.Cards);

        _ok.onClick.Invoke();
        yield return WaitFor(() => _view.Current != TutorialTip.Cards);
        Assert.That(_view.Current, Is.Not.EqualTo(TutorialTip.Parameters));
    }

    [UnityTest]
    public IEnumerator OpenPopup_HidesTheTip_WithoutMarkingItSeen()
    {
        yield return Build();
        yield return WaitFor(() => _view.Current == TutorialTip.Parameters);

        _blocker.SetActive(true);
        yield return WaitFor(() => _view.Current == TutorialTip.None);
        Assert.That(_balloon.activeSelf, Is.False);
        Assert.That(TutorialProgress.WasSeen(TutorialTip.Parameters), Is.False);

        _blocker.SetActive(false);
        yield return WaitFor(() => _view.Current == TutorialTip.Parameters);
    }

    [UnityTest]
    public IEnumerator LearnMore_UnlocksAndOpensTheEntry()
    {
        yield return Build();
        yield return WaitFor(() => _view.Current == TutorialTip.Parameters);
        Assert.That(_learnMore.gameObject.activeSelf, Is.True);

        _learnMore.onClick.Invoke();

        Assert.That(TutorialProgress.WasSeen(TutorialTip.Parameters), Is.True);
        Assert.That(AlmanacProgress.IsUnlocked(_entry), Is.True);
        Assert.That(_knowledge.Current, Is.SameAs(_entry));
    }

    [UnityTest]
    public IEnumerator TipWithoutEntry_HidesLearnMore()
    {
        yield return Build();
        yield return WaitFor(() => _view.Current == TutorialTip.Parameters);
        _ok.onClick.Invoke();
        yield return WaitFor(() => _view.Current == TutorialTip.Cards);

        Assert.That(_learnMore.gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator Tips_DoNotChangeAnyNumber()
    {
        yield return Build();
        var stats = _statsManager.Stats;
        var before = new Dictionary<CityParameterType, float>();
        foreach (CityParameterType parameter in System.Enum.GetValues(typeof(CityParameterType)))
            before[parameter] = stats.GetValue(parameter);

        yield return WaitFor(() => _view.Current == TutorialTip.Parameters);
        _ok.onClick.Invoke();
        yield return WaitFor(() => _view.Current == TutorialTip.Cards);
        _ok.onClick.Invoke();

        foreach (var pair in before)
            Assert.That(stats.GetValue(pair.Key), Is.EqualTo(pair.Value), pair.Key.ToString());
    }

    [Test]
    public void Progress_SurvivesACacheReload_AndResetClearsIt()
    {
        TutorialProgress.MarkSeen(TutorialTip.Crisis);
        TestDataFactory.SetStaticField(typeof(TutorialProgress), "_state", null);
        Assert.That(TutorialProgress.WasSeen(TutorialTip.Crisis), Is.True);

        TutorialProgress.Reset();
        Assert.That(TutorialProgress.WasSeen(TutorialTip.Crisis), Is.False);
    }
}
