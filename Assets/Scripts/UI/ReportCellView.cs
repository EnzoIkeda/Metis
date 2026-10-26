using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uma celula da planilha do relatorio: moldura com a cor da grade, preenchimento e texto.
public class ReportCellView : MonoBehaviour
{
    [SerializeField] private Image _frame;
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Button _button;

    public RectTransform Rect => (RectTransform)transform;

    public Image Frame => _frame;

    public Image Fill => _fill;

    public TMP_Text Text => _text;

    public Button Button => _button;
}
