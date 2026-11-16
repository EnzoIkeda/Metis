using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Fase do chefe de verdade: a jogada dele segura o turno, volta igual ao continuar e a vitoria encerra a rodada.
public class BossPhaseTests
{
    private string _directory;
    private GameObject _host;
    private GameObject _ui;
    private TurnManager _turnManager;
    private CityStatsManager _statsManager;
    private BossLibraryData _library;
    private GameObject _popupPanel;
    private GameObject _hudRoot;
    private BossHudView _hud;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "metis-boss-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_directory, "save.json"));
        SessionMode.EnterRoguelike();
        MetaProgressionManager.ResetRun();
        _library = Library();
    }

    [TearDown]
    public void TearDown()
    {
        DestroyCity();
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private void DestroyCity()
    {
        if (_ui != null)
            Object.DestroyImmediate(_ui);
        if (_host != null)
            Object.DestroyImmediate(_host);
    }

    private static void SetPlain(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    // Um golpe por turno: o ganho de Impeto paga a carta toda vez.
    private static BossLibraryData Library(BossMechanic mechanic = BossMechanic.GrowingDamage, float amount = 0.5f, string cardId = "")
    {
        var cards = Enumerable.Range(0, 6).Select(i =>
        {
            var card = new BossCardEntry();
            SetPlain(card, "_id", "Golpe" + i);
            SetPlain(card, "_cardName", "Golpe " + i);
            SetPlain(card, "_description", "Descricao");
            SetPlain(card, "_momentumCost", 3);
            SetPlain(card, "_effects", new[] { new StatModifier { Parameter = CityParameterType.Saude, Amount = -2f } });
            return card;
        }).ToArray();
        var boss = ScriptableObject.CreateInstance<BossData>();
        boss.name = "Chefe";
        TestDataFactory.SetField(boss, "_bossName", "Chefe do teste");
        TestDataFactory.SetField(boss, "_intro", "Abertura");
        TestDataFactory.SetField(boss, "_victory", "Vitoria da rodada");
        TestDataFactory.SetField(boss, "_targetArchetype", CardArchetype.Industria);
        TestDataFactory.SetField(boss, "_momentumPerTurn", 3);
        TestDataFactory.SetField(boss, "_mechanic", mechanic);
        TestDataFactory.SetField(boss, "_mechanicAmount", amount);
        TestDataFactory.SetField(boss, "_mechanicCardId", cardId);
        TestDataFactory.SetField(boss, "_cards", cards);
        var library = ScriptableObject.CreateInstance<BossLibraryData>();
        TestDataFactory.SetField(library, "_bosses", new[] { boss });
        return library;
    }

    private static GameObject Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void EnterBossPhase()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Industria);
        MetaProgressionManager.ChooseMapNode(PhaseMapProvider.BossOffer("Chefe")[0]);
    }

    private BossPlayPopupView BuildCity(int victoryTurns = 20, CardData extra = null)
    {
        var card = TestDataFactory.CreateCard("Neutra", cost: 2f);
        _host = new GameObject("Host");
        _host.SetActive(false);
        _statsManager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(_statsManager, "Reset");
        _turnManager = _host.AddComponent<TurnManager>();
        TestDataFactory.SetField(_turnManager, "_cityStatsManager", _statsManager);
        TestDataFactory.SetField(_turnManager, "_cardPool", extra != null ? new[] { card, extra } : new[] { card });
        TestDataFactory.SetField(_turnManager, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(_turnManager, "_advantagePool", new PassiveAdvantageData[0]);
        TestDataFactory.SetField(_turnManager, "_bosses", _library);
        TestDataFactory.SetField(_turnManager, "_victoryTurnCount", victoryTurns);

        _ui = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _ui.SetActive(false);
        _popupPanel = Ui("BossPanel", _ui.transform);
        var confirm = Ui("Confirm", _popupPanel.transform).AddComponent<Image>().gameObject.AddComponent<Button>();
        var effects = Ui("Effects", _popupPanel.transform).AddComponent<TextMeshProUGUI>();
        var popup = Ui("PopupController", _ui.transform).AddComponent<BossPlayPopupView>();
        TestDataFactory.SetField(popup, "_turnManager", _turnManager);
        TestDataFactory.SetField(popup, "_panelRoot", _popupPanel);
        TestDataFactory.SetField(popup, "_confirmButton", confirm);
        TestDataFactory.SetField(popup, "_effectsText", (TMP_Text)effects);

        _hudRoot = Ui("BossHud", _ui.transform);
        var status = Ui("Status", _hudRoot.transform).AddComponent<TextMeshProUGUI>();
        _hud = Ui("HudController", _ui.transform).AddComponent<BossHudView>();
        TestDataFactory.SetField(_hud, "_turnManager", _turnManager);
        TestDataFactory.SetField(_hud, "_root", _hudRoot);
        TestDataFactory.SetField(_hud, "_statusText", (TMP_Text)status);
        var mechanicText = Ui("Mechanic", _hudRoot.transform).AddComponent<TextMeshProUGUI>();
        TestDataFactory.SetField(_hud, "_mechanicText", (TMP_Text)mechanicText);
        return popup;
    }

    private IEnumerator Start()
    {
        _host.SetActive(true);
        _ui.SetActive(true);
        yield return null;
        yield return null;
    }

    private void Confirm()
    {
        _popupPanel.GetComponentInChildren<Button>().onClick.Invoke();
    }

    [UnityTest]
    public IEnumerator BossCard_HoldsTheTurnUntilConfirmed()
    {
        EnterBossPhase();
        BuildCity();
        yield return Start();

        Assert.That(_turnManager.Boss, Is.Not.Null);
        Assert.That(_hudRoot.activeSelf, Is.True);
        Assert.That(_hud.StatusText, Is.EqualTo(UIStrings.BossPlaysThisTurn));
        var saude = _statsManager.Stats.GetValue(CityParameterType.Saude);

        Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards.First()), Is.True);
        Assert.That(_turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Boss));
        Assert.That(_turnManager.PendingBossCard, Is.Not.Null);
        Assert.That(_popupPanel.activeSelf, Is.True);
        Assert.That(_statsManager.Stats.GetValue(CityParameterType.Saude), Is.EqualTo(saude - 2f).Within(0.01f));

        Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards.FirstOrDefault()), Is.False, "nao joga carta com o chefe pendente");
        Confirm();
        yield return null;

        Assert.That(_popupPanel.activeSelf, Is.False);
        Assert.That(_turnManager.Machine.TurnIndex, Is.EqualTo(2));
        Assert.That(_turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
        Assert.That(_turnManager.History.Turns[0].BossCardId, Is.Not.Empty);
    }

    [UnityTest]
    public IEnumerator ResumingWithTheBossPopupOpen_ShowsItAgainWithoutApplyingTwice()
    {
        EnterBossPhase();
        BuildCity();
        yield return Start();
        Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards.First()), Is.True);
        var played = _turnManager.PendingBossCard.Id;
        var saude = _statsManager.Stats.GetValue(CityParameterType.Saude);
        var cardsPlayed = _turnManager.Boss.CardsPlayed;

        DestroyCity();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        BuildCity();
        yield return Start();
        yield return null;

        Assert.That(_turnManager.ResumedFromSave, Is.True);
        Assert.That(_turnManager.Machine.CurrentPhase, Is.EqualTo(TurnPhase.Boss));
        Assert.That(_turnManager.PendingBossCard?.Id, Is.EqualTo(played));
        Assert.That(_popupPanel.activeSelf, Is.True);
        Assert.That(_statsManager.Stats.GetValue(CityParameterType.Saude), Is.EqualTo(saude).Within(0.01f));
        Assert.That(_turnManager.Boss.CardsPlayed, Is.EqualTo(cardsPlayed));

        Confirm();
        yield return null;
        Assert.That(_turnManager.Machine.TurnIndex, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator BeatingTheBoss_EndsTheRun()
    {
        EnterBossPhase();
        BuildCity(victoryTurns: 2);
        yield return Start();

        for (var turn = 0; turn < 4 && _turnManager.Machine.Outcome == GameOutcome.None; turn++)
        {
            Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards.First()), Is.True);
            if (_turnManager.PendingBossCard != null)
                Confirm();
            yield return null;
        }

        Assert.That(_turnManager.Machine.Outcome, Is.EqualTo(GameOutcome.Victory));
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.False);
    }

    [UnityTest]
    public IEnumerator NormalPhase_HasNoBossAndHidesTheHud()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Industria);
        BuildCity();
        yield return Start();

        Assert.That(_turnManager.Boss, Is.Null);
        Assert.That(_hudRoot.activeSelf, Is.False);
        Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards.First()), Is.True);
        yield return null;
        Assert.That(_turnManager.Machine.TurnIndex, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator ExtraCards_GoIntoTheDeckAndSurviveTheSave()
    {
        _library = Library(BossMechanic.ExtraCards, 2f, "Termos");
        var extra = TestDataFactory.CreateCard("Termos");
        TestDataFactory.SetField(extra, "_isExtraOnly", true);
        EnterBossPhase();
        BuildCity(extra: extra);
        yield return Start();
        Assert.That(_turnManager.Hand.CountInDeck(extra), Is.EqualTo(0));

        Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards.First(card => card != extra)), Is.True);
        Assert.That(_turnManager.Hand.CountInDeck(extra), Is.EqualTo(2));
        Assert.That(_hud.MechanicText, Is.EqualTo(UIStrings.BossMechanicStatus(BossMechanic.ExtraCards, 1f, 2, 0)));

        DestroyCity();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        BuildCity(extra: extra);
        yield return Start();
        yield return null;

        Assert.That(_turnManager.ResumedFromSave, Is.True);
        Assert.That(_turnManager.Hand.CountInDeck(extra), Is.EqualTo(2), "as copias voltam do save sem serem postas de novo");
        Confirm();
        yield return null;
        Assert.That(_turnManager.Hand.CountInDeck(extra), Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator CostIncrease_RaisesTheCostChargedForCards()
    {
        _library = Library(BossMechanic.CostIncrease, 1f);
        EnterBossPhase();
        BuildCity();
        yield return Start();
        var card = _turnManager.Hand.Cards.First();
        Assert.That(_turnManager.Hand.CostOf(card), Is.EqualTo(2f));

        Assert.That(_turnManager.PlayCard(card), Is.True);
        Assert.That(_turnManager.Hand.CostOffset, Is.EqualTo(1));
        Assert.That(_turnManager.Hand.CostOf(card), Is.EqualTo(3f));
        Assert.That(_hud.MechanicText, Is.EqualTo(UIStrings.BossMechanicStatus(BossMechanic.CostIncrease, 1f, 0, 1)));

        Confirm();
        yield return null;
        var renda = _statsManager.Stats.GetValue(CityParameterType.Renda);
        Assert.That(_turnManager.PlayCard(_turnManager.Hand.Cards.First()), Is.True);
        Assert.That(_statsManager.Stats.GetValue(CityParameterType.Renda), Is.LessThan(renda - 2.5f), "a segunda carta ja paga 3");
    }
}
