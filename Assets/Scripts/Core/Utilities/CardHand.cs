using System;
using System.Collections.Generic;

// Mao de cartas do turno atual, comprada de um pool.
// TODO: baralho de cartas finito, com estrategia propria de geracao da mao.
public class CardHand<TCard> where TCard : class, ICardDefinition
{
    private readonly IReadOnlyList<TCard> _pool;
    private readonly List<TCard> _cards = new List<TCard>();
    private readonly Random _random;

    public IReadOnlyList<TCard> Cards => _cards;

    public event Action OnHandChanged;

    // random e opcional, passar um com seed fixa deixa a compra reproduzivel.
    public CardHand(IReadOnlyList<TCard> pool, Random random = null)
    {
        _pool = pool;
        _random = random ?? new Random();
    }

    // So compra carta cujo RequiredPesquisa ja foi atingido, senao a mao vem cheia de carta travada.
    public void Draw(int count, CityStats stats)
    {
        var eligible = new List<TCard>();
        foreach (var card in _pool)
        {
            if (stats.GetValue(CityParameterType.Pesquisa) >= card.RequiredPesquisa)
                eligible.Add(card);
        }

        if (eligible.Count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            _cards.Add(eligible[_random.Next(eligible.Count)]);
        }
        OnHandChanged?.Invoke();
    }

    public void DiscardAll()
    {
        if (_cards.Count == 0)
            return;

        _cards.Clear();
        OnHandChanged?.Invoke();
    }

    // Checagem simples dos requisitos pra jogar a carta.
    public bool CanPlay(TCard card, CityStats stats)
    {
        return card != null
            && _cards.Contains(card)
            && stats.GetValue(CityParameterType.Pesquisa) >= card.RequiredPesquisa
            && stats.GetValue(CityParameterType.Renda) >= card.Cost;
    }

    // Joga uma carta da mao.
    public bool TryPlay(TCard card, CityStats stats)
    {
        if (CanPlay(card, stats) == false)
            return false;

        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = -card.Cost });
        stats.ApplyModifiers(card.StatEffects);

        _cards.Remove(card);
        OnHandChanged?.Invoke();
        return true;
    }
}
