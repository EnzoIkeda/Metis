using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

internal sealed class FakeBossCard : IBossCardDefinition
{
    public string Id { get; init; } = "Golpe";
    public int MomentumCost { get; init; } = 2;
    public IReadOnlyList<StatModifier> Effects { get; init; } = Array.Empty<StatModifier>();
}

internal sealed class FakeBoss : IBossDefinition
{
    public string Id { get; init; } = "Chefe";
    public CardArchetype TargetArchetype { get; init; } = CardArchetype.Industria;
    public int StartingMomentum { get; init; }
    public int MomentumPerTurn { get; init; } = 2;
    public BossMechanic Mechanic { get; init; }
    public float MechanicAmount { get; init; }
    public string MechanicCardId { get; init; } = "";
    public IReadOnlyList<IBossCardDefinition> Cards { get; init; } = Array.Empty<IBossCardDefinition>();
}

// Baralho, Impeto e regra especial do chefe, a fase dele no turno e o save.
public class BossTests
{
    private static FakeBossCard Card(string id, int cost, CityParameterType parameter = CityParameterType.Saude, float amount = -4f) =>
        new FakeBossCard { Id = id, MomentumCost = cost, Effects = new[] { new StatModifier { Parameter = parameter, Amount = amount } } };

    private static FakeBoss Boss(int perTurn = 2, BossMechanic mechanic = BossMechanic.None, float amount = 0f, params FakeBossCard[] cards) =>
        new FakeBoss { MomentumPerTurn = perTurn, Mechanic = mechanic, MechanicAmount = amount, Cards = cards };

    [Test]
    public void Deck_NextIsAlwaysKnownAndReshufflesTheDiscard()
    {
        var cards = new[] { Card("A", 1), Card("B", 1), Card("C", 1) };
        var deck = new BossDeck(cards, new Random(3));
        var seen = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            Assert.That(deck.Next, Is.Not.Null);
            seen.Add(deck.Next.Id);
            deck.Advance();
        }

        Assert.That(seen.Take(3), Is.EquivalentTo(new[] { "A", "B", "C" }));
        Assert.That(seen.Skip(3), Is.EquivalentTo(new[] { "A", "B", "C" }));
    }

    [Test]
    public void PlayTurn_WaitsUntilMomentumPaysTheNextCard()
    {
        var stats = CityStatsTestFactory.Build();
        var encounter = new BossEncounter(Boss(2, BossMechanic.None, 0f, Card("A", 5)), new Random(1));

        Assert.That(encounter.TurnsUntilNextPlay, Is.EqualTo(3));
        Assert.That(encounter.PlayTurn(stats), Is.Null);
        Assert.That(encounter.PlayTurn(stats), Is.Null);
        Assert.That(encounter.PlaysThisTurn, Is.True);
        var played = encounter.PlayTurn(stats);

        Assert.That(played?.Id, Is.EqualTo("A"));
        Assert.That(encounter.Momentum, Is.EqualTo(1));
        Assert.That(encounter.CardsPlayed, Is.EqualTo(1));
        Assert.That(stats.GetValue(CityParameterType.Saude), Is.EqualTo(46f).Within(0.001f));
    }

    [Test]
    public void GrowingDamage_EachPlayedCardRaisesTheNextOne()
    {
        var stats = CityStatsTestFactory.Build();
        var encounter = new BossEncounter(Boss(2, BossMechanic.GrowingDamage, 0.5f, Card("A", 2)), new Random(1));

        encounter.PlayTurn(stats);
        Assert.That(encounter.DamageFactor, Is.EqualTo(1.5f).Within(0.001f));
        encounter.PlayTurn(stats);

        // -4 e depois -6.
        Assert.That(stats.GetValue(CityParameterType.Saude), Is.EqualTo(40f).Within(0.001f));
        Assert.That(encounter.NextEffects()[0].Amount, Is.EqualTo(-8f).Within(0.001f));
    }

    [Test]
    public void NoMechanic_DamageNeverGrows()
    {
        var encounter = new BossEncounter(Boss(2, BossMechanic.None, 0.5f, Card("A", 2)), new Random(1));
        encounter.PlayTurn(CityStatsTestFactory.Build());

        Assert.That(encounter.DamageFactor, Is.EqualTo(1f));
    }

    [Test]
    public void Clone_KeepsNextAndStateButDoesNotTouchTheOriginal()
    {
        var cards = Enumerable.Range(0, 6).Select(i => Card("C" + i, 2)).ToArray();
        var original = new BossEncounter(Boss(2, BossMechanic.GrowingDamage, 0.1f, cards), new Random(5));
        original.PlayTurn(CityStatsTestFactory.Build());

        var clone = original.Clone(new Random(9));
        Assert.That(clone.Next.Id, Is.EqualTo(original.Next.Id));
        Assert.That(clone.Momentum, Is.EqualTo(original.Momentum));
        Assert.That(clone.CardsPlayed, Is.EqualTo(original.CardsPlayed));
        Assert.That(clone.Deck.DrawPile.Select(card => card.Id), Is.EquivalentTo(original.Deck.DrawPile.Select(card => card.Id)));

        var next = original.Next.Id;
        clone.PlayTurn(CityStatsTestFactory.Build());
        Assert.That(original.Next.Id, Is.EqualTo(next));
        Assert.That(original.CardsPlayed, Is.EqualTo(1));
    }

    [Test]
    public void ForArchetype_FindsTheBossThatTargetsTheDeck()
    {
        var bosses = new[] { new FakeBoss { Id = "X", TargetArchetype = CardArchetype.Industria }, new FakeBoss { Id = "Y", TargetArchetype = CardArchetype.Automacao } };

        Assert.That(BossRules.ForArchetype(bosses, CardArchetype.Automacao)?.Id, Is.EqualTo("Y"));
        Assert.That(BossRules.ForArchetype(bosses, CardArchetype.Comunitario), Is.Null);
        Assert.That(BossRules.Find(bosses, "X")?.Id, Is.EqualTo("X"));
    }

    [Test]
    public void Machine_WithBoss_StopsAtTheBossBeforeResolving()
    {
        var stats = CityStatsTestFactory.Build();
        var machine = new TurnMachine(stats, hasBoss: true);
        var phases = new List<TurnPhase>();
        machine.OnPhaseChanged += phase => phases.Add(phase);
        machine.StartGame();

        machine.EndActionPhase();
        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Boss));
        machine.AcknowledgeEvent();
        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Boss));

        machine.AcknowledgeBoss();
        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Event));
        Assert.That(phases, Is.EqualTo(new[] { TurnPhase.StartOfTurn, TurnPhase.Action, TurnPhase.Boss, TurnPhase.Resolution, TurnPhase.Event }));
    }

    [Test]
    public void Machine_WithoutBoss_NeverEntersTheBossPhase()
    {
        var machine = new TurnMachine(CityStatsTestFactory.Build());
        var phases = new List<TurnPhase>();
        machine.OnPhaseChanged += phase => phases.Add(phase);
        machine.StartGame();

        machine.EndActionPhase();
        machine.AcknowledgeBoss();

        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Event));
        Assert.That(phases, Does.Not.Contain(TurnPhase.Boss));
    }

    [Test]
    public void Machine_ResumeAtBoss_ContinuesToResolution()
    {
        var machine = new TurnMachine(CityStatsTestFactory.Build(), hasBoss: true);
        machine.ResumeAtBoss(7);

        Assert.That(machine.TurnIndex, Is.EqualTo(7));
        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Boss));
        machine.AcknowledgeBoss();
        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Event));
    }

    [Test]
    public void Save_CaptureAndRestore_KeepsDeckMomentumAndPendingCard()
    {
        var cards = Enumerable.Range(0, 6).Select(i => Card("C" + i, 3)).ToArray();
        var definition = Boss(2, BossMechanic.GrowingDamage, 0.1f, cards);
        var random = new SeededRandom(42);
        var original = new BossEncounter(definition, random);
        var stats = CityStatsTestFactory.Build();
        original.PlayTurn(stats);
        var played = original.PlayTurn(stats);

        var phase = new PhaseSaveData();
        RunSaveRules.CaptureBoss(phase, original, random, played.Id);
        Assert.That(RunSaveRules.TryRestoreBoss(phase, definition, out var restored, out var restoredRandom), Is.True);

        Assert.That(phase.AtBoss, Is.True);
        Assert.That(phase.PendingBossCardId, Is.EqualTo(played.Id));
        Assert.That(restored.Next.Id, Is.EqualTo(original.Next.Id));
        Assert.That(restored.Momentum, Is.EqualTo(original.Momentum));
        Assert.That(restored.CardsPlayed, Is.EqualTo(original.CardsPlayed));
        Assert.That(restored.Deck.DrawPile.Select(card => card.Id), Is.EqualTo(original.Deck.DrawPile.Select(card => card.Id)));
        Assert.That(restored.Deck.DiscardPile.Select(card => card.Id), Is.EqualTo(original.Deck.DiscardPile.Select(card => card.Id)));
        Assert.That(restoredRandom.State, Is.EqualTo(random.State));
    }

    [Test]
    public void Save_UnknownBossCard_FailsToRestore()
    {
        var definition = Boss(2, BossMechanic.None, 0f, Card("A", 2), Card("B", 2));
        var phase = new PhaseSaveData { HasBoss = true, BossNext = "Sumiu" };

        Assert.That(RunSaveRules.TryRestoreBoss(phase, definition, out _, out _), Is.False);
    }

    [Test]
    public void Save_PhaseWithoutBoss_LeavesTheFlagOff()
    {
        var phase = new PhaseSaveData();
        RunSaveRules.CaptureBoss(phase, null, null, null);

        Assert.That(phase.HasBoss, Is.False);
        Assert.That(RunSaveRules.TryRestoreBoss(phase, Boss(), out _, out _), Is.False);
    }

    [Test]
    public void Map_WithBoss_OffersOnlyTheBoss()
    {
        var saved = new[] { new PhaseMapNode { Id = PhaseMapProvider.UnknownCityId, Type = PhaseMapNodeType.Unknown, IsAvailable = true } };
        var offer = PhaseMapProvider.ResolveOffer(saved, new[] { "TemaA" }, new List<string>(), new Random(1), "Chefe");

        Assert.That(offer.Count, Is.EqualTo(1));
        Assert.That(offer[0].Type, Is.EqualTo(PhaseMapNodeType.Boss));
        Assert.That(offer[0].Id, Is.EqualTo("Chefe"));
    }

    [Test]
    public void Map_SavedBossNodeIsDroppedOnANormalStop()
    {
        var saved = PhaseMapProvider.BossOffer("Chefe");
        var offer = PhaseMapProvider.ResolveOffer(saved, new string[0], new List<string>(), new Random(1));

        Assert.That(offer.Single().Type, Is.EqualTo(PhaseMapNodeType.Unknown));
    }

    [Test]
    public void ApplyMapChoice_BossBecomesCurrentAndClearsTheTheme()
    {
        var save = new RunSaveData();
        save.Meta.CurrentTheme = "TemaA";
        RunSaveRules.ApplyMapChoice(save, PhaseMapProvider.BossOffer("Chefe")[0]);

        Assert.That(save.Meta.CurrentBoss, Is.EqualTo("Chefe"));
        Assert.That(save.Meta.CurrentTheme, Is.Empty);

        RunSaveRules.ApplyMapChoice(save, new PhaseMapNode { Id = PhaseMapProvider.UnknownCityId, Type = PhaseMapNodeType.Unknown });
        Assert.That(save.Meta.CurrentBoss, Is.Empty);
    }

    [Test]
    public void History_SeparatesCardEffectFromBossEffect()
    {
        var stats = CityStatsTestFactory.Build();
        var history = new PhaseHistory();
        history.RecordPlay(1, "Carta", stats, null);
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Saude, Amount = 3f });
        history.RecordBoss(1, "Golpe", PhaseHistory.Snapshot(stats));
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Saude, Amount = -5f });
        history.RecordResolutionStart(1, stats);
        history.FinishTurn(1, stats);

        var turn = history.Turns[0];
        Assert.That(turn.GetCardEffect(CityParameterType.Saude), Is.EqualTo(3f).Within(0.001f));
        Assert.That(turn.GetBossEffect(CityParameterType.Saude), Is.EqualTo(-5f).Within(0.001f));
        Assert.That(turn.BossPlayedCard, Is.True);
    }

    [Test]
    public void Tutorial_BossTipComesAfterTheBasics()
    {
        var situation = new TutorialSituation { IsActionPhase = true, Turn = 1, HasBoss = true };

        Assert.That(TutorialRules.Next(situation, tip => tip != TutorialTip.Boss), Is.EqualTo(TutorialTip.Boss));
        Assert.That(TutorialRules.Next(situation, tip => tip == TutorialTip.Parameters), Is.EqualTo(TutorialTip.Cards));
        situation.HasBoss = false;
        Assert.That(TutorialRules.Next(situation, tip => tip != TutorialTip.Boss), Is.EqualTo(TutorialTip.None));
    }

    [Test]
    public void News_BossCardBecomesASignalAboveTheEvent()
    {
        var values = Enumerable.Repeat(50f, Enum.GetValues(typeof(CityParameterType)).Length).ToArray();
        var configs = ((CityParameterType[])Enum.GetValues(typeof(CityParameterType))).Select(parameter => new CityParameterConfig
        {
            Parameter = parameter,
            MaxValue = 100f,
            CriticalLevel = 10f,
        });
        var rules = new NewsRules(configs, 25f, 75f, new[] { 15f, 30f });
        var signals = NewsEditor.BuildSignals(new[] { values, values }, rules, null, "Carta", "Evento", null, "Golpe");

        Assert.That(signals[0].Trigger, Is.EqualTo(NewsTrigger.BossCardPlayed));
        Assert.That(signals[0].TargetId, Is.EqualTo("Golpe"));
        Assert.That(NewsRules.UsesTarget(NewsTrigger.BossCardPlayed), Is.True);
    }
}
