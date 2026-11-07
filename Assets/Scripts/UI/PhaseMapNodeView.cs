using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Um destino no mapa: alfinete, nome e descricao.
public class PhaseMapNodeView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _pin;
    [SerializeField] private TMP_Text _label;

    public Button Button => _button;

    public void Bind(string title, string description, Sprite pin, bool available, Action onSelected)
    {
        _label.text = $"<b>{title}</b>\n<size=70%>{description}</size>";
        _pin.sprite = pin;
        _button.interactable = available;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onSelected());
    }
}
