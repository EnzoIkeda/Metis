using UnityEngine;

// Texto curto do jornal: fala de morador, classificado, horoscopo ou previsao da poluicao.
// So a previsao pode citar {valor}, trocado pela Sustentabilidade do turno.
[CreateAssetMenu(fileName = "New News Column", menuName = "Metis/News Column")]
public class NewsColumnData : ScriptableObject, INewsColumnDefinition
{
    [SerializeField] private NewsColumnKind _kind;
    [SerializeField] private NewsCitizen _citizen;
    [SerializeField] private NewsMoodBand _mood;
    [SerializeField, TextArea(2, 4)] private string _text;
    [SerializeField, TextArea(2, 4)] private string _textEn;

    public string Id => name;

    public NewsColumnKind Kind => _kind;

    public NewsCitizen Citizen => _citizen;

    public NewsMoodBand Mood => _mood;

    public string Text
    {
        get
        {
            if (LocalizationManager.Current == Language.English)
                return string.IsNullOrEmpty(_textEn) ? _text : _textEn;
            return string.IsNullOrEmpty(_text) ? _textEn : _text;
        }
    }
}
