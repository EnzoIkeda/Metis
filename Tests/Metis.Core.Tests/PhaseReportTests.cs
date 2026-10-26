using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class PhaseReportTests
{
    private const float Tolerance = 0.001f;

    private static readonly CityParameterType[] Parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));

    // Critico 10 em todos menos Pesquisa, sem matriz, pra conta do Colapso ficar facil de seguir.
    private static CityStats BuildStats(Action<Dictionary<CityParameterType, CityParameterConfig>> customize = null, InteractionMatrix interactions = null)
    {
        return CityStatsTestFactory.Build(interactions, colapsoPenaltyPerParameter: 12f, customize: configs =>
        {
            foreach (var parameter in Parameters)
            {
                var config = configs[parameter];
                config.CriticalLevel = parameter == CityParameterType.Pesquisa ? -1f : 10f;
                configs[parameter] = config;
            }
            customize?.Invoke(configs);
        });
    }

    private static void SetInitial(Dictionary<CityParameterType, CityParameterConfig> configs, CityParameterType parameter, float value)
    {
        var config = configs[parameter];
        config.InitialValue = value;
        configs[parameter] = config;
    }

    private static ScoreRules Rules(CityStats stats, float pesquisaCap = 0f)
    {
        var configs = Parameters.Select(parameter => new CityParameterConfig
        {
            Parameter = parameter,
            CriticalLevel = parameter == CityParameterType.Pesquisa ? -1f : 10f,
        }).ToList();
        return new ScoreRules(configs, pesquisaCap);
    }

    private static FakeCard Card(string id, params (CityParameterType Parameter, float Amount)[] effects)
    {
        return new FakeCard
        {
            Id = id,
            StatEffects = effects.Select(effect => new StatModifier { Parameter = effect.Parameter, Amount = effect.Amount }).ToArray(),
        };
    }

    // Um turno na ordem do jogo: jogada, resolucao, evento e fim.
    private static void PlayTurn(PhaseHistory history, CityStats stats, int turn, FakeCard card, IEnumerable<FakeCard> playable = null, StatModifier[] evento = null)
    {
        history.BeginTurn(turn);
        history.RecordPlay(turn, card != null ? card.Id : string.Empty, stats, playable?.Select(candidate => candidate.Id));
        if (card != null)
            GreedyLookahead.ApplyCard(stats, card);
        history.RecordResolutionStart(turn, stats);
        stats.ResolveTurn();
        if (evento != null)
            stats.ApplyModifiers(evento);
        history.RecordEvent(turn, evento != null ? "Evento" : string.Empty);
        history.FinishTurn(turn, stats);
    }

    private static Func<string, ICardDefinition> Finder(params FakeCard[] cards)
    {
        var byId = cards.ToDictionary(card => card.Id);
        return id => byId.TryGetValue(id, out var card) ? card : null;
    }

    [Test]
    public void Recording_DoesNotChangeAnyNumber()
    {
        var random = new Random(5);
        var interactions = new InteractionMatrix(50f, 1f, 25f, 75f, 1.6f, 0.5f, 0.6f, 0.1f);
        var recorded = BuildStats(interactions: interactions);
        var plain = BuildStats(interactions: interactions);
        var history = new PhaseHistory();
        history.CaptureStart(recorded);

        for (var turn = 1; turn <= 20; turn++)
        {
            var card = Card("C" + turn, (Parameters[random.Next(Parameters.Length)], (float)(random.NextDouble() * 30 - 15)));
            PlayTurn(history, recorded, turn, card, new[] { card });
            GreedyLookahead.ApplyCard(plain, card);
            plain.ResolveTurn();
        }

        foreach (var parameter in Parameters)
            Assert.That(recorded.GetValue(parameter), Is.EqualTo(plain.GetValue(parameter)), parameter.ToString());
        Assert.That(recorded.AnchorBonus, Is.EqualTo(plain.AnchorBonus));
    }

    [Test]
    public void Turn_RecordsCardEffectWithCostAndTheEndOfTurn()
    {
        var stats = BuildStats();
        var history = new PhaseHistory();
        history.CaptureStart(stats);
        var card = new FakeCard { Id = "Ciclovia", Cost = 4f, StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Mobilidade, Amount = 6f } } };

        PlayTurn(history, stats, 1, card, new[] { card }, new[] { new StatModifier { Parameter = CityParameterType.Saude, Amount = -3f } });

        var turn = history.Turns.Single();
        Assert.That(turn.CardId, Is.EqualTo("Ciclovia"));
        Assert.That(turn.EventId, Is.EqualTo("Evento"));
        Assert.That(turn.GetCardEffect(CityParameterType.Mobilidade), Is.EqualTo(6f).Within(Tolerance));
        Assert.That(turn.GetCardEffect(CityParameterType.Renda), Is.EqualTo(-4f).Within(Tolerance));
        Assert.That(turn.GetEnd(CityParameterType.Saude), Is.EqualTo(47f).Within(Tolerance));
        Assert.That(history.Current, Is.Null);
    }

    [Test]
    public void Series_StartsAtTurnZeroAndFollowsEachTurn()
    {
        var stats = BuildStats();
        var history = new PhaseHistory();
        history.CaptureStart(stats);
        var card = Card("Saude", (CityParameterType.Saude, 5f));
        PlayTurn(history, stats, 1, card);
        PlayTurn(history, stats, 2, card);

        var series = PhaseReport.Series(history, CityParameterType.Saude);

        Assert.That(series.Select(point => point.Turn), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(series.Select(point => point.Value), Is.EqualTo(new[] { 50f, 55f, 60f }));
    }

    // Continuar a rodada no meio do turno chama o comeco de novo; o que ja estava registrado fica.
    [Test]
    public void BeginTurn_SameTurnAgain_KeepsWhatWasRecorded()
    {
        var history = new PhaseHistory();
        history.RecordFreeAction(3, "Busca");

        history.BeginTurn(3);

        Assert.That(history.Current.FreeActionIds, Is.EqualTo(new[] { "Busca" }));
    }

    [Test]
    public void CollapseChain_CountsEntryTurnsAndThePenaltyOnWellbeing()
    {
        var stats = BuildStats(configs => SetInitial(configs, CityParameterType.Energia, 12f));
        var history = new PhaseHistory();
        history.CaptureStart(stats);
        var cut = Card("Corte", (CityParameterType.Energia, -5f));
        var idle = Card("Nada");

        PlayTurn(history, stats, 1, idle);
        PlayTurn(history, stats, 2, cut);
        PlayTurn(history, stats, 3, idle, evento: new[] { new StatModifier { Parameter = CityParameterType.Saude, Amount = -45f } });

        var chain = PhaseReport.CollapseChain(history, Rules(stats));

        Assert.That(chain.Select(span => span.Parameter), Is.EqualTo(new[] { CityParameterType.Energia, CityParameterType.Saude }));
        Assert.That(chain[0].FirstTurn, Is.EqualTo(2));
        Assert.That(chain[0].TurnsPenalized, Is.EqualTo(2));
        Assert.That(chain[0].WellbeingPenalty, Is.EqualTo(24f).Within(Tolerance));
        Assert.That(chain[1].FirstTurn, Is.EqualTo(3), "entrou no fim do ultimo turno, ainda sem cobrar");
        Assert.That(chain[1].TurnsPenalized, Is.EqualTo(0));
    }

    [Test]
    public void CollapseChain_NoCollapse_IsEmpty()
    {
        var stats = BuildStats();
        var history = new PhaseHistory();
        PlayTurn(history, stats, 1, Card("Nada"));

        Assert.That(PhaseReport.CollapseChain(history, Rules(stats)), Is.Empty);
    }

    // Pesquisa sem limiar a alcancar nao vale nada na nota, mas o efeito direto ainda a conta.
    [Test]
    public void VersusPass_SeesThroughFaceValue_DirectEffectDoesNot()
    {
        var stats = BuildStats(configs => SetInitial(configs, CityParameterType.Saude, 20f));
        var history = new PhaseHistory();
        history.CaptureStart(stats);
        var flashy = Card("Vistosa", (CityParameterType.Pesquisa, 20f));
        var useful = Card("Util", (CityParameterType.Saude, 4f));
        var evaluator = new TurnEvaluator(stats.Clone(), Rules(stats), Finder(flashy, useful));

        PlayTurn(history, stats, 1, flashy, new[] { flashy, useful });
        PlayTurn(history, stats, 2, useful, new[] { flashy, useful });

        var byVersus = PhaseReport.TopDecisions(history, 1, ImpactCriterion.VersusPass, evaluator);
        var byDirect = PhaseReport.TopDecisions(history, 1, ImpactCriterion.DirectEffect, evaluator);

        Assert.That(byVersus.Single().CardId, Is.EqualTo("Util"));
        Assert.That(byDirect.Single().CardId, Is.EqualTo("Vistosa"));
        Assert.That(PhaseReport.Impact(history.Turns[0], ImpactCriterion.VersusPass, evaluator), Is.EqualTo(0.0).Within(1e-6));
    }

    [Test]
    public void TopDecisions_RanksByMagnitudeAndSkipsPassedTurns()
    {
        var stats = BuildStats(configs =>
        {
            SetInitial(configs, CityParameterType.Saude, 20f);
            SetInitial(configs, CityParameterType.Mobilidade, 20f);
        });
        var history = new PhaseHistory();
        var small = Card("Pequena", (CityParameterType.Saude, 1f));
        var harmful = Card("Ruim", (CityParameterType.Mobilidade, -8f));
        var evaluator = new TurnEvaluator(stats.Clone(), Rules(stats), Finder(small, harmful));

        PlayTurn(history, stats, 1, small);
        PlayTurn(history, stats, 2, null);
        PlayTurn(history, stats, 3, harmful);

        var top = PhaseReport.TopDecisions(history, 3, ImpactCriterion.VersusPass, evaluator);

        Assert.That(top.Select(item => item.TurnIndex), Is.EqualTo(new[] { 3, 1 }));
        Assert.That(top[0].Impact, Is.LessThan(0.0));
    }

    [Test]
    public void BestAlternative_PointsToTheBetterCardOnlyWhenTheGainIsClear()
    {
        var stats = BuildStats(configs => SetInitial(configs, CityParameterType.Saude, 20f));
        var history = new PhaseHistory();
        var flashy = Card("Vistosa", (CityParameterType.Pesquisa, 20f));
        var useful = Card("Util", (CityParameterType.Saude, 4f));
        var evaluator = new TurnEvaluator(stats.Clone(), Rules(stats), Finder(flashy, useful));

        PlayTurn(history, stats, 1, flashy, new[] { flashy, useful });
        PlayTurn(history, stats, 2, useful, new[] { flashy, useful });

        var miss = PhaseReport.BestAlternative(history.Turns[0], evaluator);
        Assert.That(miss.HasValue, Is.True);
        Assert.That(miss.Value.BetterCardId, Is.EqualTo("Util"));
        Assert.That(miss.Value.AvoidsGameOver, Is.False);
        Assert.That(PhaseReport.BestAlternative(history.Turns[1], evaluator).HasValue, Is.False);
        Assert.That(PhaseReport.BiggestMiss(history, evaluator).Value.TurnIndex, Is.EqualTo(1));
    }

    [Test]
    public void BestAlternative_MarksWhenTheOtherCardAvoidedTheGameOver()
    {
        var stats = BuildStats(configs =>
        {
            foreach (var parameter in new[] { CityParameterType.Saude, CityParameterType.Mobilidade, CityParameterType.Seguranca, CityParameterType.Sustentabilidade })
            {
                SetInitial(configs, parameter, 15f);
                var config = configs[parameter];
                config.CriticalLevel = -1f;
                configs[parameter] = config;
            }
        });
        var history = new PhaseHistory();
        var ruin = Card("Ruina", (CityParameterType.Saude, -15f), (CityParameterType.Mobilidade, -15f));
        var safe = Card("Segura", (CityParameterType.Saude, 1f));
        var evaluator = new TurnEvaluator(stats.Clone(), Rules(stats), Finder(ruin, safe));

        PlayTurn(history, stats, 1, ruin, new[] { ruin, safe });

        var miss = PhaseReport.BestAlternative(history.Turns[0], evaluator);
        Assert.That(stats.IsAnchorCritical(), Is.True);
        Assert.That(miss.Value.BetterCardId, Is.EqualTo("Segura"));
        Assert.That(miss.Value.AvoidsGameOver, Is.True);
    }

    [Test]
    public void BestAlternative_PassedTurnOrUnknownCard_HasNone()
    {
        var stats = BuildStats();
        var history = new PhaseHistory();
        var known = Card("Conhecida", (CityParameterType.Saude, 1f));
        var evaluator = new TurnEvaluator(stats.Clone(), Rules(stats), Finder(known));

        PlayTurn(history, stats, 1, null);
        PlayTurn(history, stats, 2, Card("Removida", (CityParameterType.Saude, 1f)), new[] { known });

        Assert.That(PhaseReport.BestAlternative(history.Turns[0], evaluator).HasValue, Is.False);
        Assert.That(PhaseReport.BestAlternative(history.Turns[1], evaluator).HasValue, Is.False);
    }

    [Test]
    public void Evaluator_RebuildsTheStateBeforeThePlay()
    {
        var stats = BuildStats();
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.BemEstar, Amount = 4f });
        var history = new PhaseHistory();
        var card = Card("Saude", (CityParameterType.Saude, 9f));
        history.RecordPlay(1, card.Id, stats, new[] { card.Id });
        var expected = PhaseHistory.Snapshot(stats);
        GreedyLookahead.ApplyCard(stats, card);

        var evaluator = new TurnEvaluator(stats.Clone(), Rules(stats), Finder(card));
        var rebuilt = evaluator.StateBeforePlay(history.Current);

        Assert.That(PhaseHistory.Snapshot(rebuilt), Is.EqualTo(expected));
        Assert.That(rebuilt.AnchorBonus, Is.EqualTo(4f));
    }

    [Test]
    public void SaveRoundTrip_KeepsClosedTurnsAndTheTurnInProgress()
    {
        var stats = BuildStats();
        var history = new PhaseHistory();
        history.CaptureStart(stats);
        PlayTurn(history, stats, 1, Card("A", (CityParameterType.Saude, 2f)));
        history.RecordPlay(2, "B", stats, new[] { "B", "C" });
        var phase = new PhaseSaveData();

        RunSaveRules.CaptureHistory(phase, history);
        var restored = RunSaveRules.RestoreHistory(phase);

        Assert.That(restored.StartValues, Is.EqualTo(history.StartValues));
        Assert.That(restored.Turns.Single().CardId, Is.EqualTo("A"));
        Assert.That(restored.Current.CardId, Is.EqualTo("B"));
        Assert.That(restored.Current.PlayableCardIds, Is.EqualTo(new[] { "B", "C" }));
    }

    [Test]
    public void RestoreHistory_SaveWithoutHistory_StartsEmpty()
    {
        var restored = RunSaveRules.RestoreHistory(new PhaseSaveData { HistoryStart = null, History = null });

        Assert.That(restored.Turns, Is.Empty);
        Assert.That(restored.Current, Is.Null);
        Assert.That(PhaseReport.Series(restored, CityParameterType.Saude), Is.Empty);
    }

    [Test]
    public void ScoreRules_FromCards_UsesTheHighestResearchThreshold()
    {
        var cards = new[] { new FakeCard { RequiredPesquisa = 15f }, new FakeCard { RequiredPesquisa = 45f }, new FakeCard() };

        Assert.That(ScoreRules.FromCards(Array.Empty<CityParameterConfig>(), cards).PesquisaCap, Is.EqualTo(45f));
    }
}
