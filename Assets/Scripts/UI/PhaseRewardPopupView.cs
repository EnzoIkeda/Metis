using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Popup de recompensa de fim de fase, mostrado so na vitoria: sorteia opcoes entre cartas e vantagens passivas.
public class PhaseRewardPopupView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;

    [Tooltip("Abre depois do relatorio de fim de fase; vazio, abre direto no fim de jogo.")]
    [SerializeField] private PhaseEndFlow _endFlow;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _hintText;

    [Tooltip("Uma carta por opcao; o botao de escolher de cada uma faz a escolha.")]
    [SerializeField] private RewardCardView[] _optionCards = Array.Empty<RewardCardView>();

    [Tooltip("Distancia entre os centros das cartas; com menos opcoes, as que sobram ficam centralizadas.")]
    [SerializeField, Min(0f)] private float _cardSpacing = 320f;

    // Limitado pela quantidade de cartas de opcao montadas no painel.
    [SerializeField, Min(1)] private int _optionCount = 3;

    private readonly System.Random _random = new System.Random();
    private UnityEngine.Object[] _currentOptions = Array.Empty<UnityEngine.Object>();

    private void Start()
    {
        if (_endFlow != null)
            _endFlow.OnContinue += HandleGameEnded;
        else
            _turnManager.Machine.OnGameEnded += HandleGameEnded;
        if (_titleText != null)
            _titleText.text = UIStrings.RewardTitle;
        for (int i = 0; i < _optionCards.Length; i++)
        {
            var index = i;
            if (_optionCards[i] != null && _optionCards[i].ChooseButton != null)
                _optionCards[i].ChooseButton.onClick.AddListener(() => SelectOption(index));
        }

        Hide();
    }

    private void OnDisable()
    {
        if (_endFlow != null)
            _endFlow.OnContinue -= HandleGameEnded;
        if (_turnManager != null && _turnManager.Machine != null)
            _turnManager.Machine.OnGameEnded -= HandleGameEnded;
    }

    private void HandleGameEnded(GameOutcome outcome)
    {
        // Cenario volta para a selecao e o chefe encerra a rodada, sem recompensa nem mapa.
        if (outcome != GameOutcome.Victory || SessionMode.IsScenario || _turnManager.Boss != null)
            return;

        _currentOptions = DrawOptions();

        // Sem nada novo pra oferecer, segue direto pro mapa em vez de abrir um popup sem opcao.
        if (_currentOptions.Length == 0)
        {
            SceneManager.LoadScene("PhaseMap");
            return;
        }
        if (_titleText != null)
            _titleText.text = UIStrings.RewardTitle;
        if (_hintText != null)
            _hintText.text = UIStrings.RewardHint;
        for (int i = 0; i < _optionCards.Length; i++)
        {
            var hasOption = i < _currentOptions.Length;
            _optionCards[i].gameObject.SetActive(hasOption);
            if (hasOption == false)
                continue;

            _optionCards[i].Bind(_currentOptions[i]);
            // Centraliza a fileira pelo numero de opcoes de verdade.
            var rect = (RectTransform)_optionCards[i].transform;
            rect.anchoredPosition = new Vector2((i - (_currentOptions.Length - 1) / 2f) * _cardSpacing, rect.anchoredPosition.y);
        }

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    // Sorteia so entre o que ainda acrescenta algo: cartas fora do baralho atual e vantagens nao carregadas.
    private UnityEngine.Object[] DrawOptions()
    {
        var deck = DeckBuilder.Build(_turnManager.CardPool, MetaProgressionManager.Archetype, MetaProgressionManager.LoadedCardNames,
            TradeOfferRules.ExtraCardIds(_turnManager.AcceptedTrades));
        var pool = new List<UnityEngine.Object>();
        pool.AddRange(RewardOptionPicker.CardPool(_turnManager.CardPool, deck));
        pool.AddRange(RewardOptionPicker.AdvantagePool(_turnManager.AdvantagePool, MetaProgressionManager.LoadedAdvantageNames));

        // As trocas aceitas mudam o numero de opcoes, ate o limite de cartas do painel.
        var count = Math.Min(TradeOfferRules.RewardOptionCount(_optionCount, _turnManager.AcceptedTrades), _optionCards.Length);
        return RewardOptionPicker.Draw(pool, count, _random).ToArray();
    }

    private void SelectOption(int index)
    {
        if (index >= _currentOptions.Length)
            return;

        var option = _currentOptions[index];
        if (option is CardData card)
            MetaProgressionManager.AddCardReward(card.name);
        else if (option is PassiveAdvantageData advantage)
            MetaProgressionManager.AddAdvantageReward(advantage.name);

        // Ja marca o mapa como ponto de retomada, pra recompensa nao poder ser escolhida de novo.
        MetaProgressionManager.EnterPhaseMap();
        Hide();
        SceneManager.LoadScene("PhaseMap");
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
