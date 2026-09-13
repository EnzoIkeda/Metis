using System;
using System.Collections.Generic;

// Mao de cartas do turno atual, comprada de um pool.
// TODO: baralho de cartas finito, com estrategia propria de geracao da mao.
public class CardHand
{
    private readonly IReadOnlyList<CardData> _pool;
    private readonly List<CardData> _cards = new List<CardData>();
    private readonly Random _random = new Random();

    public IReadOnlyList<CardData> Cards => _cards;

    public event Action OnHandChanged;

    public CardHand(IReadOnlyList<CardData> pool)
    {
        _pool = pool;
    }

    // So compra carta cujo RequiredPesquisa ja foi atingido, senao a mao vem cheia de carta travada.
    public void Draw(int count, CityStats stats)
    {
        var eligible = new List<CardData>();
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
    public bool CanPlay(CardData card, CityStats stats)
    {
        return card != null
            && _cards.Contains(card)
            && stats.GetValue(CityParameterType.Pesquisa) >= card.RequiredPesquisa
            && stats.GetValue(CityParameterType.Renda) >= card.Cost;
    }

    // Joga uma carta da mao.
    public bool TryPlay(CardData card, CityStats stats)
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
