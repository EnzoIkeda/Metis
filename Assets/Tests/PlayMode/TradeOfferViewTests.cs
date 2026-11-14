using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TradeOfferViewTests
{
    private GameObject _root;
    private GameObject _panel;
    private TradeOfferView _view;
    private TMP_Text _title;
    private TMP_Text _terms;
    private TMP_Text _tutorial;
    private Button _accept;
    private Button _decline;
    private TradeOfferData _offer;
    private CardData _extraCard;
    private Language _previousLanguage;

    private static TMP_Text Text(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<TextMeshProUGUI>();
    }

    private static Button MakeButton(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Text(go.transform, name + "Text");
        return go.GetComponent<Button>();
    }

    [SetUp]
    public void SetUp()
    {
        _previousLanguage = LocalizationManager.Current;
        LocalizationManager.Current = Language.Portuguese;

        _root = new GameObject("Root", typeof(Canvas));
        _panel = new GameObject("Panel", typeof(RectTransform));
        _panel.transform.SetParent(_root.transform, false);
        _title = Text(_panel.transform, "Title");
        _terms = Text(_panel.transform, "Terms");
        _tutorial = Text(_panel.transform, "Tutorial");
        _accept = MakeButton(_panel.transform, "Accept");
        _decline = MakeButton(_panel.transform, "Decline");

        _extraCard = TestDataFactory.CreateCard("CardLixo");
        TestDataFactory.SetField(_extraCard, "_cardName", "Termos de Uso");

        var host = new GameObject("Controller");
        host.SetActive(false);
        host.transform.SetParent(_root.transform, false);
        _view = host.AddComponent<TradeOfferView>();
        TestDataFactory.SetField(_view, "_panelRoot", _panel);
        TestDataFactory.SetField(_view, "_titleText", _title);
        TestDataFactory.SetField(_view, "_termsText", _terms);
        TestDataFactory.SetField(_view, "_tutorialText", _tutorial);
        TestDataFactory.SetField(_view, "_acceptButton", _accept);
        TestDataFactory.SetField(_view, "_declineButton", _decline);
        TestDataFactory.SetField(_view, "_extraCards", new[] { _extraCard });
        host.SetActive(true);

        _offer = ScriptableObject.CreateInstance<TradeOfferData>();
        _offer.name = "Troca_Teste";
        TestDataFactory.SetField(_offer, "_offerName", "Dados por infraestrutura");
        TestDataFactory.SetField(_offer, "_benefit", new TradeTerm
        {
            Kind = TradeTermKind.PhaseStartEffect,
            Effects = new[] { new StatModifier { Parameter = CityParameterType.Mobilidade, Amount = 6f } },
        });
        TestDataFactory.SetField(_offer, "_drawback", new TradeTerm { Kind = TradeTermKind.ExtraCard, CardId = "CardLixo" });
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_offer);
        Object.DestroyImmediate(_extraCard);
        LocalizationManager.Current = _previousLanguage;
    }

    [UnityTest]
    public IEnumerator Start_HidesThePanelUntilAnOfferIsShown()
    {
        yield return null;
        Assert.That(_panel.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator Show_FillsBothSidesWithTheExtraCardName_AndTheTutorialLineOnlyWhenAsked()
    {
        yield return null;
        _view.Show(_offer, showTutorial: true, (offer, accepted) => { });

        Assert.That(_panel.activeSelf, Is.True);
        Assert.That(_title.text, Is.EqualTo("Dados por infraestrutura"));
        Assert.That(_terms.text, Does.Contain("no início de cada fase"));
        Assert.That(_terms.text, Does.Contain("Termos de Uso"));
        Assert.That(_tutorial.gameObject.activeSelf, Is.True);

        _view.Show(_offer, showTutorial: false, (offer, accepted) => { });
        Assert.That(_tutorial.gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator Buttons_AnswerOnceAndCloseThePanel()
    {
        yield return null;
        var answers = new System.Collections.Generic.List<bool>();
        _view.Show(_offer, false, (offer, accepted) => answers.Add(accepted));

        _accept.onClick.Invoke();
        _decline.onClick.Invoke();

        Assert.That(answers, Is.EqualTo(new[] { true }), "depois de responder, o outro botao nao responde de novo");
        Assert.That(_panel.activeSelf, Is.False);

        _view.Show(_offer, false, (offer, accepted) => answers.Add(accepted));
        _decline.onClick.Invoke();
        Assert.That(answers, Is.EqualTo(new[] { true, false }));
    }
}
