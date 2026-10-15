using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uma linha da barra superior mostrando um parametro.
public class StatRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text _labelText;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private Image _criticalHighlight;
    [SerializeField] private Image _iconImage;

    // Fundo claro atras do icone, pra icones escuros nao sumirem no painel azul.
    [SerializeField] private GameObject _iconRoot;

    // Toque na linha abre a explicacao da variacao.
    [SerializeField] private Button _button;

    // Seta de variacao do ultimo turno, apontando pra cima por padrao.
    [SerializeField] private Image _deltaArrow;

    private Action _onClick;

    private void Awake()
    {
        if (_button != null)
            _button.onClick.AddListener(HandleClicked);
        SetDelta(0, Color.white);
    }

    public void SetClickHandler(Action onClick)
    {
        _onClick = onClick;
    }

    // direction: 1 subiu, -1 caiu, 0 esconde a seta.
    public void SetDelta(int direction, Color color)
    {
        if (_deltaArrow == null)
            return;

        _deltaArrow.gameObject.SetActive(direction != 0);
        _deltaArrow.color = color;
        _deltaArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, direction < 0 ? 180f : 0f);
    }

    private void HandleClicked()
    {
        _onClick?.Invoke();
    }

    public void SetLabel(string label)
    {
        if (_labelText != null)
            _labelText.text = label;
    }

    // Com icone, o icone toma o lugar do nome; sem icone, o nome continua aparecendo.
    public void SetIcon(Sprite icon)
    {
        var hasIcon = icon != null && _iconImage != null;
        if (_iconImage != null)
            _iconImage.sprite = icon;
        if (_iconRoot != null)
            _iconRoot.SetActive(hasIcon);
        else if (_iconImage != null)
            _iconImage.gameObject.SetActive(hasIcon);
        if (_labelText != null)
            _labelText.gameObject.SetActive(hasIcon == false);
    }

    // Recebe o valor ja formatado (unidade real ou indice).
    public void SetValue(string formattedValue, bool isCritical)
    {
        if (_valueText != null)
            _valueText.text = formattedValue;
        if (_criticalHighlight != null)
            _criticalHighlight.enabled = isCritical;
    }
}
