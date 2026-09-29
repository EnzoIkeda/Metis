using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class CardHandTests
{
    private static List<FakeCard> NumberedPool(int count)
    {
        return Enumerable.Range(0, count).Select(i => new FakeCard { Id = $"C{i}" }).ToList();
    }

    [Test]
    public void Draw_OnlyPicksCardsWithReachedRequiredPesquisa()
    {
        var locked = new FakeCard { Id = "Locked", RequiredPesquisa = 80f };
        var unlocked = new FakeCard { Id = "Unlocked" };
        var hand = new CardHand<FakeCard>(new List<FakeCard> { locked, unlocked });

        hand.Draw(10, CityStatsTestFactory.Build());

        Assert.That(hand.Cards, Has.Count.EqualTo(10));
        Assert.That(hand.Cards, Has.All.SameAs(unlocked));
    }

    [Test]
    public void Draw_SameSeed_ProducesSameHands()
    {
        var pool = NumberedPool(20);
        var first = new CardHand<FakeCard>(pool, new Random(42));
        var second = new CardHand<FakeCard>(pool, new Random(42));
        var stats = CityStatsTestFactory.Build();

        for (int turn = 0; turn < 5; turn++)
        {
            first.Draw(5, stats);
            second.Draw(5, stats);

            Assert.That(second.Cards.Select(card => card.Id), Is.EqualTo(first.Cards.Select(card => card.Id)));

            first.DiscardAll();
            second.DiscardAll();
        }
    }

    [Test]
    public void TryPlay_ChargesCostInRendaAppliesEffectsAndRemovesCard()
    {
        var card = new FakeCard
        {
            Cost = 10f,
            StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Saude, Amount = 5f } },
        };
        var hand = new CardHand<FakeCard>(new List<FakeCard> { card });
        var stats = CityStatsTestFactory.Build();
        hand.Draw(1, stats);

        var played = hand.TryPlay(card, stats);

        Assert.That(played, Is.True);
        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(CityStatsTestFactory.NeutralValue - 10f));
        Assert.That(stats.GetValue(CityParameterType.Saude), Is.EqualTo(CityStatsTestFactory.NeutralValue + 5f));
        Assert.That(hand.Cards, Is.Empty);
    }

    [Test]
    public void CanPlay_RendaBelowCost_ReturnsFalse()
    {
        var card = new FakeCard { Cost = CityStatsTestFactory.NeutralValue + 1f };
        var hand = new CardHand<FakeCard>(new List<FakeCard> { card });
        var stats = CityStatsTestFactory.Build();
        hand.Draw(1, stats);

        Assert.That(hand.CanPlay(card, stats), Is.False);
    }
}
