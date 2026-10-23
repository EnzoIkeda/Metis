using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Quadro de uma ficha na grade do almanaque: miniatura a esquerda (quando houver) e o titulo.
public class AlmanacEntryView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _label;

    [Tooltip("Margem esquerda do titulo quando a miniatura aparece.")]
    [SerializeField] private float _labelLeftWithIcon = 104f;

    [Tooltip("Margem esquerda do titulo sem miniatura.")]
    [SerializeField] private float _labelLeftWithoutIcon = 24f;

    public void Bind(string label, Sprite icon)
    {
        var hasIcon = icon != null;
        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.gameObject.SetActive(hasIcon);
        }
        if (_label != null)
        {
            _label.text = label;
            var rect = _label.rectTransform;
            rect.offsetMin = new Vector2(hasIcon ? _labelLeftWithIcon : _labelLeftWithoutIcon, rect.offsetMin.y);
        }
    }
}
