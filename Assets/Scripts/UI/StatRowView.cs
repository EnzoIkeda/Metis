using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Uma linha da barra superior para mostrar os parametros
/// </summary>
public class StatRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text _labelText;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private Image _criticalHighlight;

    public void SetLabel(string label)
    {
        if (_labelText != null)
            _labelText.text = label;
    }

    public void SetValue(float value, bool isCritical)
    {
        if (_valueText != null)
            _valueText.text = value.ToString("0");
        if (_criticalHighlight != null)
            _criticalHighlight.enabled = isCritical;
    }
}
