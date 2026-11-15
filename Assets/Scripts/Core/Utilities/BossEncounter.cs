using System;
using System.Collections.Generic;

// Baralho finito do chefe: a proxima carta e sempre conhecida; esgotada a pilha, o descarte volta embaralhado.
public class BossDeck
{
    private readonly List<IBossCardDefinition> _drawPile = new List<IBossCardDefinition>();
    private readonly List<IBossCardDefinition> _discardPile = new List<IBossCardDefinition>();
    private readonly Random _random;

    public IBossCardDefinition Next { get; private set; }
    public IReadOnlyList<IBossCardDefinition> DrawPile => _drawPile;
    public IReadOnlyList<IBossCardDefinition> DiscardPile => _discardPile;

    public BossDeck(IEnumerable<IBossCardDefinition> cards, Random random)
    {
        _random = random ?? new Random();
        foreach (var card in cards)
        {
            if (card != null)
                _drawPile.Add(card);
        }
        Shuffle(_drawPile);
        Next = Take();
    }

    private BossDeck(Random random)
    {
        _random = random;
    }

    // Retoma pilhas salvas exatamente como estavam.
    public static BossDeck FromState(IBossCardDefinition next, IEnumerable<IBossCardDefinition> drawPile, IEnumerable<IBossCardDefinition> discardPile, Random random)
    {
        var deck = new BossDeck(random ?? new Random());
        deck._drawPile.AddRange(drawPile);
        deck._discardPile.AddRange(discardPile);
        deck.Next = next;
        return deck;
    }

    // Copia que conhece a proxima carta mas nao a ordem do resto da pilha.
    public BossDeck CloneShuffled(Random random)
    {
        var copy = FromState(Next, _drawPile, _discardPile, random);
        copy.Shuffle(copy._drawPile);
        return copy;
    }

    // A proxima foi jogada: vai para o descarte e a seguinte vira a proxima.
    public void Advance()
    {
        if (Next != null)
            _discardPile.Add(Next);
        Next = Take();
    }

    private IBossCardDefinition Take()
    {
        if (_drawPile.Count == 0 && _discardPile.Count > 0)
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_drawPile);
        }
        if (_drawPile.Count == 0)
            return null;
        var card = _drawPile[0];
        _drawPile.RemoveAt(0);
        return card;
    }

    private void Shuffle(List<IBossCardDefinition> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
}

// O chefe dentro da fase: baralho, Impeto e quantas cartas ele ja jogou. Mesma conta no jogo, no simulador e no planejamento.
public class BossEncounter
{
    public IBossDefinition Definition { get; }
    public BossDeck Deck { get; }
    public int Momentum { get; private set; }
    public int CardsPlayed { get; private set; }

    public IBossCardDefinition Next => Deck.Next;

    public BossEncounter(IBossDefinition definition, Random random)
        : this(definition, new BossDeck(definition.Cards, random), definition.StartingMomentum, 0)
    {
    }

    private BossEncounter(IBossDefinition definition, BossDeck deck, int momentum, int cardsPlayed)
    {
        Definition = definition;
        Deck = deck;
        Momentum = momentum;
        CardsPlayed = cardsPlayed;
    }

    public static BossEncounter FromState(IBossDefinition definition, BossDeck deck, int momentum, int cardsPlayed)
    {
        return new BossEncounter(definition, deck, Math.Max(0, momentum), Math.Max(0, cardsPlayed));
    }

    public BossEncounter Clone(Random random)
    {
        return new BossEncounter(Definition, Deck.CloneShuffled(random), Momentum, CardsPlayed);
    }

    // O chefe joga a proxima carta neste turno, com o ganho de Impeto do turno.
    public bool PlaysThisTurn => Next != null && Momentum + Definition.MomentumPerTurn >= Next.MomentumCost;

    // Em quantas fases do chefe a proxima carta sai, contando a deste turno como 1; -1 sem carta.
    public int TurnsUntilNextPlay
    {
        get
        {
            if (Next == null)
                return -1;
            var missing = Next.MomentumCost - Momentum;
            var gain = Math.Max(1, Definition.MomentumPerTurn);
            return Math.Max(1, (missing + gain - 1) / gain);
        }
    }

    // Multiplicador do dano da proxima carta.
    public float DamageFactor => BossRules.DamageFactor(Definition, CardsPlayed);

    // Efeitos que a proxima carta aplicaria agora, ja com a regra especial.
    public List<StatModifier> NextEffects()
    {
        return Next != null ? BossRules.ScaledEffects(Definition, Next, DamageFactor) : new List<StatModifier>();
    }

    // Quanto o custo das cartas do jogador sobe agora; zero fora do encarecimento.
    public int CardCostOffset => BossRules.CardCostOffset(Definition, CardsPlayed);

    // Fase do chefe dentro do turno: ganha Impeto e, se der para pagar, joga a proxima carta. Devolve a carta jogada ou nula.
    public IBossCardDefinition PlayTurn(CityStats stats)
    {
        Momentum += Definition.MomentumPerTurn;
        var card = Next;
        if (card == null || Momentum < card.MomentumCost)
            return null;

        Momentum -= card.MomentumCost;
        stats.ApplyModifiers(BossRules.ScaledEffects(Definition, card, DamageFactor));
        CardsPlayed++;
        Deck.Advance();
        return card;
    }
}

public static class BossRules
{
    // Fases vencidas antes do chefe.
    public const int PhasesBeforeBoss = 3;

    // O chefe que ataca o baralho; nulo quando o baralho ainda nao tem chefe.
    public static TBoss ForArchetype<TBoss>(IReadOnlyList<TBoss> bosses, CardArchetype archetype)
        where TBoss : class, IBossDefinition
    {
        if (bosses == null)
            return null;
        foreach (var boss in bosses)
        {
            if (boss != null && boss.TargetArchetype == archetype)
                return boss;
        }
        return null;
    }

    public static TBoss Find<TBoss>(IReadOnlyList<TBoss> bosses, string id)
        where TBoss : class, IBossDefinition
    {
        if (bosses == null || string.IsNullOrEmpty(id))
            return null;
        foreach (var boss in bosses)
        {
            if (boss != null && boss.Id == id)
                return boss;
        }
        return null;
    }

    public static IBossCardDefinition FindCard(IBossDefinition boss, string cardId)
    {
        if (boss == null || string.IsNullOrEmpty(cardId))
            return null;
        foreach (var card in boss.Cards)
        {
            if (card != null && card.Id == cardId)
                return card;
        }
        return null;
    }

    public static float DamageFactor(IBossDefinition boss, int cardsPlayed)
    {
        return boss.Mechanic == BossMechanic.GrowingDamage ? 1f + cardsPlayed * boss.MechanicAmount : 1f;
    }

    // Efeitos da carta com o multiplicador e, na cascata, o dano na Energia repassado a quem depende dela,
    // arredondado para inteiro (o que a tela mostra e o que vale); parcela que arredonda para zero fica de fora.
    public static List<StatModifier> ScaledEffects(IBossDefinition boss, IBossCardDefinition card, float factor)
    {
        var effects = new List<StatModifier>();
        foreach (var effect in card.Effects)
            effects.Add(new StatModifier { Parameter = effect.Parameter, Amount = effect.Amount * factor });
        if (boss == null || boss.Mechanic != BossMechanic.CascadeSpread)
            return effects;

        var energy = 0f;
        foreach (var effect in effects)
        {
            if (effect.Parameter == CityParameterType.Energia)
                energy += effect.Amount;
        }
        if (energy >= 0f)
            return effects;

        foreach (var target in CascadeTargets())
        {
            var amount = (float)Math.Round(energy * InteractionMatrix.GetCoefficient(target, CityParameterType.Energia) * boss.MechanicAmount, MidpointRounding.AwayFromZero);
            if (amount == 0f)
                continue;
            var index = effects.FindIndex(effect => effect.Parameter == target);
            if (index >= 0)
                effects[index] = new StatModifier { Parameter = target, Amount = effects[index].Amount + amount };
            else
                effects.Add(new StatModifier { Parameter = target, Amount = amount });
        }
        return effects;
    }

    // Quem depende da Energia na matriz, na ordem do enum: so as ligacoes positivas, porque o chefe so tira.
    public static List<CityParameterType> CascadeTargets()
    {
        var targets = new List<CityParameterType>();
        foreach (CityParameterType target in Enum.GetValues(typeof(CityParameterType)))
        {
            if (target != CityParameterType.Energia && target != CityParameterType.BemEstar
                && InteractionMatrix.GetCoefficient(target, CityParameterType.Energia) > 0f)
                targets.Add(target);
        }
        return targets;
    }

    // Encarecimento: aumento inteiro pelas cartas ja jogadas pelo chefe.
    public static int CardCostOffset(IBossDefinition boss, int cardsPlayed)
    {
        if (boss == null || boss.Mechanic != BossMechanic.CostIncrease)
            return 0;
        return (int)Math.Floor(cardsPlayed * boss.MechanicAmount + 0.0001f);
    }

    // Cartas extras que a jogada poe no baralho do jogador.
    public static int ExtraCardCount(IBossDefinition boss)
    {
        if (boss == null || boss.Mechanic != BossMechanic.ExtraCards)
            return 0;
        return Math.Max(0, (int)Math.Round(boss.MechanicAmount));
    }

    // Depois da jogada do chefe: poe as cartas extras na pilha de compra e atualiza o custo.
    // baseCostOffset e o que ja vinha das trocas; extraCard nulo ignora as cartas extras.
    public static void ApplyToHand<TCard>(BossEncounter boss, IBossCardDefinition played, CardHand<TCard> hand, TCard extraCard, int baseCostOffset)
        where TCard : class, ICardDefinition
    {
        if (boss == null || hand == null)
            return;
        if (played != null && extraCard != null)
            hand.InsertIntoDrawPile(extraCard, ExtraCardCount(boss.Definition));
        hand.CostOffset = baseCostOffset + boss.CardCostOffset;
    }
}
