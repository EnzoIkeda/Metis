using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class EventPopupViewTests
{
    private GameObject _host;
    private GameObject _ui;
    private string _saveDirectory;
    private TurnManager _turnManager;
    private GameObject _panel;
    private Button _close;
    private Button _template;
    private TMP_Text _description;

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-popup-" + System.Guid.NewGuid().ToString("N"));
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

    private static Button MakeButton(string name, Transform parent)
    {
        var go = Ui(name, parent);
        go.AddComponent<Image>();
        go.AddComponent<LayoutElement>();
        Ui("Text", go.transform).AddComponent<TextMeshProUGUI>();
        return go.AddComponent<Button>();
    }

    private void Build(params RandomEventData[] events)
    {
        _host = new GameObject("Host");
        _host.SetActive(false);
        var stats = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(stats, "Reset");
        _turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(_turnManager, "_cityStatsManager", stats);
        var cards = Enumerable.Range(0, 6).Select(i => TestDataFactory.CreateCard($"C{i}")).ToArray();
        TestDataFactory.SetField(_turnManager, "_cardPool", cards);
        TestDataFactory.SetField(_turnManager, "_eventPool", events);
        TestDataFactory.SetField(_turnManager, "_advantagePool", new PassiveAdvantageData[0]);
        TestDataFactory.SetField(_turnManager, "_handSize", 3);

        _ui = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _ui.SetActive(false);
        _panel = Ui("Panel", _ui.transform);
        var layout = _panel.AddComponent<VerticalLayoutGroup>();
        layout.padding.bottom = 185;
        _description = Ui("Description", _panel.transform).AddComponent<TextMeshProUGUI>();
        _close = MakeButton("Close", _panel.transform);
        var choices = Ui("Choices", _panel.transform);
        _template = MakeButton("Template", choices.transform);

        var view = Ui("Controller", _ui.transform).AddComponent<EventPopupView>();
        TestDataFactory.SetField(view, "_turnManager", _turnManager);
        TestDataFactory.SetField(view, "_panelRoot", _panel);
        TestDataFactory.SetField(view, "_descriptionText", _description);
        TestDataFactory.SetField(view, "_closeButton", _close);
        TestDataFactory.SetField(view, "_popupLayout", layout);
        TestDataFactory.SetField(view, "_choicesRoot", choices);
        TestDataFactory.SetField(view, "_choiceTemplate", _template);

        _host.SetActive(true);
        _ui.SetActive(true);
    }

    private static RandomEventData Dilemma(string followUp = "")
    {
        var dilemma = TestDataFactory.CreateEvent("Dilema");
        TestDataFactory.SetField(dilemma, "_choices", new[]
        {
            new EventChoiceData
            {
                Label = "Implantar",
                Detail = "Rende criticas depois",
                Rule = new EventChoiceDefinition
                {
                    Effects = new[] { new StatModifier { Parameter = CityParameterType.Renda, Amount = -4f } },
                    FollowUpEventId = followUp,
                    FollowUpDelay = 1,
                },
            },
            new EventChoiceData { Label = "Recusar", Rule = new EventChoiceDefinition { Effects = new StatModifier[0] } },
        });
        return dilemma;
    }

    private Button[] Options()
    {
        return _template.transform.parent.GetComponentsInChildren<Button>(false);
    }

    private IEnumerator OpenPopup()
    {
        yield return null;
        yield return null;
        Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards[0]), Is.True);
        yield return new WaitForSeconds(2f);
        Assert.That(_panel.activeSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator Dilemma_ShowsOneButtonPerOption_WithEffects_AndHidesClose()
    {
        Build(Dilemma());
        yield return OpenPopup();

        var options = Options();
        Assert.That(options.Length, Is.EqualTo(2));
        Assert.That(_close.gameObject.activeSelf, Is.False);
        var text = options[0].GetComponentInChildren<TMP_Text>().text;
        StringAssert.Contains("Implantar", text);
        StringAssert.Contains("Rende criticas depois", text);
        StringAssert.Contains("-4", text);
        StringAssert.Contains("<sprite name=\"Renda\">", text);
        Assert.That(_panel.GetComponent<VerticalLayoutGroup>().padding.bottom, Is.EqualTo(45));
    }

    [UnityTest]
    public IEnumerator Dilemma_LongOption_GrowsTheButtonToFitTheText()
    {
        var dilemma = TestDataFactory.CreateEvent("Dilema");
        TestDataFactory.SetField(dilemma, "_choices", new[]
        {
            new EventChoiceData { Label = "Curta", Rule = new EventChoiceDefinition { Effects = new StatModifier[0] } },
            new EventChoiceData { Label = string.Concat(Enumerable.Repeat("texto bem comprido ", 40)), Rule = new EventChoiceDefinition { Effects = new StatModifier[0] } },
        });
        Build(dilemma);
        yield return OpenPopup();

        var options = Options();
        var shortHeight = options[0].GetComponent<LayoutElement>().preferredHeight;
        Assert.That(shortHeight, Is.GreaterThanOrEqualTo(110f));
        Assert.That(options[1].GetComponent<LayoutElement>().preferredHeight, Is.GreaterThan(shortHeight * 2f));
    }

    [UnityTest]
    public IEnumerator Dilemma_ClickingAnOption_ClosesThePopupAndApplies()
    {
        Build(Dilemma());
        yield return OpenPopup();
        var stats = _host.GetComponent<CityStatsManager>().Stats;
        var before = stats.GetValue(CityParameterType.Renda);

        Options()[0].onClick.Invoke();

        Assert.That(_panel.activeSelf, Is.False);
        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(before - 4f));
    }

    [UnityTest]
    public IEnumerator CommonEvent_KeepsTheCloseButton_AndNoOptions()
    {
        Build(TestDataFactory.CreateEvent("Comum"));
        yield return OpenPopup();

        Assert.That(_close.gameObject.activeSelf, Is.True);
        Assert.That(_template.transform.parent.gameObject.activeSelf, Is.False);
        Assert.That(_panel.GetComponent<VerticalLayoutGroup>().padding.bottom, Is.EqualTo(185));
    }

    [UnityTest]
    public IEnumerator FollowUp_RemindsWhichDecisionCausedIt()
    {
        var followUp = TestDataFactory.CreateEvent("Consequencia");
        TestDataFactory.SetField(followUp, "_isFollowUpOnly", true);
        TestDataFactory.SetField(followUp, "_description", "O que aconteceu");
        Build(Dilemma("Consequencia"), followUp);
        yield return OpenPopup();
        Options()[0].onClick.Invoke();
        yield return new WaitForSeconds(3f);

        Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards[0]), Is.True);
        yield return new WaitForSeconds(2f);

        Assert.That(_panel.activeSelf, Is.True);
        StringAssert.Contains("O que aconteceu", _description.text);
        StringAssert.Contains("Implantar", _description.text);
    }
}
