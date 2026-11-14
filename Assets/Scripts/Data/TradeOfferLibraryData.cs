using System;
using System.Collections.Generic;
using UnityEngine;

// Todas as ofertas de troca que o mapa pode fazer.
[CreateAssetMenu(fileName = "TradeOfferLibrary", menuName = "Metis/Trade Offer Library")]
public class TradeOfferLibraryData : ScriptableObject
{
    [SerializeField] private TradeOfferData[] _offers = Array.Empty<TradeOfferData>();

    public IReadOnlyList<TradeOfferData> Offers => _offers;

    public TradeOfferData Find(string offerName)
    {
        if (string.IsNullOrEmpty(offerName))
            return null;
        foreach (var offer in _offers)
        {
            if (offer != null && offer.name == offerName)
                return offer;
        }
        return null;
    }

    // Trocas aceitas na rodada, na ordem em que foram aceitas.
    public List<TradeOfferData> Accepted(IEnumerable<string> acceptedIds)
    {
        return TradeOfferRules.Resolve(_offers, acceptedIds);
    }
}
