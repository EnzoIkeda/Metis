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
    private TurnManager CreateTurnManager(CardData[] cardPool, int handSize = 5)
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
        TestDataFactory.SetField(turnManager, "_handSize", handSize);

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

    // Carta de revelacao e acao livre: paga o custo, revela a mao e o turno continua esperando a jogada.
    [UnityTest]
    public IEnumerator PlayCard_RevealCard_IsAFreeActionThatRevealsTheHand()
    {
        var reveal = TestDataFactory.CreateCard("Revelar", cost: 3f, ability: CardAbility.RevealHand);
        var turnManager = CreateTurnManager(new[] { reveal });
        yield return null;
        var handCount = turnManager.Hand.Cards.Count;

        var played = turnManager.PlayCard(reveal);

        Assert.That(played, Is.True);
        Assert.That(turnManager.Hand.IsRevealed, Is.True);
        Assert.That(turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(1));
        Assert.That(turnManager.Hand.Cards.Count, Is.EqualTo(handCount - 1));
    }

    // Busca e acao livre: pede a escolha, bloqueia outras jogadas ate ela chegar, e a escolhida entra na mao.
    [UnityTest]
    public IEnumerator PlayCard_SearchCard_WaitsForChoiceThenAddsTheCard()
    {
        var search = TestDataFactory.CreateCard("Buscar", ability: CardAbility.SearchDeck);
        var target = TestDataFactory.CreateCard("Alvo");
        // Mao grande pra compra sorteada trazer as duas cartas com probabilidade de falha ~1 em um milhao.
        var turnManager = CreateTurnManager(new[] { search, target }, handSize: 20);
        yield return null;
        Assume.That(turnManager.Hand.Cards, Has.Member(search));
        Assume.That(turnManager.Hand.Cards, Has.Member(target));
        System.Collections.Generic.IReadOnlyList<CardData> offered = null;
        turnManager.OnSearchRequested += candidates => offered = candidates;
        var otherCard = target;

        Assert.That(turnManager.PlayCard(search), Is.True);
        Assert.That(offered, Is.EquivalentTo(new[] { target }), "a propria busca nao pode ser trazida");
        Assert.That(turnManager.PlayCard(otherCard), Is.False, "nada mais pode ser jogado enquanto a busca espera a escolha");

        var countBefore = turnManager.Hand.Cards.Count;
        Assert.That(turnManager.CompleteSearch(target), Is.True);

        Assert.That(turnManager.Hand.Cards.Count, Is.EqualTo(countBefore + 1));
        Assert.That(turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
        Assert.That(turnManager.PlayCard(target), Is.True, "depois da busca a jogada normal do turno continua disponivel");
    }

    // Passar a vez so existe quando nada comum e jogavel: aqui a unica carta custa mais que a Renda inicial.
    [UnityTest]
    public IEnumerator PassTurn_OnlyAllowedWhenNoPlainCardIsPlayable()
    {
        var expensive = TestDataFactory.CreateCard("Cara", cost: 1000f);
        var turnManager = CreateTurnManager(new[] { expensive });
        yield return null;

        Assert.That(turnManager.CanPassTurn, Is.True);
        Assert.That(turnManager.PassTurn(), Is.True);
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(2), "sem eventos, passar a vez fecha o turno direto");
        Assert.That(turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
    }

    [UnityTest]
    public IEnumerator PassTurn_RefusedWhileAPlainCardIsPlayable()
    {
        var card = TestDataFactory.CreateCard();
        var turnManager = CreateTurnManager(new[] { card });
        yield return null;

        Assert.That(turnManager.CanPassTurn, Is.False);
        Assert.That(turnManager.PassTurn(), Is.False);
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(1));
    }
}
