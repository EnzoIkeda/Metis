using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class RunSaveRulesTests
{
    private static List<FakeCard> Pool(int count)
    {
        return Enumerable.Range(0, count).Select(i => new FakeCard { Id = $"C{i}", Cost = 1f }).ToList();
    }

    private static Func<string, FakeCard> Finder(IEnumerable<FakeCard> pool)
    {
        var byId = pool.ToDictionary(card => card.Id);
        return id => byId.TryGetValue(id, out var card) ? card : null;
    }

    // Joga uns turnos pra mao, pilhas e geradores sairem do estado inicial.
    private static (CityStats stats, CardHand<FakeCard> hand, SeededRandom handRandom, SeededRandom eventRandom) PlayedState(List<FakeCard> pool)
    {
        var stats = CityStatsTestFactory.Build();
        var handRandom = new SeededRandom(11);
        var eventRandom = new SeededRandom(22);
        var hand = new CardHand<FakeCard>(pool, handRandom, finiteDeck: true);
        for (int turn = 0; turn < 3; turn++)
        {
            hand.Draw(4, stats);
            hand.TryPlay(hand.Cards[0], stats);
            hand.DiscardAll();
            eventRandom.Next(10);
        }
        hand.Draw(4, stats);
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.BemEstar, Amount = 3f });
        return (stats, hand, handRandom, eventRandom);
    }

    [Test]
    public void CaptureAndRestore_RebuildsStatsHandAndPilesExactly()
    {
        var pool = Pool(10);
        var (stats, hand, handRandom, eventRandom) = PlayedState(pool);
        hand.Reveal(untilPhaseEnd: true);

        var saved = RunSaveRules.CapturePhase(4, stats, hand, handRandom, eventRandom);

        var restoredStats = CityStatsTestFactory.Build();
        RunSaveRules.RestoreStats(saved, restoredStats);
        Assert.That(RunSaveRules.TryRestoreHand(saved, Finder(pool), new SeededRandom(saved.HandRandomState), out var restoredHand), Is.True);

        Assert.That(saved.TurnIndex, Is.EqualTo(4));
        foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
            Assert.That(restoredStats.GetValue(parameter), Is.EqualTo(stats.GetValue(parameter)), parameter.ToString());
        Assert.That(restoredStats.AnchorBonus, Is.EqualTo(stats.AnchorBonus));
        Assert.That(restoredHand.Cards, Is.EqualTo(hand.Cards));
        Assert.That(restoredHand.DrawPile, Is.EqualTo(hand.DrawPile), "a ordem da pilha de compra tem que ser a mesma");
        Assert.That(restoredHand.DiscardPile, Is.EqualTo(hand.DiscardPile));
        Assert.That(restoredHand.FiniteDeck, Is.True);
        Assert.That(restoredHand.IsRevealed, Is.True);
        Assert.That(restoredHand.RevealedUntilPhaseEnd, Is.True);
    }

    // Continuar a rodada nao pode mudar o que vem depois: as proximas compras saem iguais as da partida original.
    [Test]
    public void RestoredHand_DrawsTheSameCardsAsTheOriginal()
    {
        var pool = Pool(9);
        var (stats, hand, handRandom, eventRandom) = PlayedState(pool);
        var saved = RunSaveRules.CapturePhase(4, stats, hand, handRandom, eventRandom);
        RunSaveRules.TryRestoreHand(saved, Finder(pool), new SeededRandom(saved.HandRandomState), out var restoredHand);
        var restoredStats = CityStatsTestFactory.Build();
        RunSaveRules.RestoreStats(saved, restoredStats);

        for (int turn = 0; turn < 6; turn++)
        {
            hand.DiscardAll();
            restoredHand.DiscardAll();
            hand.Draw(4, stats);
            restoredHand.Draw(4, restoredStats);

            Assert.That(restoredHand.Cards.Select(card => card.Id), Is.EqualTo(hand.Cards.Select(card => card.Id)), $"turno {turn}");
        }
    }

    [Test]
    public void RestoredEventRandom_ContinuesTheSameSequence()
    {
        var pool = Pool(6);
        var (stats, hand, handRandom, eventRandom) = PlayedState(pool);
        var saved = RunSaveRules.CapturePhase(4, stats, hand, handRandom, eventRandom);
        var restored = new SeededRandom(saved.EventRandomState);

        for (int i = 0; i < 20; i++)
            Assert.That(restored.Next(30), Is.EqualTo(eventRandom.Next(30)));
    }

    [Test]
    public void TryRestoreHand_UnknownCardId_Fails()
    {
        var pool = Pool(6);
        var (stats, hand, handRandom, eventRandom) = PlayedState(pool);
        var saved = RunSaveRules.CapturePhase(4, stats, hand, handRandom, eventRandom);
        saved.DrawPile.Add("CartaQueNaoExisteMais");

        Assert.That(RunSaveRules.TryRestoreHand(saved, Finder(pool), new SeededRandom(1), out var restored), Is.False);
        Assert.That(restored, Is.Null);
    }

    [Test]
    public void RestoreStats_ClampsAndIgnoresUnknownParameters()
    {
        var stats = CityStatsTestFactory.Build();
        var saved = new PhaseSaveData();
        saved.Parameters.Add(new ParameterValueSave { Parameter = CityParameterType.Renda, Value = 250f });
        saved.Parameters.Add(new ParameterValueSave { Parameter = (CityParameterType)999, Value = 10f });

        RunSaveRules.RestoreStats(saved, stats);

        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(100f));
        Assert.That(stats.GetValue(CityParameterType.Saude), Is.EqualTo(CityStatsTestFactory.NeutralValue), "parametro nao salvo fica como estava");
    }

    // Bem-estar e recalculado na resolucao; o bonus restaurado tem que continuar somando por cima.
    [Test]
    public void RestoreStats_KeepsAnchorBonusThroughTheNextResolution()
    {
        var original = CityStatsTestFactory.Build();
        original.ApplyModifier(new StatModifier { Parameter = CityParameterType.BemEstar, Amount = 7f });
        var saved = RunSaveRules.CapturePhase(1, original, new CardHand<FakeCard>(Pool(1), new SeededRandom(1), finiteDeck: true), new SeededRandom(1), new SeededRandom(2));

        var restored = CityStatsTestFactory.Build();
        RunSaveRules.RestoreStats(saved, restored);
        original.ResolveTurn();
        restored.ResolveTurn();

        Assert.That(restored.GetValue(CityParameterType.BemEstar), Is.EqualTo(original.GetValue(CityParameterType.BemEstar)));
    }

    [Test]
    public void IsCompatible_RejectsOtherVersionsAndNull()
    {
        Assert.That(RunSaveRules.IsCompatible(new RunSaveData()), Is.True);
        Assert.That(RunSaveRules.IsCompatible(new RunSaveData { Version = RunSaveData.CurrentVersion + 1 }), Is.False);
        Assert.That(RunSaveRules.IsCompatible(new RunSaveData { Version = 0 }), Is.False);
        Assert.That(RunSaveRules.IsCompatible(null), Is.False);
    }
}
