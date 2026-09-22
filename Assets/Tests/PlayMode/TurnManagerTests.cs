using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TurnManagerTests
{
    private GameObject _host;

    [SetUp]
    public void SetUp()
    {
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        MetaProgressionManager.ResetRun();
        if (_host != null)
            Object.DestroyImmediate(_host);
    }

    // CityEffectsController fica sem nenhum prefab de particula atribuido; PlayOneShot lida com isso
    // direto (so pula o Instantiate), entao a espera calibrada continua rodando de verdade.
    private TurnManager CreateTurnManager(CardData[] cardPool)
    {
        _host = new GameObject("TurnManagerHost");
        _host.SetActive(false);

        var statsManager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");

        var effects = _host.AddComponent<CityEffectsController>();

        var turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(turnManager, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(turnManager, "_cardPool", cardPool);
        TestDataFactory.SetField(turnManager, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(turnManager, "_advantagePool", new PassiveAdvantageData[0]);
        TestDataFactory.SetField(turnManager, "_effects", effects);

        _host.SetActive(true);
        return turnManager;
    }

    [UnityTest]
    public IEnumerator Start_DrawsInitialHand_AndEntersActionPhase()
    {
        var turnManager = CreateTurnManager(new[] { TestDataFactory.CreateCard() });
        yield return null;

        Assert.That(turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
        Assert.That(turnManager.Hand.Cards, Is.Not.Empty);
    }

    // Pendencia documentada no ARCHITECTURE.md: joga uma carta (dispara o efeito de impacto, que atrasa
    // o fim da fase de acao) e tenta jogar de novo antes do efeito terminar; a segunda jogada tem que
    // ser bloqueada, e o turno so pode avancar uma vez quando o efeito atrasado finalmente completa.
    [UnityTest]
    public IEnumerator PlayCard_WhileImpactAnimationPending_SecondCallIsBlockedAndTurnAdvancesOnlyOnce()
    {
        var card = TestDataFactory.CreateCard();
        var turnManager = CreateTurnManager(new[] { card });
        yield return null;

        var playedFirst = turnManager.PlayCard(card);
        Assert.That(playedFirst, Is.True);
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(1));

        var playedSecond = turnManager.PlayCard(card);
        Assert.That(playedSecond, Is.False);

        yield return new WaitForSeconds(2f);

        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(2));
        Assert.That(turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
    }
}
