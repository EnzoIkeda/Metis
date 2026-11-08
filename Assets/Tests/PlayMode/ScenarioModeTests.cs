using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Cenario de verdade ate o fim: estrelas gravadas, save apagado, objetivos no relatorio e nenhuma recompensa.
public class ScenarioModeTests
{
    private const string ProgressKey = "Metis.Scenarios.PlayTest";

    private string _directory;
    private GameObject _host;
    private GameObject _ui;
    private TurnManager _turnManager;
    private CityStatsManager _statsManager;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "metis-scenario-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_directory, "save.json"));
        TestDataFactory.SetStaticField(typeof(ScenarioProgress), "_prefsKeyOverride", ProgressKey);
        TestDataFactory.SetStaticField(typeof(ScenarioProgress), "_state", null);
        PlayerPrefs.DeleteKey(ProgressKey);
        SessionMode.EnterRoguelike();
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        if (_ui != null)
            Object.DestroyImmediate(_ui);
        if (_host != null)
            Object.DestroyImmediate(_host);
        SessionMode.EnterRoguelike();
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        PlayerPrefs.DeleteKey(ProgressKey);
        TestDataFactory.SetStaticField(typeof(ScenarioProgress), "_prefsKeyOverride", null);
        TestDataFactory.SetStaticField(typeof(ScenarioProgress), "_state", null);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private static ScenarioLibraryData Library()
    {
        var scenario = ScriptableObject.CreateInstance<ScenarioData>();
        scenario.name = "Cenario";
        TestDataFactory.SetField(scenario, "_objectives", new[]
        {
            new ScenarioObjective { Parameter = CityParameterType.Renda, Comparison = ObjectiveComparison.AtLeast, Threshold = 0f, ByTurn = 1 },
            new ScenarioObjective { Parameter = CityParameterType.Pesquisa, Comparison = ObjectiveComparison.AtLeast, Threshold = 100f, ByTurn = 1 },
        });
        var library = ScriptableObject.CreateInstance<ScenarioLibraryData>();
        TestDataFactory.SetField(library, "_scenarios", new[] { scenario });
        return library;
    }

    private static GameObject Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void BuildCity()
    {
        var card = TestDataFactory.CreateCard("Neutra");
        _host = new GameObject("Host");
        _host.SetActive(false);
        _statsManager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(_statsManager, "Reset");
        _turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(_turnManager, "_cityStatsManager", _statsManager);
        TestDataFactory.SetField(_turnManager, "_cardPool", new[] { card });
        TestDataFactory.SetField(_turnManager, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(_turnManager, "_advantagePool", new PassiveAdvantageData[0]);
        TestDataFactory.SetField(_turnManager, "_scenarios", Library());
        TestDataFactory.SetField(_turnManager, "_victoryTurnCount", 2);
    }

    private ScenarioObjectivesView BuildObjectives(out GameObject panel, out GameObject openButton)
    {
        _ui = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _ui.SetActive(false);
        panel = Ui("Panel", _ui.transform);
        var body = Ui("Body", panel.transform).AddComponent<TextMeshProUGUI>();
        var buttonObject = Ui("Open", _ui.transform);
        openButton = buttonObject;
        var view = Ui("Controller", _ui.transform).AddComponent<ScenarioObjectivesView>();
        TestDataFactory.SetField(view, "_turnManager", _turnManager);
        TestDataFactory.SetField(view, "_cityStatsManager", _statsManager);
        TestDataFactory.SetField(view, "_openButton", buttonObject.AddComponent<Button>());
        TestDataFactory.SetField(view, "_panelRoot", panel);
        TestDataFactory.SetField(view, "_bodyText", (TMP_Text)body);
        return view;
    }

    private IEnumerator PlayToTheEnd()
    {
        for (var turn = 0; turn < 5 && _turnManager.Machine.Outcome == GameOutcome.None; turn++)
        {
            Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards.First()), Is.True);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator Victory_RecordsStarsClearsTheSaveAndShowsObjectivesInTheReport()
    {
        SessionMode.EnterScenario("Cenario", "Tema");
        MetaProgressionManager.BeginScenario("Cenario", string.Empty, CardArchetype.Sustentabilidade);
        BuildCity();

        _ui = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _ui.SetActive(false);
        var reportPanel = Ui("ReportPanel", _ui.transform);
        var body = Ui("Body", reportPanel.transform).AddComponent<TextMeshProUGUI>();
        var report = Ui("Report", _ui.transform).AddComponent<PhaseReportView>();
        TestDataFactory.SetField(report, "_turnManager", _turnManager);
        TestDataFactory.SetField(report, "_cityStatsManager", _statsManager);
        TestDataFactory.SetField(report, "_panelRoot", reportPanel);
        TestDataFactory.SetField(report, "_bodyText", (TMP_Text)body);
        var flow = Ui("Flow", _ui.transform).AddComponent<PhaseEndFlow>();
        TestDataFactory.SetField(flow, "_turnManager", _turnManager);
        TestDataFactory.SetField(flow, "_report", report);

        var rewardPanel = Ui("RewardPanel", _ui.transform);
        var reward = Ui("Reward", _ui.transform).AddComponent<PhaseRewardPopupView>();
        TestDataFactory.SetField(reward, "_turnManager", _turnManager);
        TestDataFactory.SetField(reward, "_endFlow", flow);
        TestDataFactory.SetField(reward, "_panelRoot", rewardPanel);
        TestDataFactory.SetField(reward, "_optionButtons", new Button[0]);
        TestDataFactory.SetField(reward, "_optionTexts", new TMP_Text[0]);

        _host.SetActive(true);
        _ui.SetActive(true);
        yield return null;
        yield return PlayToTheEnd();

        Assert.That(_turnManager.Machine.Outcome, Is.EqualTo(GameOutcome.Victory));
        Assert.That(ScenarioProgress.BestStars("Cenario"), Is.EqualTo(2), "sobreviver mais o objetivo cumprido");
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.False);
        Assert.That(body.text, Does.Contain(UIStrings.ReportObjectivesHeader));
        Assert.That(body.text, Does.Contain(UIStrings.ReportScenarioStars(GameOutcome.Victory, 2, 3)));

        report.SendMessage("Continue");
        yield return null;
        Assert.That(rewardPanel.activeSelf, Is.False, "cenario nao tem recompensa");
    }

    [UnityTest]
    public IEnumerator ObjectivesPanel_ShowsTheCurrentState()
    {
        SessionMode.EnterScenario("Cenario", "Tema");
        MetaProgressionManager.BeginScenario("Cenario", string.Empty, CardArchetype.Sustentabilidade);
        BuildCity();
        TestDataFactory.SetField(_turnManager, "_victoryTurnCount", 20);
        var view = BuildObjectives(out var panel, out var openButton);
        _host.SetActive(true);
        _ui.SetActive(true);
        yield return null;

        Assert.That(openButton.activeSelf, Is.True);
        Assert.That(panel.activeSelf, Is.False);

        view.Show();
        Assert.That(panel.activeSelf, Is.True);
        Assert.That(view.BodyText, Does.Contain(UIStrings.ScenarioStateLabel(ObjectiveState.Pending)));

        yield return PlayOneTurn();
        Assert.That(view.BodyText, Does.Contain(UIStrings.ScenarioStateLabel(ObjectiveState.Achieved)));
        Assert.That(view.BodyText, Does.Contain(UIStrings.ScenarioStateLabel(ObjectiveState.Failed)));
    }

    private IEnumerator PlayOneTurn()
    {
        Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards.First()), Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator NormalRun_HidesTheObjectivesButton()
    {
        BuildCity();
        BuildObjectives(out var panel, out var openButton);
        _host.SetActive(true);
        _ui.SetActive(true);
        yield return null;

        Assert.That(_turnManager.Scenario, Is.Null);
        Assert.That(openButton.activeSelf, Is.False);
        Assert.That(panel.activeSelf, Is.False);
    }
}
