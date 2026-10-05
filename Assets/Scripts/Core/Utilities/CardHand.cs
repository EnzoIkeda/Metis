using System;
using System.Collections.Generic;

// Mao de cartas do turno atual. Com baralho finito compra de uma pilha embaralhada e descarta numa pilha propria; sem ele, compra com reposicao do pool inteiro.
public class CardHand<TCard> where TCard : class, ICardDefinition
{
    private readonly IReadOnlyList<TCard> _pool;
    private readonly List<TCard> _cards = new List<TCard>();
    private readonly Random _random;
    private bool _finiteDeck;
    private readonly List<TCard> _drawPile = new List<TCard>();
    private readonly List<TCard> _discardPile = new List<TCard>();

    public IReadOnlyList<TCard> Cards => _cards;

    public bool FiniteDeck => _finiteDeck;

    public IReadOnlyList<TCard> DrawPile => _drawPile;

    public IReadOnlyList<TCard> DiscardPile => _discardPile;

    // Efeito exato das cartas visivel: so ate o descarte, ou ate o fim da fase quando a revelacao e permanente.
    public bool IsRevealed { get; private set; }

    private bool _revealedUntilPhaseEnd;

    public event Action OnHandChanged;

    // random e opcional, passar um com seed fixa deixa a compra reproduzivel.
    public CardHand(IReadOnlyList<TCard> pool, Random random = null, bool finiteDeck = false)
    {
        _pool = pool;
        _random = random ?? new Random();
        _finiteDeck = finiteDeck;

        if (_finiteDeck)
        {
            _drawPile.AddRange(pool);
            Shuffle(_drawPile);
        }
    }

    // Baralho finito com pilhas ja conhecidas: a pilha de compra e embaralhada (a ordem e o que nao se sabe), o descarte fica como esta.
    public static CardHand<TCard> FromPiles(IReadOnlyList<TCard> pool, IEnumerable<TCard> drawPile, IEnumerable<TCard> discardPile, Random random)
    {
        var hand = new CardHand<TCard>(pool, random, finiteDeck: false);
        hand._finiteDeck = true;
        hand._drawPile.AddRange(drawPile);
        hand._discardPile.AddRange(discardPile);
        hand.Shuffle(hand._drawPile);
        return hand;
    }

    // So compra carta cujo RequiredPesquisa ja foi atingido, senao a mao vem cheia de carta travada.
    public void Draw(int count, CityStats stats)
    {
        var drawn = _finiteDeck ? DrawFromPile(count, stats) : DrawWithReplacement(count, stats);
        if (drawn > 0)
            OnHandChanged?.Invoke();
    }

    public void DiscardAll()
    {
        var wasRevealed = IsRevealed;
        IsRevealed = _revealedUntilPhaseEnd;
        if (_cards.Count == 0 && wasRevealed == IsRevealed)
            return;

        if (_finiteDeck)
            _discardPile.AddRange(_cards);
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
        if (_finiteDeck)
            _discardPile.Add(card);
        OnHandChanged?.Invoke();
        return true;
    }

    // A mao vive uma fase so, entao "ate o fim da fase" e simplesmente nao desfazer no descarte.
    public void Reveal(bool untilPhaseEnd = false)
    {
        var changed = IsRevealed == false;
        IsRevealed = true;
        _revealedUntilPhaseEnd |= untilPhaseEnd;
        if (changed)
            OnHandChanged?.Invoke();
    }

    // Cartas que a busca pode trazer: liberadas pela Pesquisa, sem habilidade, e fora da mao quando o baralho e finito.
    public List<TCard> SearchCandidates(CityStats stats)
    {
        var source = new List<TCard>();
        if (_finiteDeck)
        {
            source.AddRange(_drawPile);
            source.AddRange(_discardPile);
        }
        else
        {
            source.AddRange(_pool);
        }

        var candidates = new List<TCard>();
        foreach (var card in source)
        {
            if (CardRules.IsFreeAction(card) == false
                && stats.GetValue(CityParameterType.Pesquisa) >= card.RequiredPesquisa
                && candidates.Contains(card) == false)
                candidates.Add(card);
        }
        return candidates;
    }

    // Traz uma das cartas da busca pra mao, tirando ela da pilha de onde veio quando o baralho e finito.
    public bool TakeFromDeck(TCard card, CityStats stats)
    {
        if (card == null || SearchCandidates(stats).Contains(card) == false)
            return false;

        if (_finiteDeck && _drawPile.Remove(card) == false)
            _discardPile.Remove(card);

        _cards.Add(card);
        OnHandChanged?.Invoke();
        return true;
    }

    private int DrawWithReplacement(int count, CityStats stats)
    {
        var eligible = new List<TCard>();
        foreach (var card in _pool)
        {
            if (stats.GetValue(CityParameterType.Pesquisa) >= card.RequiredPesquisa)
                eligible.Add(card);
        }

        if (eligible.Count == 0)
            return 0;

        for (int i = 0; i < count; i++)
            _cards.Add(eligible[_random.Next(eligible.Count)]);
        return count;
    }

    // Compra na ordem da pilha pulando as travadas, que ficam no lugar; sem nenhuma liberada, reembaralha o descarte uma vez.
    private int DrawFromPile(int count, CityStats stats)
    {
        var drawn = 0;
        var reshuffled = false;
        while (drawn < count)
        {
            var index = _drawPile.FindIndex(card => stats.GetValue(CityParameterType.Pesquisa) >= card.RequiredPesquisa);
            if (index < 0)
            {
                if (reshuffled || _discardPile.Count == 0)
                    break;

                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                Shuffle(_drawPile);
                reshuffled = true;
                continue;
            }

            _cards.Add(_drawPile[index]);
            _drawPile.RemoveAt(index);
            drawn++;
        }
        return drawn;
    }

    private void Shuffle(List<TCard> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
}
