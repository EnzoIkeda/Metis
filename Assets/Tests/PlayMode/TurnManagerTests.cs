using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TurnManagerTests
{
    private GameObject _host;
    private string _saveDirectory;

    [SetUp]
    public void SetUp()
    {
        // O TurnManager grava o save a cada turno; numa pasta temporaria, pra nunca tocar na rodada de verdade.
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-playmode-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_saveDirectory, "run_save.json"));
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        // Destruir primeiro: o efeito pendente dispara no OnDisable, avanca o turno e grava o save, que ainda tem de cair na pasta temporaria.
        if (_host != null)
            Object.DestroyImmediate(_host);
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_saveDirectory))
            Directory.Delete(_saveDirectory, true);
    }

    // Simula fechar o app: some a cena e o cache em memoria, so o arquivo sobrevive.
    private void CloseAndForgetInMemoryState()
    {
        Object.DestroyImmediate(_host);
        _host = null;
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
    }

    private static CardData[] DistinctCards(int count)
    {
        var cards = new CardData[count];
        for (int i = 0; i < count; i++)
            cards[i] = TestDataFactory.CreateCard($"Carta{i}");
        return cards;
    }

    private static string[] Ids(System.Collections.Generic.IEnumerable<CardData> cards)
    {
        return cards.Select(card => card.name).ToArray();
    }

    // Sem prefab de particula atribuido, o efeito so pula a instancia e a espera calibrada continua valendo.
    private TurnManager CreateTurnManager(CardData[] cardPool, int handSize = 5, RandomEventData[] eventPool = null)
    {
        _host = new GameObject("TurnManagerHost");
        _host.SetActive(false);

        var statsManager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");

        var effects = _host.AddComponent<CityEffectsController>();

        var turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(turnManager, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(turnManager, "_cardPool", cardPool);
        TestDataFactory.SetField(turnManager, "_eventPool", eventPool ?? new RandomEventData[0]);
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

    // Jogar de novo durante o efeito de impacto e bloqueado, e o turno avanca uma vez so quando o efeito termina.
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

    // Desativar o controlador de efeitos no meio do impacto parava a corrotina e o turno travava pra sempre;
    // agora o callback pendente dispara no OnDisable e o turno avanca uma vez so.
    [UnityTest]
    public IEnumerator PlayCard_EffectsDisabledMidAnimation_TurnStillAdvancesOnce()
    {
        var card = TestDataFactory.CreateCard();
        var turnManager = CreateTurnManager(new[] { card });
        yield return null;

        Assert.That(turnManager.PlayCard(card), Is.True);
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(1));

        _host.GetComponent<CityEffectsController>().enabled = false;

        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(2));
        Assert.That(turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));

        yield return new WaitForSeconds(2f);

        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(2), "a corrotina interrompida nao pode avancar o turno de novo");
    }

    // Com o controlador ja desativado nao da pra rodar corrotina: a animacao e pulada e o turno segue na hora.
    [UnityTest]
    public IEnumerator PlayCard_EffectsAlreadyDisabled_TurnAdvancesImmediately()
    {
        var card = TestDataFactory.CreateCard();
        var turnManager = CreateTurnManager(new[] { card });
        yield return null;
        _host.GetComponent<CityEffectsController>().enabled = false;

        Assert.That(turnManager.PlayCard(card), Is.True);

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
        // Baralho finito com 1 busca + 3 copias do alvo e mao de 2: a busca vem em ate 2 compras e sempre sobra alvo pra buscar.
        var search = TestDataFactory.CreateCard("Buscar", ability: CardAbility.SearchDeck);
        var target = TestDataFactory.CreateCard("Alvo", copies: 3);
        var turnManager = CreateTurnManager(new[] { search, target }, handSize: 2);
        yield return null;
        for (int redraw = 0; redraw < 3 && turnManager.Hand.Cards.Contains(search) == false; redraw++)
        {
            turnManager.Hand.DiscardAll();
            turnManager.Hand.Draw(2, Object.FindFirstObjectByType<CityStatsManager>().Stats);
        }
        Assert.That(turnManager.Hand.Cards, Has.Member(search));
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

    // Fechar o app e continuar volta ao comeco da fase de acao do mesmo turno: mesma mao, mesmas pilhas, mesmos parametros.
    [UnityTest]
    public IEnumerator Resume_AfterClosing_RestoresTurnHandPilesAndParameters()
    {
        var pool = DistinctCards(12);
        var turnManager = CreateTurnManager(pool, handSize: 4);
        yield return null;
        Assert.That(turnManager.PlayCard(turnManager.Hand.Cards[0]), Is.True);
        yield return new WaitForSeconds(2f);
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(2));

        var stats = _host.GetComponent<CityStatsManager>().Stats;
        var expectedHand = Ids(turnManager.Hand.Cards);
        var expectedDraw = Ids(turnManager.Hand.DrawPile);
        var expectedDiscard = Ids(turnManager.Hand.DiscardPile);
        var expectedRenda = stats.GetValue(CityParameterType.Renda);
        var expectedBemEstar = stats.GetValue(CityParameterType.BemEstar);

        CloseAndForgetInMemoryState();
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.True);
        var resumed = CreateTurnManager(pool, handSize: 4);
        yield return null;

        var resumedStats = _host.GetComponent<CityStatsManager>().Stats;
        Assert.That(resumed.ResumedFromSave, Is.True);
        Assert.That(resumed.Machine.TurnIndex, Is.EqualTo(2));
        Assert.That(resumed.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
        Assert.That(Ids(resumed.Hand.Cards), Is.EqualTo(expectedHand));
        Assert.That(Ids(resumed.Hand.DrawPile), Is.EqualTo(expectedDraw));
        Assert.That(Ids(resumed.Hand.DiscardPile), Is.EqualTo(expectedDiscard));
        Assert.That(resumedStats.GetValue(CityParameterType.Renda), Is.EqualTo(expectedRenda));
        Assert.That(resumedStats.GetValue(CityParameterType.BemEstar), Is.EqualTo(expectedBemEstar));
    }

    // Com o popup de evento aberto o evento ja foi aplicado: continuar mostra o mesmo evento em vez de sortear outro ou desfazer a jogada.
    [UnityTest]
    public IEnumerator Resume_WithEventPopupOpen_AnnouncesTheSameEventWithoutReapplyingIt()
    {
        var pool = DistinctCards(8);
        var eventData = TestDataFactory.CreateEvent("EventoRenda", new[] { new StatModifier { Parameter = CityParameterType.Renda, Amount = -5f } });
        var turnManager = CreateTurnManager(pool, handSize: 3, eventPool: new[] { eventData });
        yield return null;
        Assert.That(turnManager.PlayCard(turnManager.Hand.Cards[0]), Is.True);
        yield return new WaitForSeconds(2f);
        Assert.That(turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Event));
        var expectedRenda = _host.GetComponent<CityStatsManager>().Stats.GetValue(CityParameterType.Renda);
        var expectedHand = Ids(turnManager.Hand.Cards);

        CloseAndForgetInMemoryState();
        var resumed = CreateTurnManager(pool, handSize: 3, eventPool: new[] { eventData });
        RandomEventData announced = null;
        resumed.OnRandomEventTriggered += triggered => announced = triggered;
        for (int frame = 0; frame < 10 && announced == null; frame++)
            yield return null;

        Assert.That(announced, Is.SameAs(eventData));
        Assert.That(resumed.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Event));
        Assert.That(resumed.Machine.TurnIndex, Is.EqualTo(1));
        Assert.That(Ids(resumed.Hand.Cards), Is.EqualTo(expectedHand));
        Assert.That(_host.GetComponent<CityStatsManager>().Stats.GetValue(CityParameterType.Renda), Is.EqualTo(expectedRenda), "o evento nao pode ser aplicado de novo");

        resumed.AcknowledgeEvent();
        yield return new WaitForSeconds(3f);
        Assert.That(resumed.Machine.TurnIndex, Is.EqualTo(2));
        Assert.That(resumed.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
    }

    // Save com carta que nao existe mais no jogo: a fase recomeca do zero, mas a rodada continua.
    [UnityTest]
    public IEnumerator Resume_WithUnknownCard_StartsThePhaseFresh()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Industria);
        var phase = new PhaseSaveData { TurnIndex = 9 };
        phase.Hand.Add("CartaRemovida");
        MetaProgressionManager.SavePhase(phase);

        var turnManager = CreateTurnManager(DistinctCards(6));
        yield return null;

        Assert.That(turnManager.ResumedFromSave, Is.False);
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(1));
        Assert.That(turnManager.Hand.Cards, Is.Not.Empty);
        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Industria));
    }

    // Derrota apaga o save na hora, senao continuar repetiria o turno perdido.
    [UnityTest]
    public IEnumerator GameOver_DeletesTheSavedRun()
    {
        var turnManager = CreateTurnManager(DistinctCards(4));
        yield return null;
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.True);

        turnManager.Machine.DebugForceGameOver();

        Assert.That(MetaProgressionManager.HasRunInProgress, Is.False);
        Assert.That(MetaProgressionManager.SavedPhase, Is.Null);
    }
}
