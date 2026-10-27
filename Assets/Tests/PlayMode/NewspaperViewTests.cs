using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Jornal montado de verdade: edicao de posse, edicao por turno, selo de nova edicao e bloqueio no popup de evento.
public class NewspaperViewTests
{
    private readonly List<Object> _created = new List<Object>();
    private GameObject _host;
    private GameObject _ui;
    private string _saveDirectory;

    private TurnManager _turnManager;
    private NewspaperView _view;
    private GameObject _panel;
    private Button _openButton;
    private RectTransform _badge;
    private TextMeshProUGUI _headline;
    private Button _previous;

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-news-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_saveDirectory, "run_save.json"));
        MetaProgressionManager.ResetRun();
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
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_saveDirectory))
            Directory.Delete(_saveDirectory, true);
    }

    private NewsStoryData Story(string id, NewsTrigger trigger, CityParameterType parameter = default)
    {
        var story = ScriptableObject.CreateInstance<NewsStoryData>();
        story.name = id;
        TestDataFactory.SetField(story, "_trigger", trigger);
        TestDataFactory.SetField(story, "_parameter", parameter);
        TestDataFactory.SetField(story, "_headline", "Manchete " + id);
        TestDataFactory.SetField(story, "_headlineEn", "Manchete " + id);
        TestDataFactory.SetField(story, "_body", "Texto {turno}");
        TestDataFactory.SetField(story, "_bodyEn", "Texto {turno}");
        _created.Add(story);
        return story;
    }

    private static GameObject Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private IEnumerator Build(CardData[] cards, RandomEventData[] events)
    {
        var library = ScriptableObject.CreateInstance<NewsLibraryData>();
        _created.Add(library);
        TestDataFactory.SetField(library, "_stories", new[]
        {
            Story("Posse", NewsTrigger.Inauguration),
            Story("Calmaria", NewsTrigger.QuietTurn),
            Story("ColapsoEnergia", NewsTrigger.EnteredCollapse, CityParameterType.Energia),
        });

        _host = new GameObject("Host");
        _host.SetActive(false);
        var statsManager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");
        _turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(_turnManager, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(_turnManager, "_cardPool", cards);
        TestDataFactory.SetField(_turnManager, "_eventPool", events);
        TestDataFactory.SetField(_turnManager, "_advantagePool", new PassiveAdvantageData[0]);
        var room = _host.AddComponent<NewsRoom>();
        TestDataFactory.SetField(room, "_turnManager", _turnManager);
        TestDataFactory.SetField(room, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(room, "_library", library);

        _ui = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _ui.SetActive(false);
        _openButton = Ui("ButtonNewspaper", _ui.transform).AddComponent<Button>();
        _badge = (RectTransform)Ui("Badge", _openButton.transform).transform;
        _panel = Ui("NewspaperPanel", _ui.transform);
        _headline = Ui("Headline", _panel.transform).AddComponent<TextMeshProUGUI>();
        _previous = Ui("Previous", _panel.transform).AddComponent<Button>();
        _view = Ui("NewspaperController", _ui.transform).AddComponent<NewspaperView>();
        TestDataFactory.SetField(_view, "_turnManager", _turnManager);
        TestDataFactory.SetField(_view, "_newsRoom", room);
        TestDataFactory.SetField(_view, "_openButton", _openButton);
        TestDataFactory.SetField(_view, "_badge", _badge);
        TestDataFactory.SetField(_view, "_panelRoot", _panel);
        TestDataFactory.SetField(_view, "_headlineText", (TMP_Text)_headline);
        TestDataFactory.SetField(_view, "_previousButton", _previous);

        _host.SetActive(true);
        _ui.SetActive(true);
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator Start_PublishesTheInauguration_AndTheBadgeShowsUntilOpened()
    {
        yield return Build(new[] { TestDataFactory.CreateCard() }, new RandomEventData[0]);

        Assert.That(_turnManager.News.Editions.Single().TurnIndex, Is.EqualTo(0));
        Assert.That(_panel.activeSelf, Is.False);
        Assert.That(_badge.gameObject.activeSelf, Is.True, "edicao nova acende o selo");

        _openButton.onClick.Invoke();
        yield return null;

        Assert.That(_panel.activeSelf, Is.True);
        Assert.That(_headline.text, Is.EqualTo("Manchete Posse"));
        Assert.That(_badge.gameObject.activeSelf, Is.False, "abrir o jornal apaga o selo");
    }

    [UnityTest]
    public IEnumerator Turn_WithCollapse_PublishesAStrongEdition_AndOlderEditionsCanBeBrowsed()
    {
        var crash = TestDataFactory.CreateCard("Desmonte", statEffects: new[] { new StatModifier { Parameter = CityParameterType.Energia, Amount = -45f } });
        yield return Build(new[] { crash }, new RandomEventData[0]);
        _openButton.onClick.Invoke();
        TestDataFactory.InvokePrivateMethod(_view, "Hide");

        Assert.That(_turnManager.PlayCard(crash), Is.True);
        yield return null;

        var latest = _turnManager.News.Latest;
        Assert.That(latest.TurnIndex, Is.EqualTo(1));
        Assert.That(latest.Items[0].StoryId, Is.EqualTo("ColapsoEnergia"));
        Assert.That(latest.Strong, Is.True);
        Assert.That(_turnManager.News.HasStrongUnread, Is.True);
        Assert.That(_badge.gameObject.activeSelf, Is.True);

        _openButton.onClick.Invoke();
        Assert.That(_headline.text, Is.EqualTo("Manchete ColapsoEnergia"));
        Assert.That(_previous.interactable, Is.True);

        _previous.onClick.Invoke();
        Assert.That(_headline.text, Is.EqualTo("Manchete Posse"));
        Assert.That(_previous.interactable, Is.False);
    }

    [UnityTest]
    public IEnumerator EventPopupOpen_ButtonIsDisabled()
    {
        var card = TestDataFactory.CreateCard();
        yield return Build(new[] { card }, new[] { TestDataFactory.CreateEvent("Chuva") });

        Assert.That(_turnManager.PlayCard(card), Is.True);
        yield return null;

        Assert.That(_turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Event));
        Assert.That(_openButton.interactable, Is.False);
        _view.Show();
        Assert.That(_panel.activeSelf, Is.False);

        _turnManager.AcknowledgeEvent();
        yield return null;
        Assert.That(_openButton.interactable, Is.True);
    }
}
