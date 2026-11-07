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
    private TextMeshProUGUI _quote;
    private TextMeshProUGUI _columnTitle;
    private TextMeshProUGUI _column;
    private Button _learnMore;
    private KnowledgePopupView _knowledgePopup;
    private GameObject _knowledgePanel;

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

    private NewsColumnData Column(string id, NewsColumnKind kind, NewsCitizen citizen, NewsMoodBand mood, string text)
    {
        var column = ScriptableObject.CreateInstance<NewsColumnData>();
        column.name = id;
        TestDataFactory.SetField(column, "_kind", kind);
        TestDataFactory.SetField(column, "_citizen", citizen);
        TestDataFactory.SetField(column, "_mood", mood);
        TestDataFactory.SetField(column, "_text", text);
        TestDataFactory.SetField(column, "_textEn", text);
        _created.Add(column);
        return column;
    }

    private NewsColumnData[] AllColumns()
    {
        var columns = new List<NewsColumnData> { Column("Classificado", NewsColumnKind.Classified, default, default, "Vendo canoa") };
        foreach (NewsMoodBand band in System.Enum.GetValues(typeof(NewsMoodBand)))
        {
            columns.Add(Column("Horoscopo" + band, NewsColumnKind.Horoscope, default, band, "Astros " + band));
            columns.Add(Column("Previsao" + band, NewsColumnKind.PollutionForecast, default, band, "Ar em {valor}"));
            foreach (NewsCitizen citizen in System.Enum.GetValues(typeof(NewsCitizen)))
                columns.Add(Column($"Fala{citizen}{band}", NewsColumnKind.CitizenQuote, citizen, band, $"Fala {citizen} {band}"));
        }
        return columns.ToArray();
    }

    private static GameObject Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private IEnumerator Build(CardData[] cards, RandomEventData[] events, NewsStoryData[] extraStories = null)
    {
        var library = ScriptableObject.CreateInstance<NewsLibraryData>();
        _created.Add(library);
        var stories = new List<NewsStoryData>
        {
            Story("Posse", NewsTrigger.Inauguration),
            Story("Calmaria", NewsTrigger.QuietTurn),
            Story("ColapsoEnergia", NewsTrigger.EnteredCollapse, CityParameterType.Energia),
        };
        if (extraStories != null)
            stories.AddRange(extraStories);
        TestDataFactory.SetField(library, "_stories", stories.ToArray());
        TestDataFactory.SetField(library, "_columns", AllColumns());

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
        _quote = Ui("Quote", _panel.transform).AddComponent<TextMeshProUGUI>();
        _columnTitle = Ui("ColumnTitle", _panel.transform).AddComponent<TextMeshProUGUI>();
        _column = Ui("Column", _panel.transform).AddComponent<TextMeshProUGUI>();
        _learnMore = Ui("LearnMore", _panel.transform).AddComponent<Button>();
        _knowledgePanel = Ui("KnowledgePanel", _ui.transform);
        var knowledgeController = Ui("KnowledgeController", _ui.transform);
        _knowledgePopup = knowledgeController.AddComponent<KnowledgePopupView>();
        TestDataFactory.SetField(_knowledgePopup, "_panelRoot", _knowledgePanel);
        _view = Ui("NewspaperController", _ui.transform).AddComponent<NewspaperView>();
        TestDataFactory.SetField(_view, "_quoteText", (TMP_Text)_quote);
        TestDataFactory.SetField(_view, "_columnTitleText", (TMP_Text)_columnTitle);
        TestDataFactory.SetField(_view, "_columnText", (TMP_Text)_column);
        TestDataFactory.SetField(_view, "_learnMoreButton", _learnMore);
        TestDataFactory.SetField(_view, "_knowledgePopup", _knowledgePopup);
        TestDataFactory.SetField(_view, "_turnManager", _turnManager);
        TestDataFactory.SetField(_view, "_newsRoom", room);
        TestDataFactory.SetField(_view, "_openButton", _openButton);
        TestDataFactory.SetField(_view, "_badge", _badge);
        TestDataFactory.SetField(_view, "_panelRoot", _panel);
        TestDataFactory.SetField(_view, "_paper", (RectTransform)_panel.transform);
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
    public IEnumerator Open_PaperArrivesTurned_AndEndsFlatAtFullSize()
    {
        yield return Build(new[] { TestDataFactory.CreateCard() }, new RandomEventData[0]);
        var paper = _panel.transform;

        _view.Show();
        Assert.That(paper.localScale.x, Is.LessThan(1f));
        Assert.That(paper.localRotation, Is.Not.EqualTo(Quaternion.identity));

        var limit = Time.realtimeSinceStartup + 5f;
        while (paper.localScale.x < 1f && Time.realtimeSinceStartup < limit)
            yield return null;

        Assert.That(paper.localScale, Is.EqualTo(Vector3.one));
        Assert.That(Quaternion.Angle(paper.localRotation, Quaternion.identity), Is.LessThan(0.01f));
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

    [UnityTest]
    public IEnumerator Edition_ShowsTheQuoteAndTheColumn()
    {
        yield return Build(new[] { TestDataFactory.CreateCard() }, new RandomEventData[0]);

        _openButton.onClick.Invoke();

        Assert.That(_turnManager.News.Latest.QuoteId, Is.Not.Empty);
        Assert.That(_quote.gameObject.activeSelf, Is.True);
        Assert.That(_quote.text, Does.Contain("Fala"));
        Assert.That(_columnTitle.text, Is.EqualTo(UIStrings.NewsColumnTitle(NewsColumnKind.Classified)));
        Assert.That(_column.text, Is.EqualTo("Vendo canoa"));
        Assert.That(_learnMore.gameObject.activeSelf, Is.False, "edicao sem carta ou evento nao tem ficha");
    }

    [UnityTest]
    public IEnumerator EditionFromAnOldSave_HidesTheNewAreas()
    {
        yield return Build(new[] { TestDataFactory.CreateCard() }, new RandomEventData[0]);
        var latest = _turnManager.News.Latest;
        latest.QuoteId = string.Empty;
        latest.ColumnId = string.Empty;

        _openButton.onClick.Invoke();

        Assert.That(_panel.activeSelf, Is.True);
        Assert.That(_quote.gameObject.activeSelf, Is.False);
        Assert.That(_columnTitle.gameObject.activeSelf, Is.False);
        Assert.That(_column.gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator CardStory_LearnMoreOpensTheEntryOfThatCard()
    {
        var entry = ScriptableObject.CreateInstance<KnowledgeEntryData>();
        _created.Add(entry);
        var card = TestDataFactory.CreateCard("Horta");
        card.name = "Horta";
        TestDataFactory.SetField(card, "_knowledgeEntry", entry);
        var story = Story("NoticiaHorta", NewsTrigger.CardPlayed);
        TestDataFactory.SetField(story, "_targetId", "Horta");
        yield return Build(new[] { card }, new RandomEventData[0], new[] { story });

        Assert.That(_turnManager.PlayCard(card), Is.True);
        yield return null;

        Assert.That(_turnManager.News.Latest.Items[0].StoryId, Is.EqualTo("NoticiaHorta"));
        _openButton.onClick.Invoke();
        Assert.That(_headline.text, Is.EqualTo("Manchete NoticiaHorta"));
        Assert.That(_learnMore.gameObject.activeSelf, Is.True);

        _learnMore.onClick.Invoke();
        Assert.That(_panel.activeSelf, Is.False);
        Assert.That(_knowledgePanel.activeSelf, Is.True);
        Assert.That(_knowledgePopup.Current, Is.SameAs(entry));
    }
}
