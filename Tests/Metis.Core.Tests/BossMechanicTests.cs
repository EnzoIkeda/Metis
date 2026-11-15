using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

// Regras especiais da segunda leva: cascata pela matriz, cartas extras no baralho e encarecimento.
public class BossMechanicTests
{
    private static FakeBossCard Card(string id, int cost, params (CityParameterType Parameter, float Amount)[] effects) =>
        new FakeBossCard { Id = id, MomentumCost = cost, Effects = effects.Select(effect => new StatModifier { Parameter = effect.Parameter, Amount = effect.Amount }).ToArray() };

    private static FakeBoss Boss(BossMechanic mechanic, float amount, string cardId = "", params FakeBossCard[] cards) =>
        new FakeBoss { MomentumPerTurn = 2, Mechanic = mechanic, MechanicAmount = amount, MechanicCardId = cardId, Cards = cards };

    private static float Amount(List<StatModifier> effects, CityParameterType parameter) =>
        effects.Where(effect => effect.Parameter == parameter).Sum(effect => effect.Amount);

    [Test]
    public void Cascade_EnergyDamageSpreadsByTheMatrixCoefficients()
    {
        var card = Card("A", 2, (CityParameterType.Energia, -10f));
        var effects = BossRules.ScaledEffects(Boss(BossMechanic.CascadeSpread, 0.5f, "", card), card, 1f);

        Assert.That(Amount(effects, CityParameterType.Energia), Is.EqualTo(-10f).Within(0.001f));
        foreach (var target in BossRules.CascadeTargets())
        {
            var expected = (float)Math.Round(-10f * InteractionMatrix.GetCoefficient(target, CityParameterType.Energia) * 0.5f, MidpointRounding.AwayFromZero);
            Assert.That(Amount(effects, target), Is.EqualTo(expected).Within(0.001f), target.ToString());
        }
        Assert.That(BossRules.CascadeTargets(), Is.EquivalentTo(new[] { CityParameterType.Renda, CityParameterType.Populacao, CityParameterType.Pesquisa, CityParameterType.Mobilidade }));
    }

    [Test]
    public void Cascade_AddsToDamageTheCardAlreadyDoes()
    {
        var card = Card("A", 2, (CityParameterType.Energia, -10f), (CityParameterType.Mobilidade, -2f));
        var effects = BossRules.ScaledEffects(Boss(BossMechanic.CascadeSpread, 1f, "", card), card, 1f);

        Assert.That(effects.Count(effect => effect.Parameter == CityParameterType.Mobilidade), Is.EqualTo(1));
        Assert.That(Amount(effects, CityParameterType.Mobilidade), Is.EqualTo(-2f - 10f * InteractionMatrix.GetCoefficient(CityParameterType.Mobilidade, CityParameterType.Energia)).Within(0.001f));
    }

    [Test]
    public void Cascade_RoundsToWholePointsAndDropsZeros()
    {
        var card = Card("A", 2, (CityParameterType.Energia, -2f));
        var effects = BossRules.ScaledEffects(Boss(BossMechanic.CascadeSpread, 0.5f, "", card), card, 1f);

        // -2 x 0,4 x 0,5 = -0,4 e menos ainda nos outros: nada se espalha.
        Assert.That(effects, Has.Count.EqualTo(1));
        Assert.That(effects.All(effect => effect.Amount == (float)Math.Round(effect.Amount)), Is.True);
    }

    [Test]
    public void Cascade_NeverTouchesWhatEnergyWeakensOrWellbeing()
    {
        var card = Card("A", 2, (CityParameterType.Energia, -10f));
        var effects = BossRules.ScaledEffects(Boss(BossMechanic.CascadeSpread, 1f, "", card), card, 1f);

        Assert.That(effects.Any(effect => effect.Parameter == CityParameterType.Sustentabilidade), Is.False);
        Assert.That(effects.Any(effect => effect.Parameter == CityParameterType.BemEstar), Is.False);
        Assert.That(effects.All(effect => effect.Amount < 0f), Is.True);
    }

    [Test]
    public void Cascade_PlayTurnAppliesTheSameEffectsTheHudShows()
    {
        var card = Card("A", 2, (CityParameterType.Energia, -10f));
        var encounter = new BossEncounter(Boss(BossMechanic.CascadeSpread, 0.5f, "", card), new Random(1));
        var shown = encounter.NextEffects();
        var stats = CityStatsTestFactory.Build();
        encounter.PlayTurn(stats);

        foreach (var effect in shown)
            Assert.That(stats.GetValue(effect.Parameter), Is.EqualTo(50f + effect.Amount).Within(0.001f), effect.Parameter.ToString());
    }

    [Test]
    public void OtherMechanics_DoNotSpread()
    {
        var card = Card("A", 2, (CityParameterType.Energia, -10f));
        Assert.That(BossRules.ScaledEffects(Boss(BossMechanic.GrowingDamage, 0.5f, "", card), card, 1f), Has.Count.EqualTo(1));
        Assert.That(BossRules.ScaledEffects(null, card, 1f), Has.Count.EqualTo(1));
    }

    [Test]
    public void CostIncrease_GrowsWithCardsPlayed()
    {
        var card = Card("A", 2, (CityParameterType.Renda, -3f));
        var encounter = new BossEncounter(Boss(BossMechanic.CostIncrease, 0.5f, "", card), new Random(1));
        var stats = CityStatsTestFactory.Build();
        var offsets = new List<int> { encounter.CardCostOffset };
        for (var i = 0; i < 4; i++)
        {
            encounter.PlayTurn(stats);
            offsets.Add(encounter.CardCostOffset);
        }

        Assert.That(offsets, Is.EqualTo(new[] { 0, 0, 1, 1, 2 }));
        Assert.That(BossRules.CardCostOffset(Boss(BossMechanic.GrowingDamage, 0.5f), 4), Is.EqualTo(0));
    }

    [Test]
    public void CostIncrease_HandChargesTheHigherCostAndTellsTheView()
    {
        var bossCard = Card("A", 2, (CityParameterType.Renda, -1f));
        var encounter = new BossEncounter(Boss(BossMechanic.CostIncrease, 1f, "", bossCard), new Random(1));
        var card = new FakeCard { Id = "X", Cost = 3f };
        var hand = new CardHand<FakeCard>(new[] { card }, new Random(1), finiteDeck: true) { CostOffset = 1 };
        var changes = 0;
        hand.OnHandChanged += () => changes++;
        var stats = CityStatsTestFactory.Build();

        var played = encounter.PlayTurn(stats);
        BossRules.ApplyToHand(encounter, played, hand, null, 1);

        Assert.That(hand.CostOffset, Is.EqualTo(2));
        Assert.That(hand.CostOf(card), Is.EqualTo(5f));
        Assert.That(changes, Is.EqualTo(1));

        hand.CostOffset = 2;
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void CostIncrease_CanMakeACardUnplayable()
    {
        var card = new FakeCard { Id = "X", Cost = 4f };
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            var renda = configs[CityParameterType.Renda];
            renda.InitialValue = 5f;
            configs[CityParameterType.Renda] = renda;
        });
        var hand = new CardHand<FakeCard>(new[] { card }, new Random(1), finiteDeck: true);
        hand.Draw(1, stats);

        Assert.That(hand.CanPlay(card, stats), Is.True);
        hand.CostOffset = 2;
        Assert.That(hand.CanPlay(card, stats), Is.False);
    }

    [Test]
    public void ExtraCards_EachPlayPutsCopiesInTheDrawPile()
    {
        var bossCard = Card("A", 2, (CityParameterType.Seguranca, -3f));
        var encounter = new BossEncounter(Boss(BossMechanic.ExtraCards, 2f, "Termos", bossCard), new Random(1));
        var cards = Enumerable.Range(0, 5).Select(i => new FakeCard { Id = "C" + i }).ToArray();
        var extra = new FakeCard { Id = "Termos", IsExtraOnly = true };
        var hand = new CardHand<FakeCard>(cards, new Random(3), finiteDeck: true);
        var stats = CityStatsTestFactory.Build();

        BossRules.ApplyToHand(encounter, encounter.PlayTurn(stats), hand, extra, 0);
        Assert.That(hand.CountInDeck(extra), Is.EqualTo(2));
        Assert.That(hand.DrawPile.Count, Is.EqualTo(7));

        BossRules.ApplyToHand(encounter, null, hand, extra, 0);
        Assert.That(hand.CountInDeck(extra), Is.EqualTo(2));
        Assert.That(hand.CostOffset, Is.EqualTo(0));
    }

    [Test]
    public void InsertIntoDrawPile_IsDrawnLikeAnyCardAndSurvivesTheSave()
    {
        var cards = Enumerable.Range(0, 4).Select(i => new FakeCard { Id = "C" + i }).ToArray();
        var extra = new FakeCard { Id = "Termos", IsExtraOnly = true };
        var hand = new CardHand<FakeCard>(cards, new Random(3), finiteDeck: true);
        hand.InsertIntoDrawPile(extra, 3);
        var stats = CityStatsTestFactory.Build();

        var restored = CardHand<FakeCard>.FromSavedState(hand.Cards, hand.DrawPile, hand.DiscardPile, false, false, new Random(1));
        Assert.That(restored.CountInDeck(extra), Is.EqualTo(3));

        hand.Draw(7, stats);
        Assert.That(hand.Cards.Count(card => card == extra), Is.EqualTo(3));
    }

    [Test]
    public void InsertIntoDrawPile_IgnoredWithoutFiniteDeck()
    {
        var hand = new CardHand<FakeCard>(new[] { new FakeCard() }, new Random(1));
        hand.InsertIntoDrawPile(new FakeCard { Id = "Termos" }, 2);

        Assert.That(hand.DrawPile, Is.Empty);
    }

    [Test]
    public void ExtraCardCount_OnlyForExtraCards()
    {
        Assert.That(BossRules.ExtraCardCount(Boss(BossMechanic.ExtraCards, 2f, "T")), Is.EqualTo(2));
        Assert.That(BossRules.ExtraCardCount(Boss(BossMechanic.CostIncrease, 2f)), Is.EqualTo(0));
    }

    [Test]
    public void SavedEncounter_KeepsTheCostIncrease()
    {
        var cards = Enumerable.Range(0, 6).Select(i => Card("C" + i, 2, (CityParameterType.Renda, -2f))).ToArray();
        var definition = Boss(BossMechanic.CostIncrease, 0.5f, "", cards);
        var random = new SeededRandom(7);
        var encounter = new BossEncounter(definition, random);
        var stats = CityStatsTestFactory.Build();
        encounter.PlayTurn(stats);
        encounter.PlayTurn(stats);

        var phase = new PhaseSaveData();
        RunSaveRules.CaptureBoss(phase, encounter, random, null);
        Assert.That(RunSaveRules.TryRestoreBoss(phase, definition, out var restored, out _), Is.True);
        Assert.That(restored.CardCostOffset, Is.EqualTo(encounter.CardCostOffset).And.EqualTo(1));
    }
}
