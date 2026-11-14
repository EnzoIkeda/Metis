using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tela da oferta de troca no mapa: mostra os dois lados e devolve a resposta do jogador.
public class TradeOfferView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _headerText;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _summaryText;
    [SerializeField] private TMP_Text _termsText;
    [SerializeField] private TMP_Text _tutorialText;
    [SerializeField] private Button _acceptButton;
    [SerializeField] private Button _declineButton;

    // Cartas que uma troca pode acrescentar, so para mostrar o nome.
    [SerializeField] private CardData[] _extraCards = Array.Empty<CardData>();

    private TradeOfferData _offer;
    private Action<TradeOfferData, bool> _onDecided;
    private bool _shown;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    private void Awake()
    {
        if (_acceptButton != null)
            _acceptButton.onClick.AddListener(() => Decide(true));
        if (_declineButton != null)
            _declineButton.onClick.AddListener(() => Decide(false));
    }

    private void Start()
    {
        // Mostrada antes do Start, a tela continua aberta.
        if (_shown == false && _panelRoot != null)
            _panelRoot.SetActive(false);
    }

    // showTutorial liga a linha que explica a troca, na primeira oferta ou ao rever o tutorial.
    public void Show(TradeOfferData offer, bool showTutorial, Action<TradeOfferData, bool> onDecided)
    {
        _offer = offer;
        _onDecided = onDecided;
        _shown = true;

        SetText(_headerText, UIStrings.TradeHeader);
        SetText(_titleText, offer.OfferName);
        SetText(_summaryText, offer.Summary);
        SetText(_termsText, DescribeTerms(offer));
        if (_tutorialText != null)
        {
            _tutorialText.gameObject.SetActive(showTutorial);
            _tutorialText.text = UIStrings.TutorialMessage(TutorialTip.Trade);
        }
        SetButtonLabel(_acceptButton, UIStrings.TradeAccept);
        SetButtonLabel(_declineButton, UIStrings.TradeDecline);

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    public string DescribeTerms(TradeOfferData offer)
    {
        var lines = new List<string>
        {
            $"<b>{UIStrings.TradeBenefitHeader}</b>",
            UIStrings.TradeTerm(offer.Benefit, CardName(offer.Benefit)),
            string.Empty,
            $"<b>{UIStrings.TradeDrawbackHeader}</b>",
            UIStrings.TradeTerm(offer.Drawback, CardName(offer.Drawback)),
            string.Empty,
            $"<i>{UIStrings.TradeDuration}</i>",
        };
        return string.Join("\n", lines);
    }

    private string CardName(TradeTerm term)
    {
        if (term.Kind != TradeTermKind.ExtraCard)
            return string.Empty;
        foreach (var card in _extraCards)
        {
            if (card != null && card.name == term.CardId)
                return card.CardName;
        }
        return term.CardId;
    }

    private void Decide(bool accepted)
    {
        if (_offer == null)
            return;
        var offer = _offer;
        var onDecided = _onDecided;
        _offer = null;
        _onDecided = null;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        onDecided?.Invoke(offer, accepted);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        if (button == null)
            return;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = value;
    }
}
