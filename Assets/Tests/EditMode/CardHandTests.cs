using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CardHandTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private CardData Card(string name, float requiredPesquisa = 0f, StatModifier[] statEffects = null, float cost = 0f)
    {
        var card = TestDataFactory.CreateCard(name: name, requiredPesquisa: requiredPesquisa, statEffects: statEffects, cost: cost);
        _created.Add(card);
        return card;
    }

    [Test]
    public void Draw_OnlyPicksCardsWithReachedRequiredPesquisa()
    {
        var locked = Card("Locked", requiredPesquisa: 80f); // Pesquisa neutro do factory e 50, fica de fora do pool elegivel
        var unlocked = Card("Unlocked");
        var hand = new CardHand(new List<CardData> { locked, unlocked });
        var stats = EditModeCityStatsFactory.Build();

        hand.Draw(10, stats);

        Assert.That(hand.Cards, Has.None.EqualTo(locked));
        Assert.That(hand.Cards, Has.All.EqualTo(unlocked));
    }

    [Test]
    public void Draw_NoEligibleCards_LeavesHandEmpty()
    {
        var locked = Card("Locked", requiredPesquisa: 80f);
        var hand = new CardHand(new List<CardData> { locked });
        var stats = EditModeCityStatsFactory.Build();

        hand.Draw(5, stats);

        Assert.That(hand.Cards, Is.Empty);
    }

    [Test]
    public void Draw_FiresOnHandChanged_OnlyWhenSomethingWasDrawn()
    {
        var locked = Card("Locked", requiredPesquisa: 80f);
        var unlocked = Card("Unlocked");
        var stats = EditModeCityStatsFactory.Build();
        var lockedHand = new CardHand(new List<CardData> { locked });
        var unlockedHand = new CardHand(new List<CardData> { unlocked });
        var lockedFired = false;
        var unlockedFired = false;
        lockedHand.OnHandChanged += () => lockedFired = true;
        unlockedHand.OnHandChanged += () => unlockedFired = true;

        lockedHand.Draw(1, stats);
        unlockedHand.Draw(1, stats);

        Assert.That(lockedFired, Is.False);
        Assert.That(unlockedFired, Is.True);
    }

    [Test]
    public void CanPlay_AffordableCardInHand_ReturnsTrue()
    {
        var card = Card("Affordable", cost: 10f);
        var hand = new CardHand(new List<CardData> { card }); // pool com 1 carta so, Draw sempre pega ela
        var stats = EditModeCityStatsFactory.Build(); // Renda neutro = 50
        hand.Draw(1, stats);

        Assert.That(hand.CanPlay(card, stats), Is.True);
    }

    [Test]
    public void CanPlay_CostAboveAvailableRenda_ReturnsFalse()
    {
        var card = Card("Expensive", cost: 1000f);
        var hand = new CardHand(new List<CardData> { card });
        var stats = EditModeCityStatsFactory.Build();
        hand.Draw(1, stats);

        Assert.That(hand.CanPlay(card, stats), Is.False);
    }

    [Test]
    public void CanPlay_CardNotInHand_ReturnsFalse()
    {
        var card = Card("NotDrawn");
        var hand = new CardHand(new List<CardData> { card }); // nunca chamou Draw

        Assert.That(hand.CanPlay(card, EditModeCityStatsFactory.Build()), Is.False);
    }

    [Test]
    public void TryPlay_AppliesCostAndStatEffectsAndRemovesFromHand()
    {
        var statEffects = new[] { new StatModifier { Parameter = CityParameterType.Sustentabilidade, Amount = 5f } };
        var card = Card("Effectful", cost: 10f, statEffects: statEffects);
        var hand = new CardHand(new List<CardData> { card });
        var stats = EditModeCityStatsFactory.Build();
        hand.Draw(1, stats);

        var played = hand.TryPlay(card, stats);

        Assert.That(played, Is.True);
        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(40f).Within(0.001f));
        Assert.That(stats.GetValue(CityParameterType.Sustentabilidade), Is.EqualTo(55f).Within(0.001f));
        Assert.That(hand.Cards, Has.No.Member(card));
    }

    [Test]
    public void TryPlay_WhenCanPlayFails_DoesNothingAndReturnsFalse()
    {
        var card = Card("Expensive", cost: 1000f);
        var hand = new CardHand(new List<CardData> { card });
        var stats = EditModeCityStatsFactory.Build();
        hand.Draw(1, stats);
        var rendaAntes = stats.GetValue(CityParameterType.Renda);

        var played = hand.TryPlay(card, stats);

        Assert.That(played, Is.False);
        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(rendaAntes));
        Assert.That(hand.Cards, Has.Member(card));
    }

    [Test]
    public void DiscardAll_ClearsHand_FiresEventOnlyWhenNotAlreadyEmpty()
    {
        var card = Card("Any");
        var hand = new CardHand(new List<CardData> { card });
        var stats = EditModeCityStatsFactory.Build();
        hand.Draw(1, stats);
        var fireCount = 0;
        hand.OnHandChanged += () => fireCount++;

        hand.DiscardAll();
        hand.DiscardAll(); // mao ja vazia, nao deveria disparar de novo

        Assert.That(hand.Cards, Is.Empty);
        Assert.That(fireCount, Is.EqualTo(1));
    }
}
