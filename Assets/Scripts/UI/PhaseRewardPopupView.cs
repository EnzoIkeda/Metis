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
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button[] _optionButtons;
    [SerializeField] private TMP_Text[] _optionTexts;

    // Limitado pela quantidade de botoes de opcao montados no painel.
    [SerializeField, Min(1)] private int _optionCount = 3;

    private readonly System.Random _random = new System.Random();
    private UnityEngine.Object[] _currentOptions = Array.Empty<UnityEngine.Object>();

    private void Start()
    {
        _turnManager.Machine.OnGameEnded += HandleGameEnded;
        for (int i = 0; i < _optionButtons.Length; i++)
        {
            var index = i;
            _optionButtons[i].onClick.AddListener(() => SelectOption(index));
        }

        Hide();
    }

    private void OnDisable()
    {
        if (_turnManager != null && _turnManager.Machine != null)
            _turnManager.Machine.OnGameEnded -= HandleGameEnded;
    }

    private void HandleGameEnded(GameOutcome outcome)
    {
        if (outcome != GameOutcome.Victory)
            return;

        _currentOptions = DrawOptions();

        // Sem nada novo pra oferecer, segue direto pro mapa em vez de abrir um popup sem opcao.
        if (_currentOptions.Length == 0)
        {
            SceneManager.LoadScene("PhaseMap");
            return;
        }
        for (int i = 0; i < _optionButtons.Length; i++)
        {
            var hasOption = i < _currentOptions.Length;
            _optionButtons[i].gameObject.SetActive(hasOption);
            if (hasOption == false)
                continue;

            if (i < _optionTexts.Length)
                _optionTexts[i].text = DescribeOption(_currentOptions[i]);
        }

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    // Sorteia so entre o que ainda acrescenta algo: cartas fora do baralho atual e vantagens nao carregadas.
    private UnityEngine.Object[] DrawOptions()
    {
        var deck = DeckBuilder.Build(_turnManager.CardPool, MetaProgressionManager.Archetype, MetaProgressionManager.LoadedCardNames);
        var pool = new List<UnityEngine.Object>();
        pool.AddRange(RewardOptionPicker.CardPool(_turnManager.CardPool, deck));
        pool.AddRange(RewardOptionPicker.AdvantagePool(_turnManager.AdvantagePool, MetaProgressionManager.LoadedAdvantageNames));

        var count = Math.Min(_optionCount, _optionButtons.Length);
        return RewardOptionPicker.Draw(pool, count, _random).ToArray();
    }

    private static string DescribeOption(UnityEngine.Object option)
    {
        if (option is CardData card)
            return $"{card.CardName}\n{card.Description}";
        if (option is PassiveAdvantageData advantage)
            return $"{advantage.AdvantageName}\n{advantage.Description}";
        return string.Empty;
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

        Hide();
        SceneManager.LoadScene("PhaseMap");
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
