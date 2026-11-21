namespace Metis.Core.Tests;

public class MusicFadeTests
{
    [Test]
    public void StartsAndEndsExactly()
    {
        Assert.That(MusicFade.VolumeAt(0f, 1f, 0.2f, 1f), Is.EqualTo(0.2f));
        Assert.That(MusicFade.VolumeAt(1f, 1f, 0.2f, 1f), Is.EqualTo(1f));
        Assert.That(MusicFade.VolumeAt(5f, 1f, 1f, 0f), Is.EqualTo(0f), "depois do fim fica no destino");
        Assert.That(MusicFade.VolumeAt(-1f, 1f, 1f, 0f), Is.EqualTo(1f));
    }

    [Test]
    public void ZeroDurationJumpsToTheTarget()
    {
        Assert.That(MusicFade.VolumeAt(0f, 0f, 1f, 0f), Is.EqualTo(0f));
        Assert.That(MusicFade.IsDone(0f, 0f), Is.True);
    }

    [Test]
    public void IsMonotonicAndHalfwayAtTheMiddle()
    {
        var previous = 0f;
        for (var i = 0; i <= 20; i++)
        {
            var volume = MusicFade.VolumeAt(i / 20f, 1f, 0f, 1f);
            Assert.That(volume, Is.GreaterThanOrEqualTo(previous));
            previous = volume;
        }
        Assert.That(MusicFade.VolumeAt(0.5f, 1f, 0f, 1f), Is.EqualTo(0.5f).Within(1e-5));
        Assert.That(MusicFade.IsDone(0.99f, 1f), Is.False);
    }
}

public class CrisisMusicRuleTests
{
    private const float Threshold = 25f;

    private static Func<CityParameterType, float> AllAt(float value, CityParameterType? other = null, float otherValue = 0f)
    {
        return parameter => other.HasValue && parameter == other.Value ? otherValue : value;
    }

    [Test]
    public void EntersAtTheCrisisThreshold()
    {
        var rule = new CrisisMusicRule();
        Assert.That(rule.Update(AllAt(50f, CityParameterType.Saude, 25.01f), Threshold), Is.False);
        Assert.That(rule.Update(AllAt(50f, CityParameterType.Saude, 25f), Threshold), Is.True);
    }

    [Test]
    public void StaysUntilEveryoneIsAboveTheMargin()
    {
        var rule = new CrisisMusicRule();
        rule.Update(AllAt(50f, CityParameterType.Renda, 20f), Threshold);

        Assert.That(rule.Update(AllAt(50f, CityParameterType.Renda, 26f), Threshold), Is.True);
        Assert.That(rule.Update(AllAt(50f, CityParameterType.Renda, 28f), Threshold), Is.True, "28 ainda esta dentro da folga");
        Assert.That(rule.Update(AllAt(50f, CityParameterType.Renda, 28.1f), Threshold), Is.False);
        Assert.That(rule.Update(AllAt(50f, CityParameterType.Renda, 27f), Threshold), Is.False, "fora da crise, so 25 ou menos faz entrar");
    }

    [Test]
    public void ResearchNeverCountsButWellBeingDoes()
    {
        var rule = new CrisisMusicRule();
        Assert.That(rule.Update(AllAt(50f, CityParameterType.Pesquisa, 0f), Threshold), Is.False);
        Assert.That(rule.Update(AllAt(50f, CityParameterType.BemEstar, 10f), Threshold), Is.True);
        Assert.That(rule.Update(AllAt(50f, CityParameterType.Pesquisa, 10f), Threshold), Is.False, "a Pesquisa baixa nao segura a crise");
    }

    [Test]
    public void ResetClearsTheCrisis()
    {
        var rule = new CrisisMusicRule();
        rule.Update(AllAt(10f), Threshold);
        rule.Reset();
        Assert.That(rule.IsActive, Is.False);
        Assert.That(rule.Update(null, Threshold), Is.False);
    }
}

public class MusicMoodSelectorTests
{
    [Test]
    public void SequenceBeatsCrisisAndCrisisBeatsPlaylist()
    {
        var selector = new MusicMoodSelector();
        Assert.That(selector.Current, Is.EqualTo(MusicMood.Playlist));

        Assert.That(selector.SetCrisis(true), Is.True);
        Assert.That(selector.Current, Is.EqualTo(MusicMood.Crisis));

        Assert.That(selector.SetSequence(true), Is.True);
        Assert.That(selector.Current, Is.EqualTo(MusicMood.Sequence));
        Assert.That(selector.SetCrisis(false), Is.False, "a sequencia continua, nada muda");

        Assert.That(selector.SetSequence(false), Is.True);
        Assert.That(selector.Current, Is.EqualTo(MusicMood.Playlist));
    }

    [Test]
    public void EndOfPhaseClearsTheCrisis()
    {
        var selector = new MusicMoodSelector();
        selector.SetCrisis(true);

        Assert.That(selector.EndPhase(), Is.True);
        Assert.That(selector.Current, Is.EqualTo(MusicMood.Playlist));
        Assert.That(selector.SetCrisis(true), Is.False, "depois do fim da fase a crise nao volta");
        Assert.That(selector.Current, Is.EqualTo(MusicMood.Playlist));

        selector.StartPhase();
        Assert.That(selector.SetCrisis(true), Is.True);
        Assert.That(selector.Current, Is.EqualTo(MusicMood.Crisis));
    }

    [Test]
    public void RepeatedCallsReportNoChange()
    {
        var selector = new MusicMoodSelector();
        Assert.That(selector.SetCrisis(false), Is.False);
        Assert.That(selector.SetSequence(true), Is.True);
        Assert.That(selector.SetSequence(true), Is.False);
    }
}

public class SlideSequenceTests
{
    private static SlideSequence ThreeSlides()
    {
        return new SlideSequence(new[]
        {
            new SlideStep(0, "a", 2f),
            new SlideStep(1, "b", 3f),
            new SlideStep(2, "c", 1f),
        });
    }

    [Test]
    public void AdvancesByTimeAndEndsExactly()
    {
        var sequence = ThreeSlides();
        sequence.Tick(1.99f);
        Assert.That(sequence.CurrentIndex, Is.EqualTo(0));
        sequence.Tick(0.01f);
        Assert.That(sequence.CurrentIndex, Is.EqualTo(1));
        Assert.That(sequence.ElapsedInStep, Is.EqualTo(0f).Within(1e-4));

        sequence.Tick(3f);
        Assert.That(sequence.CurrentIndex, Is.EqualTo(2));
        Assert.That(sequence.IsFinished, Is.False);
        sequence.Tick(1f);
        Assert.That(sequence.IsFinished, Is.True);
        Assert.That(sequence.WasSkipped, Is.False);
    }

    [Test]
    public void LongFrameCarriesOverToTheNextSlides()
    {
        var sequence = ThreeSlides();
        sequence.Tick(5.5f);
        Assert.That(sequence.CurrentIndex, Is.EqualTo(2));
        Assert.That(sequence.ElapsedInStep, Is.EqualTo(0.5f).Within(1e-4));
    }

    [Test]
    public void TapDuringTheFadeCompletesTheFade()
    {
        var sequence = ThreeSlides();
        sequence.Tick(0.1f);
        Assert.That(sequence.IsFading, Is.True);

        sequence.Tap();
        Assert.That(sequence.CurrentIndex, Is.EqualTo(0));
        Assert.That(sequence.FadeProgress, Is.EqualTo(1f));
        Assert.That(sequence.IsFading, Is.False);

        sequence.Tap();
        Assert.That(sequence.CurrentIndex, Is.EqualTo(1), "com a tela parada, o toque avanca");
        Assert.That(sequence.FadeProgress, Is.EqualTo(0f));
    }

    [Test]
    public void TapOnTheLastSlideFinishes()
    {
        var sequence = ThreeSlides();
        sequence.Tick(5.5f);
        sequence.Tap();
        Assert.That(sequence.IsFinished, Is.True);
        sequence.Tap();
        sequence.Tick(1f);
        Assert.That(sequence.IsFinished, Is.True);
    }

    [Test]
    public void SkipEndsAtOnce()
    {
        var sequence = ThreeSlides();
        sequence.Tick(0.5f);
        sequence.Skip();
        Assert.That(sequence.IsFinished, Is.True);
        Assert.That(sequence.WasSkipped, Is.True);
    }

    [Test]
    public void ZoomGrowsAcrossTheSlide()
    {
        var sequence = ThreeSlides();
        Assert.That(sequence.Zoom, Is.EqualTo(SlideSequence.ZoomStart));
        sequence.Tick(1f);
        Assert.That(sequence.Zoom, Is.EqualTo((SlideSequence.ZoomStart + SlideSequence.ZoomEnd) / 2f).Within(1e-5));
    }

    [Test]
    public void SlidesLastAtLeastTheFadeAndEmptyListIsFinished()
    {
        var sequence = new SlideSequence(new[] { new SlideStep(0, null, 0f) });
        Assert.That(sequence.Steps[0].Duration, Is.EqualTo(SlideSequence.FadeDuration));
        Assert.That(sequence.Steps[0].CaptionKey, Is.EqualTo(string.Empty));

        Assert.That(new SlideSequence(null).IsFinished, Is.True);
        Assert.That(new SlideSequence(Array.Empty<SlideStep>()).IsFinished, Is.True);
    }

    [Test]
    public void IgnoresBadTime()
    {
        var sequence = ThreeSlides();
        sequence.Tick(-1f);
        sequence.Tick(float.NaN);
        Assert.That(sequence.ElapsedInStep, Is.EqualTo(0f));
    }
}

public class FlyoverPathTests
{
    private static FlyoverPath Path()
    {
        return new FlyoverPath(new CameraPose(7f, 30f, 7f, 7f), new CameraPose(0f, 30f, -5f, 19f), 4f);
    }

    [Test]
    public void StartsAndEndsExactly()
    {
        var path = Path();
        var start = path.PoseAt(0f);
        var end = path.PoseAt(1f);

        Assert.That(start.X, Is.EqualTo(7f));
        Assert.That(start.Size, Is.EqualTo(7f));
        Assert.That(end.X, Is.EqualTo(0f));
        Assert.That(end.Z, Is.EqualTo(-5f));
        Assert.That(end.Size, Is.EqualTo(19f), "termina no enquadramento de repouso, sem erro de arredondamento");
        Assert.That(path.PoseAtTime(4f).Size, Is.EqualTo(19f));
    }

    [Test]
    public void OutsideTheRangeStaysAtTheEnds()
    {
        var path = Path();
        Assert.That(path.PoseAt(-2f).Size, Is.EqualTo(7f));
        Assert.That(path.PoseAt(float.NaN).Size, Is.EqualTo(7f));
        Assert.That(path.PoseAt(3f).Size, Is.EqualTo(19f));
        Assert.That(path.IsFinished(3.9f), Is.False);
        Assert.That(path.IsFinished(4f), Is.True);
    }

    [Test]
    public void SizeAndPositionMoveOneWay()
    {
        var path = Path();
        var previous = path.PoseAt(0f);
        for (var i = 1; i <= 40; i++)
        {
            var pose = path.PoseAt(i / 40f);
            Assert.That(pose.Size, Is.GreaterThanOrEqualTo(previous.Size));
            Assert.That(pose.X, Is.LessThanOrEqualTo(previous.X));
            Assert.That(pose.Z, Is.LessThanOrEqualTo(previous.Z));
            previous = pose;
        }
    }

    [Test]
    public void ZeroDurationIsAlreadyAtTheEnd()
    {
        var path = new FlyoverPath(new CameraPose(0f, 0f, 0f, 7f), new CameraPose(0f, 0f, 0f, 19f), 0f);
        Assert.That(path.IsFinished(0f), Is.True);
        Assert.That(path.PoseAtTime(0f).Size, Is.EqualTo(19f));
    }
}

public class RunSummaryTests
{
    private static MetaProgressionState State(int phasesCompleted, string boss = "")
    {
        return new MetaProgressionState
        {
            Archetype = CardArchetype.Industria,
            PhasesCompleted = phasesCompleted,
            CurrentBoss = boss,
        };
    }

    private static RunSummaryLine Line(RunSummary summary, RunSummaryLineKind kind)
    {
        return summary.Lines.Single(line => line.Kind == kind);
    }

    [TestCase(0, 1)]
    [TestCase(1, 2)]
    [TestCase(2, 3)]
    public void DefeatInARegularPhase(int completed, int phase)
    {
        var summary = RunSummary.Build(State(completed), RunEnding.Defeat);

        Assert.That(summary.EndPhase, Is.EqualTo(phase));
        Assert.That(summary.PhasesWon, Is.EqualTo(completed));
        Assert.That(Line(summary, RunSummaryLineKind.DefeatPhase).Number, Is.EqualTo(phase));
        Assert.That(summary.Lines.Any(line => line.Kind == RunSummaryLineKind.DefeatAtBoss), Is.False);
    }

    [Test]
    public void DefeatAgainstTheBoss()
    {
        var summary = RunSummary.Build(State(3, "BossEnchente"), RunEnding.Defeat);

        Assert.That(summary.PhasesWon, Is.EqualTo(3));
        Assert.That(Line(summary, RunSummaryLineKind.DefeatAtBoss).Ids, Is.EqualTo(new[] { "BossEnchente" }));
        Assert.That(summary.Lines.Any(line => line.Kind == RunSummaryLineKind.DefeatPhase), Is.False);
    }

    [Test]
    public void VictoryAgainstTheBossCountsTheBossPhase()
    {
        var summary = RunSummary.Build(State(3, "BossEnchente"), RunEnding.BossVictory);

        Assert.That(summary.PhasesWon, Is.EqualTo(4));
        Assert.That(Line(summary, RunSummaryLineKind.BossDefeated).Ids, Is.EqualTo(new[] { "BossEnchente" }));
        Assert.That(Line(summary, RunSummaryLineKind.Archetype).Number, Is.EqualTo((int)CardArchetype.Industria));
    }

    [Test]
    public void EmptyListsAreLeftOut()
    {
        var summary = RunSummary.Build(State(0), RunEnding.Defeat);

        Assert.That(summary.Lines.Select(line => line.Kind), Is.EqualTo(new[]
        {
            RunSummaryLineKind.Archetype, RunSummaryLineKind.PhasesWon, RunSummaryLineKind.DefeatPhase,
        }));
    }

    [Test]
    public void ListsThemesRewardsAndTradesInOrder()
    {
        var state = State(2);
        state.PlayedThemes.AddRange(new[] { "TemaPorto", "TemaEnchente" });
        state.LoadedCardNames.Add("CardCiclovias");
        state.LoadedAdvantageNames.Add("AdvantageBonus");
        state.AcceptedTrades.AddRange(new[] { "TradeConcessao", "" });

        var summary = RunSummary.Build(state, RunEnding.Defeat);

        Assert.That(Line(summary, RunSummaryLineKind.Themes).Ids, Is.EqualTo(new[] { "TemaPorto", "TemaEnchente" }));
        Assert.That(Line(summary, RunSummaryLineKind.Themes).Number, Is.EqualTo(2));
        Assert.That(Line(summary, RunSummaryLineKind.RewardCards).Ids, Is.EqualTo(new[] { "CardCiclovias" }));
        Assert.That(Line(summary, RunSummaryLineKind.RewardAdvantages).Ids, Is.EqualTo(new[] { "AdvantageBonus" }));
        Assert.That(Line(summary, RunSummaryLineKind.Trades).Ids, Is.EqualTo(new[] { "TradeConcessao" }), "id vazio fica de fora");
    }

    [Test]
    public void SurvivesTheSaveBeingCleared()
    {
        var state = State(1);
        state.PlayedThemes.Add("TemaPorto");
        var summary = RunSummary.Build(state, RunEnding.Defeat);

        state.PlayedThemes.Clear();
        state.PhasesCompleted = 0;

        Assert.That(summary.Themes, Is.EqualTo(new[] { "TemaPorto" }));
        Assert.That(summary.EndPhase, Is.EqualTo(2));
    }

    [Test]
    public void NullStateGivesAnEmptySummary()
    {
        var summary = RunSummary.Build(null, RunEnding.Defeat);
        Assert.That(summary.Lines, Is.Empty);
        Assert.That(summary.BossId, Is.EqualTo(string.Empty));
    }
}
