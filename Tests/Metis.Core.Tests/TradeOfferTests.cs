using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

internal sealed class FakeTrade : ITradeOfferDefinition
{
    public string Id { get; init; } = "Troca";
    public TradeTerm Benefit { get; init; }
    public TradeTerm Drawback { get; init; }
}

// Regras das trocas do mapa, o save da oferta e a mao com custo ajustado.
public class TradeOfferTests
{
    private static TradeTerm Effect(CityParameterType parameter, float amount) =>
        new TradeTerm { Kind = TradeTermKind.PhaseStartEffect, Effects = new[] { new StatModifier { Parameter = parameter, Amount = amount } } };

    private static TradeTerm Cost(int amount) => new TradeTerm { Kind = TradeTermKind.CardCostOffset, Amount = amount };

    private static TradeTerm Rewards(int amount) => new TradeTerm { Kind = TradeTermKind.RewardOptionOffset, Amount = amount };

    private static TradeTerm Extra(string card) => new TradeTerm { Kind = TradeTermKind.ExtraCard, CardId = card };

    [Test]
    public void ApplyPhaseStart_AppliesEffectsFromBothSides()
    {
        var stats = CityStatsTestFactory.Build();
        var trade = new FakeTrade { Benefit = Effect(CityParameterType.Renda, 15f), Drawback = Effect(CityParameterType.Saude, -4f) };

        TradeOfferRules.ApplyPhaseStart(new[] { trade }, stats);

        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(65f).Within(0.001f));
        Assert.That(stats.GetValue(CityParameterType.Saude), Is.EqualTo(46f).Within(0.001f));
    }

    [Test]
    public void Offsets_AddUpAcrossAcceptedTrades()
    {
        var trades = new ITradeOfferDefinition[]
        {
            new FakeTrade { Id = "A", Benefit = Effect(CityParameterType.Renda, 15f), Drawback = Cost(1) },
            new FakeTrade { Id = "B", Benefit = Cost(-1), Drawback = Rewards(-1) },
            new FakeTrade { Id = "C", Benefit = Effect(CityParameterType.Pesquisa, 6f), Drawback = Extra("Lixo") },
        };

        Assert.That(TradeOfferRules.CardCostOffset(trades), Is.EqualTo(0));
        Assert.That(TradeOfferRules.RewardOptionCount(3, trades), Is.EqualTo(2));
        Assert.That(TradeOfferRules.ExtraCardIds(trades), Is.EqualTo(new[] { "Lixo" }));
    }

    [Test]
    public void RewardOptionCount_NeverDropsBelowOne()
    {
        var trades = new[] { new FakeTrade { Benefit = Cost(-1), Drawback = Rewards(-5) } };
        Assert.That(TradeOfferRules.RewardOptionCount(3, trades), Is.EqualTo(1));
    }

    [Test]
    public void EffectiveCost_IsNeverNegative()
    {
        Assert.That(TradeOfferRules.EffectiveCost(5f, 1), Is.EqualTo(6f));
        Assert.That(TradeOfferRules.EffectiveCost(0f, -1), Is.EqualTo(0f));
    }

    [Test]
    public void Draw_SkipsAlreadyOfferedAndReturnsNullWhenNoneLeft()
    {
        var offers = new List<FakeTrade> { new FakeTrade { Id = "A" }, new FakeTrade { Id = "B" } };

        Assert.That(TradeOfferRules.Draw(offers, new[] { "A" }, new Random(1)).Id, Is.EqualTo("B"));
        Assert.That(TradeOfferRules.Draw(offers, new[] { "A", "B" }, new Random(1)), Is.Null);
    }

    [Test]
    public void Resolve_KeepsAcceptedOrderAndDropsUnknownIds()
    {
        var offers = new List<FakeTrade> { new FakeTrade { Id = "A" }, new FakeTrade { Id = "B" } };

        var accepted = TradeOfferRules.Resolve(offers, new[] { "B", "Sumiu", "A", "B" });

        Assert.That(accepted.Select(offer => offer.Id), Is.EqualTo(new[] { "B", "A" }));
    }

    [Test]
    public void CardHand_ChargesTheAdjustedCost()
    {
        var card = new FakeCard { Id = "Obra", Cost = 5f };
        var hand = new CardHand<FakeCard>(new List<FakeCard> { card }, new Random(1)) { CostOffset = 1 };
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            var renda = configs[CityParameterType.Renda];
            renda.InitialValue = 5.5f;
            configs[CityParameterType.Renda] = renda;
        });
        hand.Draw(1, stats);

        Assert.That(hand.CostOf(card), Is.EqualTo(6f));
        Assert.That(hand.CanPlay(card, stats), Is.False, "5,5 de Renda nao paga a carta que agora custa 6");

        hand.CostOffset = -1;
        Assert.That(hand.TryPlay(card, stats), Is.True);
        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(1.5f).Within(0.001f));
    }

    [Test]
    public void GreedyLookahead_UsesTheSameAdjustedCost()
    {
        var card = new FakeCard { Id = "Obra", Cost = 5f };
        var withOffset = CityStatsTestFactory.Build();
        var plain = CityStatsTestFactory.Build();

        GreedyLookahead.ApplyCard(withOffset, card, costOffset: 2);
        GreedyLookahead.ApplyCard(plain, card);

        Assert.That(plain.GetValue(CityParameterType.Renda) - withOffset.GetValue(CityParameterType.Renda), Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void DeckBuilder_LeavesExtraOnlyCardsOutUnlessATradeAddsThem()
    {
        var basica = new FakeCard { Id = "Basica", Tier = CardTier.Basica, Archetype = CardArchetype.Geral };
        var lixo = new FakeCard { Id = "Lixo", Tier = CardTier.Basica, Archetype = CardArchetype.Geral, IsExtraOnly = true };
        var all = new List<FakeCard> { basica, lixo };

        Assert.That(DeckBuilder.Build(all, CardArchetype.Automacao, new List<string> { "Lixo" }), Is.EqualTo(new[] { basica }));
        Assert.That(DeckBuilder.Build(all, CardArchetype.Automacao, new List<string>(), new[] { "Lixo" }), Is.EqualTo(new[] { basica, lixo }));
    }

    [Test]
    public void RewardPool_NeverOffersExtraOnlyCards()
    {
        var lixo = new FakeCard { Id = "Lixo", IsExtraOnly = true };
        var outra = new FakeCard { Id = "Outra", Archetype = CardArchetype.Industria, Tier = CardTier.SmartCity };

        Assert.That(RewardOptionPicker.CardPool(new List<FakeCard> { lixo, outra }, new List<FakeCard>()), Is.EqualTo(new[] { outra }));
    }

    [Test]
    public void Save_KeepsTheSameOfferUntilAnswered_ThenClearsItAtTheNextStop()
    {
        var offers = new List<FakeTrade> { new FakeTrade { Id = "A" }, new FakeTrade { Id = "B" } };
        var save = new RunSaveData();

        var first = RunSaveRules.ResolveTradeOffer(save, offers, new Random(3));
        var again = RunSaveRules.ResolveTradeOffer(save, offers, new Random(99));
        Assert.That(again, Is.SameAs(first), "sair e voltar ao mapa mostra a mesma oferta");
        Assert.That(save.Meta.OfferedTrades, Is.EqualTo(new[] { first.Id }));

        RunSaveRules.ApplyTradeDecision(save, first.Id, accepted: true);
        Assert.That(RunSaveRules.ResolveTradeOffer(save, offers, new Random(3)), Is.Null, "respondida, nao volta");
        Assert.That(save.Meta.AcceptedTrades, Is.EqualTo(new[] { first.Id }));
        Assert.That(save.Meta.PendingTradeNews, Is.EqualTo(first.Id));

        RunSaveRules.ApplyMapChoice(save, new PhaseMapNode { Id = PhaseMapProvider.UnknownCityId, Type = PhaseMapNodeType.Unknown, IsAvailable = true });
        var next = RunSaveRules.ResolveTradeOffer(save, offers, new Random(3));
        Assert.That(next, Is.Not.Null);
        Assert.That(next.Id, Is.Not.EqualTo(first.Id), "a mesma oferta nao sai duas vezes na rodada");
    }

    [Test]
    public void Save_DeclinedOfferIsNotAccepted_AndRunsOutQuietly()
    {
        var offers = new List<FakeTrade> { new FakeTrade { Id = "A" } };
        var save = new RunSaveData();

        var offer = RunSaveRules.ResolveTradeOffer(save, offers, new Random(1));
        RunSaveRules.ApplyTradeDecision(save, offer.Id, accepted: false);
        Assert.That(save.Meta.AcceptedTrades, Is.Empty);
        Assert.That(save.Meta.PendingTradeNews, Is.Empty);

        RunSaveRules.ApplyMapChoice(save, new PhaseMapNode { Id = PhaseMapProvider.UnknownCityId });
        Assert.That(RunSaveRules.ResolveTradeOffer(save, offers, new Random(1)), Is.Null);
        Assert.That(save.TradeOfferDecided, Is.True);
    }
}
