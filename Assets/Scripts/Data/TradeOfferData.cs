using UnityEngine;

// Oferta de troca do mapa entre fases: uma vantagem e uma desvantagem que valem ate o fim da rodada.
[CreateAssetMenu(fileName = "New Trade Offer", menuName = "Metis/Trade Offer Data")]
public class TradeOfferData : ScriptableObject, ITradeOfferDefinition
{
    [SerializeField] private string _offerName;
    [SerializeField] private string _offerNameEn;
    [SerializeField, TextArea(2, 6)] private string _summary;
    [SerializeField, TextArea(2, 6)] private string _summaryEn;
    [SerializeField] private KnowledgeEntryData _knowledgeEntry;
    [SerializeField] private TradeTerm _benefit;
    [SerializeField] private TradeTerm _drawback;

    public string OfferName => Pick(_offerName, _offerNameEn);
    public string Summary => Pick(_summary, _summaryEn);
    public KnowledgeEntryData KnowledgeEntry => _knowledgeEntry;

    string ITradeOfferDefinition.Id => name;
    public TradeTerm Benefit => _benefit;
    public TradeTerm Drawback => _drawback;

    private static string Pick(string portuguese, string english)
    {
        return LocalizationManager.Current == Language.English && string.IsNullOrEmpty(english) == false ? english : portuguese;
    }
}
