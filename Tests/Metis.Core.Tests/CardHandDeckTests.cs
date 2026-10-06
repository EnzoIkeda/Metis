using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class CardHandDeckTests
{
    private static List<FakeCard> NumberedPool(int count, float requiredPesquisa = 0f)
    {
        return Enumerable.Range(0, count).Select(i => new FakeCard { Id = $"C{i}", RequiredPesquisa = requiredPesquisa }).ToList();
    }

    [Test]
    public void FiniteDeck_DrawsEachCardOnceBeforeReshuffling()
    {
        var pool = NumberedPool(10);
        var hand = new CardHand<FakeCard>(pool, new Random(1), finiteDeck: true);
        var stats = CityStatsTestFactory.Build();
        var seen = new List<FakeCard>();

        for (int turn = 0; turn < 2; turn++)
        {
            hand.Draw(5, stats);
            seen.AddRange(hand.Cards);
            hand.DiscardAll();
        }

        Assert.That(seen, Is.EquivalentTo(pool), "10 cartas em 2 maos de 5 deveriam sair todas, sem repetir");
        Assert.That(hand.DrawPile, Is.Empty);
        Assert.That(hand.DiscardPile, Has.Count.EqualTo(10));
    }

    [Test]
    public void FiniteDeck_EmptyDrawPile_ReshufflesDiscardAndKeepsDrawing()
    {
        var pool = NumberedPool(6);
        var hand = new CardHand<FakeCard>(pool, new Random(2), finiteDeck: true);
        var stats = CityStatsTestFactory.Build();
        hand.Draw(5, stats);
        hand.DiscardAll();

        hand.Draw(5, stats);

        Assert.That(hand.Cards, Has.Count.EqualTo(5));
        Assert.That(hand.DrawPile.Count + hand.DiscardPile.Count + hand.Cards.Count, Is.EqualTo(6));
    }

    [Test]
    public void FiniteDeck_LockedCardsStayInThePileUntilUnlocked()
    {
        var locked = NumberedPool(3, requiredPesquisa: 80f);
        var open = NumberedPool(2);
        var hand = new CardHand<FakeCard>(locked.Concat(open).ToList(), new Random(3), finiteDeck: true);
        var stats = CityStatsTestFactory.Build();

        hand.Draw(5, stats);

        Assert.That(hand.Cards, Is.EquivalentTo(open));
        Assert.That(hand.DrawPile, Is.EquivalentTo(locked));
    }

    [Test]
    public void FiniteDeck_PlayedCardGoesToDiscard()
    {
        var pool = NumberedPool(5);
        var hand = new CardHand<FakeCard>(pool, new Random(4), finiteDeck: true);
        var stats = CityStatsTestFactory.Build();
        hand.Draw(5, stats);
        var played = hand.Cards[0];

        hand.TryPlay(played, stats);

        Assert.That(hand.DiscardPile, Has.Member(played));
        Assert.That(hand.Cards, Has.No.Member(played));
    }

    [Test]
    public void FiniteDeck_SameSeed_SameOrder()
    {
        var pool = NumberedPool(20);
        var first = new CardHand<FakeCard>(pool, new Random(9), finiteDeck: true);
        var second = new CardHand<FakeCard>(pool, new Random(9), finiteDeck: true);
        var stats = CityStatsTestFactory.Build();

        first.Draw(5, stats);
        second.Draw(5, stats);

        Assert.That(second.Cards.Select(card => card.Id), Is.EqualTo(first.Cards.Select(card => card.Id)));
    }

    [Test]
    public void Reveal_LastsUntilTheHandIsDiscarded()
    {
        var hand = new CardHand<FakeCard>(NumberedPool(3));
        var stats = CityStatsTestFactory.Build();
        hand.Draw(3, stats);
        var notifications = 0;
        hand.OnHandChanged += () => notifications++;

        hand.Reveal();
        Assert.That(hand.IsRevealed, Is.True);
        Assert.That(notifications, Is.EqualTo(1));

        hand.DiscardAll();
        Assert.That(hand.IsRevealed, Is.False);
    }

    [Test]
    public void RevealUntilPhaseEnd_SurvivesDiscardAndNewDraws()
    {
        var hand = new CardHand<FakeCard>(NumberedPool(6), new Random(1), finiteDeck: true);
        var stats = CityStatsTestFactory.Build();
        hand.Draw(3, stats);

        hand.Reveal(untilPhaseEnd: true);
        hand.DiscardAll();
        hand.Draw(3, stats);

        Assert.That(hand.IsRevealed, Is.True);
    }

    [Test]
    public void SearchCandidates_SkipLockedCardsAndAbilityCards()
    {
        var open = new FakeCard { Id = "Aberta" };
        var locked = new FakeCard { Id = "Travada", RequiredPesquisa = 80f };
        var search = new FakeCard { Id = "Busca", Ability = CardAbility.SearchDeck };
        var hand = new CardHand<FakeCard>(new List<FakeCard> { open, locked, search });

        var candidates = hand.SearchCandidates(CityStatsTestFactory.Build());

        Assert.That(candidates, Is.EquivalentTo(new[] { open }));
    }

    [Test]
    public void TakeFromDeck_FiniteDeck_MovesTheCardFromThePileToTheHand()
    {
        var pool = NumberedPool(8);
        var hand = new CardHand<FakeCard>(pool, new Random(5), finiteDeck: true);
        var stats = CityStatsTestFactory.Build();
        var wanted = hand.DrawPile.Last();

        var taken = hand.TakeFromDeck(wanted, stats);

        Assert.That(taken, Is.True);
        Assert.That(hand.Cards, Has.Member(wanted));
        Assert.That(hand.DrawPile, Has.No.Member(wanted));
        Assert.That(hand.SearchCandidates(stats), Has.No.Member(wanted), "carta na mao nao e mais candidata");
    }

    [Test]
    public void TakeFromDeck_CardThatIsNotACandidate_IsRefused()
    {
        var locked = new FakeCard { Id = "Travada", RequiredPesquisa = 80f };
        var hand = new CardHand<FakeCard>(new List<FakeCard> { locked });

        Assert.That(hand.TakeFromDeck(locked, CityStatsTestFactory.Build()), Is.False);
        Assert.That(hand.Cards, Is.Empty);
    }

    [Test]
    public void IsFreeAction_OnlyForCardsWithAbility()
    {
        Assert.That(CardRules.IsFreeAction(new FakeCard()), Is.False);
        Assert.That(CardRules.IsFreeAction(new FakeCard { Ability = CardAbility.SearchDeck }), Is.True);
        Assert.That(CardRules.IsFreeAction(new FakeCard { Ability = CardAbility.RevealHand }), Is.True);
    }
}
