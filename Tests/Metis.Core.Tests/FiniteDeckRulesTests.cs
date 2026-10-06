using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class FiniteDeckRulesTests
{
    [Test]
    public void DeckBuilder_AddsEachCardAsManyTimesAsItsCopies()
    {
        var single = new FakeCard { Id = "Uma" };
        var triple = new FakeCard { Id = "Tres", Copies = 3 };

        var deck = DeckBuilder.Build(new List<FakeCard> { single, triple }, CardArchetype.Geral, new List<string>());

        Assert.That(deck.Count(card => card == single), Is.EqualTo(1));
        Assert.That(deck.Count(card => card == triple), Is.EqualTo(3));
    }

    [Test]
    public void DeckBuilder_LoadedRewardComesWithItsCopies()
    {
        var reward = new FakeCard { Id = "Recompensa", Tier = CardTier.SmartCity, Archetype = CardArchetype.Industria, Copies = 2 };

        var deck = DeckBuilder.Build(new List<FakeCard> { reward }, CardArchetype.Automacao, new List<string> { "Recompensa" });

        Assert.That(deck, Has.Count.EqualTo(2));
    }

    [Test]
    public void FiniteDeck_WholeDeckIsDrawnBeforeAnyCardRepeats()
    {
        var a = new FakeCard { Id = "A", Copies = 2 };
        var b = new FakeCard { Id = "B", Copies = 3 };
        var deck = DeckBuilder.Build(new List<FakeCard> { a, b }, CardArchetype.Geral, new List<string>());
        var hand = new CardHand<FakeCard>(deck, new Random(4), finiteDeck: true);
        var stats = CityStatsTestFactory.Build();

        hand.Draw(5, stats);

        Assert.That(hand.Cards.Count(card => card == a), Is.EqualTo(2));
        Assert.That(hand.Cards.Count(card => card == b), Is.EqualTo(3));
    }

    [Test]
    public void FromPiles_KeepsTheKnownCompositionAndOnlyShufflesTheOrder()
    {
        var cards = Enumerable.Range(0, 6).Select(i => new FakeCard { Id = $"C{i}" }).ToList();
        var drawPile = cards.Take(4).ToList();
        var discard = cards.Skip(4).ToList();

        var hand = CardHand<FakeCard>.FromPiles(cards, drawPile, discard, new Random(1));

        Assert.That(hand.FiniteDeck, Is.True);
        Assert.That(hand.DrawPile, Is.EquivalentTo(drawPile));
        Assert.That(hand.DiscardPile, Is.EqualTo(discard));

        hand.Draw(4, CityStatsTestFactory.Build());
        Assert.That(hand.Cards, Is.EquivalentTo(drawPile), "as 4 primeiras compras so podem vir da pilha conhecida");
    }

    [Test]
    public void PresetRules_OverrideInitialValuesAndScaleOnlyNegativeDrifts()
    {
        var baseConfigs = new[]
        {
            new CityParameterConfig { Parameter = CityParameterType.Renda, InitialValue = 55f, Deriva = 1f },
            new CityParameterConfig { Parameter = CityParameterType.Energia, InitialValue = 50f, Deriva = -2f },
        };
        var overrides = new[] { new CityParameterOverride { Parameter = CityParameterType.Energia, InitialValue = 30f } };

        var configs = CityPresetRules.Apply(baseConfigs, overrides, 1.5f);

        Assert.That(configs[0].InitialValue, Is.EqualTo(55f));
        Assert.That(configs[0].Deriva, Is.EqualTo(1f));
        Assert.That(configs[1].InitialValue, Is.EqualTo(30f));
        Assert.That(configs[1].Deriva, Is.EqualTo(-3f));
        Assert.That(baseConfigs[1].Deriva, Is.EqualTo(-2f), "nao pode mexer na configuracao base");
    }
}
