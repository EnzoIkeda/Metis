using UnityEngine;

// Noticia do jornal: manchete e texto curto, nos dois idiomas, publicada quando o gatilho acontece.
// O texto pode citar {valor}, {variacao}, {turno} e {parametro}, trocados pelos numeros do turno.
[CreateAssetMenu(fileName = "New News Story", menuName = "Metis/News Story")]
public class NewsStoryData : ScriptableObject, INewsStoryDefinition
{
    [SerializeField] private NewsTrigger _trigger;

    // So vale nos gatilhos que dependem de um parametro.
    [SerializeField] private CityParameterType _parameter;

    [SerializeField] private string _headline;
    [SerializeField] private string _headlineEn;
    [SerializeField, TextArea(2, 6)] private string _body;
    [SerializeField, TextArea(2, 6)] private string _bodyEn;

    public string Id => name;

    public NewsTrigger Trigger => _trigger;

    public CityParameterType Parameter => _parameter;

    public string Headline => Localized(_headline, _headlineEn);

    public string Body => Localized(_body, _bodyEn);

    // Texto no idioma atual, com fallback pro outro idioma se a traducao estiver vazia.
    private static string Localized(string pt, string en)
    {
        if (LocalizationManager.Current == Language.English)
            return string.IsNullOrEmpty(en) ? pt : en;
        return string.IsNullOrEmpty(pt) ? en : pt;
    }
}
