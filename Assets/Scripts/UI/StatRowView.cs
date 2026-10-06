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

    public void SetValue(float value, bool isCritical)
    {
        if (_valueText != null)
            _valueText.text = value.ToString("0");
        if (_criticalHighlight != null)
            _criticalHighlight.enabled = isCritical;
    }
}
