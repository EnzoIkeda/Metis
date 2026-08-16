using System;
using System.Collections.Generic;

/// <summary>
/// Mao de cartas do turno atual, comprada de um pool. Notifica mudancas via Observer.
/// TODO: Baralho de cartas finito, e com estrategia propria de onde a mao sera gerada.
/// </summary>
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

    public void Draw(int count)
    {
        if (_pool.Count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            _cards.Add(_pool[_random.Next(_pool.Count)]);
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

    /// <summary>
    /// Checagem simples to requisitos
    /// </summary>
    public bool CanPlay(CardData card, CityStats stats)
    {
        return card != null
            && _cards.Contains(card)
            && stats.GetValue(CityParameterType.Pesquisa) >= card.RequiredPesquisa
            && stats.GetValue(CityParameterType.Renda) >= card.Cost;
    }

    /// <summary>
    /// Joga uma carta da mao 
    /// </summary>
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
