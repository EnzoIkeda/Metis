using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

// Fim de fase de verdade: derrota por Colapso, relatorio com a cadeia e o popup de desfecho so depois dele.
public class PhaseReportViewTests
{
    private GameObject _host;
    private GameObject _ui;
    private string _saveDirectory;

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-report-" + System.Guid.NewGuid().ToString("N"));
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

    [UnityTest]
    public IEnumerator GameOverByCollapse_ReportShowsTheChainBeforeTheOutcomePopup()
    {
        // Quatro parametros em Colapso logo no primeiro turno tiram 48 do Bem-estar, e Seguranca zerada ainda baixa a media.
        var crash = TestDataFactory.CreateCard("Desmonte", statEffects: new[]
        {
            new StatModifier { Parameter = CityParameterType.Energia, Amount = -100f },
            new StatModifier { Parameter = CityParameterType.Renda, Amount = -100f },
            new StatModifier { Parameter = CityParameterType.Populacao, Amount = -100f },
            new StatModifier { Parameter = CityParameterType.Seguranca, Amount = -100f },
        });

        _host = new GameObject("Host");
        _host.SetActive(false);
        var statsManager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");
        var turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(turnManager, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(turnManager, "_cardPool", new[] { crash });
        TestDataFactory.SetField(turnManager, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(turnManager, "_advantagePool", new PassiveAdvantageData[0]);

        _ui = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _ui.SetActive(false);
        var reportPanel = Ui("ReportPanel", _ui.transform);
        var body = Ui("Body", reportPanel.transform).AddComponent<TextMeshProUGUI>();
        var report = Ui("ReportController", _ui.transform).AddComponent<PhaseReportView>();
        TestDataFactory.SetField(report, "_turnManager", turnManager);
        TestDataFactory.SetField(report, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(report, "_panelRoot", reportPanel);
        TestDataFactory.SetField(report, "_bodyText", body);

        var flow = Ui("PhaseEndFlow", _ui.transform).AddComponent<PhaseEndFlow>();
        TestDataFactory.SetField(flow, "_turnManager", turnManager);
        TestDataFactory.SetField(flow, "_report", report);

        var outcomePanel = Ui("GameOverPopup", _ui.transform);
        var outcome = Ui("GameOverController", _ui.transform).AddComponent<GameOutcomePopupView>();
        TestDataFactory.SetField(outcome, "_turnManager", turnManager);
        TestDataFactory.SetField(outcome, "_endFlow", flow);
        TestDataFactory.SetField(outcome, "_outcomeToShowFor", GameOutcome.GameOver);
        TestDataFactory.SetField(outcome, "_panelRoot", outcomePanel);

        _host.SetActive(true);
        _ui.SetActive(true);
        yield return null;
        Assert.That(reportPanel.activeSelf, Is.False, "o relatorio comeca escondido");

        // Sem controlador de efeitos, cada jogada fecha o turno na hora.
        for (var turn = 0; turn < 20 && turnManager.Machine.Outcome == GameOutcome.None; turn++)
        {
            Assert.That(turnManager.PlayCard(turnManager.Hand.Cards.First()), Is.True);
            yield return null;
        }

        Assert.That(turnManager.Machine.Outcome, Is.EqualTo(GameOutcome.GameOver));
        Assert.That(reportPanel.activeSelf, Is.True, "o relatorio abre no fim de jogo");
        Assert.That(outcomePanel.activeSelf, Is.False, "o desfecho espera o relatorio");

        var chain = PhaseReport.CollapseChain(turnManager.History, turnManager.CreateEvaluator().Rules);
        Assert.That(chain.Select(span => span.Parameter), Has.Member(CityParameterType.Energia));
        var energia = chain.First(span => span.Parameter == CityParameterType.Energia);
        Assert.That(energia.FirstTurn, Is.EqualTo(1));
        Assert.That(energia.TurnsPenalized, Is.EqualTo(turnManager.Machine.TurnIndex));

        Assert.That(body.text, Does.Contain(UIStrings.ReportCollapseHeader(GameOutcome.GameOver)));
        Assert.That(body.text, Does.Contain(CityParameterType.Energia.GetFullName()));
        Assert.That(body.text, Does.Contain(UIStrings.ReportAnchorCritical(turnManager.Machine.TurnIndex)));
        Assert.That(turnManager.History.Turns.Count, Is.EqualTo(turnManager.Machine.TurnIndex));

        TestDataFactory.InvokePrivateMethod(report, "Continue");

        Assert.That(reportPanel.activeSelf, Is.False);
        Assert.That(outcomePanel.activeSelf, Is.True, "o desfecho abre ao sair do relatorio");
    }
}
