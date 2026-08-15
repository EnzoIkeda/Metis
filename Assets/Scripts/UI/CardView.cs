using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Uma carta na mao
/// </summary>
public class CardView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Button _button;

    private CardData _card;
    private Action<CardData> _onClicked;

    public void Bind(CardData card, Action<CardData> onClicked)
    {
        _card = card;
        _onClicked = onClicked;

        if (_nameText != null)
            _nameText.text = card.CardName;
        if (_descriptionText != null)
            _descriptionText.text = card.Description;
        if (_costText != null)
            _costText.text = card.Cost.ToString("0");

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        _onClicked?.Invoke(_card);
    }
}
