using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Um parametro no mapa de relacoes: icone num circulo com a cor da zona atual.
public class RelationNodeView : MonoBehaviour
{
    [SerializeField] private Image _ring;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Button _button;
    [SerializeField] private CanvasGroup _canvasGroup;

    [SerializeField] private float _selectedScale = 1.15f;
    [SerializeField, Range(0f, 1f)] private float _dimmedAlpha = 0.35f;

    private Action _onClick;

    public RectTransform RectTransform => (RectTransform)transform;

    private void Awake()
    {
        if (_button != null)
            _button.onClick.AddListener(() => _onClick?.Invoke());
    }

    public void Bind(Sprite icon, string displayName, Action onClick)
    {
        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }
        SetName(displayName);
        _onClick = onClick;
    }

    public void SetName(string displayName)
    {
        if (_nameText != null)
            _nameText.text = displayName;
    }

    public void SetRingColor(Color color)
    {
        if (_ring != null)
            _ring.color = color;
    }

    public void SetState(bool selected, bool dimmed)
    {
        transform.localScale = Vector3.one * (selected ? _selectedScale : 1f);
        if (_canvasGroup != null)
            _canvasGroup.alpha = dimmed ? _dimmedAlpha : 1f;
    }
}
