using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class NewsEditorTests
{
    private static readonly CityParameterType[] Parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));

    private sealed class FakeStory : INewsStoryDefinition
    {
        public string Id { get; init; } = "Story";
        public NewsTrigger Trigger { get; init; }
        public CityParameterType Parameter { get; init; }
        public string TargetId { get; init; } = string.Empty;
    }

    private sealed class FakeColumn : INewsColumnDefinition
    {
        public string Id { get; init; } = "Column";
        public NewsColumnKind Kind { get; init; }
        public NewsCitizen Citizen { get; init; }
        public NewsMoodBand Mood { get; init; }
    }

    // Critico 10 em todos menos Pesquisa, crise em 25, excesso em 75, tiers em 15 e 30.
    private static NewsRules Rules()
    {
        var configs = Parameters.Select(parameter => new CityParameterConfig
        {
            Parameter = parameter,
            MinValue = 0f,
            MaxValue = 100f,
            CriticalLevel = parameter == CityParameterType.Pesquisa ? -1f : 10f,
        });
        return new NewsRules(configs, 25f, 75f, new[] { 15f, 30f });
    }

    private static float[] Values(float neutral = 50f, params (CityParameterType Parameter, float Value)[] overrides)
    {
        var values = Parameters.Select(_ => neutral).ToArray();
        foreach (var entry in overrides)
            values[(int)entry.Parameter] = entry.Value;
        return values;
    }

    private static FakeStory Story(string id, NewsTrigger trigger, CityParameterType parameter = default)
    {
        return new FakeStory { Id = id, Trigger = trigger, Parameter = parameter };
    }

    [Test]
    public void DetectSignals_NoChange_HasNoSignal()
    {
        Assert.That(NewsEditor.DetectSignals(Values(), Values(), Rules()), Is.Empty);
    }

    [Test]
    public void DetectSignals_CollapseWinsOverCrisisAndDropForTheSameParameter()
    {
        var signals = NewsEditor.DetectSignals(Values(), Values(overrides: (CityParameterType.Energia, 8f)), Rules());

        Assert.That(signals.Count, Is.EqualTo(1));
        Assert.That(signals[0].Trigger, Is.EqualTo(NewsTrigger.EnteredCollapse));
        Assert.That(signals[0].Parameter, Is.EqualTo(CityParameterType.Energia));
        Assert.That(signals[0].Delta, Is.EqualTo(-42f));
    }

    [Test]
    public void DetectSignals_FindsEachKindOfChange()
    {
        var before = Values(overrides: new[]
        {
            (CityParameterType.Renda, 8f),
            (CityParameterType.Saude, 30f),
            (CityParameterType.Mobilidade, 72f),
            (CityParameterType.Pesquisa, 12f),
        });
        var after = Values(overrides: new[]
        {
            (CityParameterType.Renda, 14f),
            (CityParameterType.Saude, 24f),
            (CityParameterType.Mobilidade, 76f),
            (CityParameterType.Seguranca, 58f),
            (CityParameterType.Pesquisa, 16f),
            (CityParameterType.Populacao, 52f),
        });

        var signals = NewsEditor.DetectSignals(before, after, Rules());
        var byParameter = signals.Where(signal => NewsRules.UsesParameter(signal.Trigger)).ToDictionary(signal => signal.Parameter, signal => signal.Trigger);

        Assert.That(byParameter[CityParameterType.Renda], Is.EqualTo(NewsTrigger.LeftCollapse));
        Assert.That(byParameter[CityParameterType.Saude], Is.EqualTo(NewsTrigger.EnteredCrisis));
        Assert.That(byParameter[CityParameterType.Mobilidade], Is.EqualTo(NewsTrigger.EnteredExcess));
        Assert.That(byParameter[CityParameterType.Seguranca], Is.EqualTo(NewsTrigger.SharpRise));
        Assert.That(signals.Select(signal => signal.Trigger), Has.Member(NewsTrigger.ResearchTier));
        Assert.That(signals.Select(signal => signal.Trigger), Has.Member(NewsTrigger.Crowding));
        Assert.That(signals[0].Trigger, Is.EqualTo(NewsTrigger.LeftCollapse), "a mais importante vem primeiro");
    }

    [Test]
    public void DetectSignals_WellbeingOutranksTheSameTriggerElsewhere()
    {
        var signals = NewsEditor.DetectSignals(Values(), Values(overrides: new[] { (CityParameterType.Saude, 40f), (CityParameterType.BemEstar, 42f) }), Rules());

        Assert.That(signals.Select(signal => signal.Parameter), Is.EqualTo(new[] { CityParameterType.BemEstar, CityParameterType.Saude }));
    }

    [Test]
    public void DetectSignals_ResearchHasNoCrisisAndWellbeingHasNoCollapse()
    {
        var signals = NewsEditor.DetectSignals(
            Values(overrides: new[] { (CityParameterType.Pesquisa, 30f), (CityParameterType.BemEstar, 30f) }),
            Values(overrides: new[] { (CityParameterType.Pesquisa, 26f), (CityParameterType.BemEstar, 9f) }),
            Rules());

        Assert.That(signals.Any(signal => signal.Parameter == CityParameterType.Pesquisa), Is.False);
        Assert.That(signals.Single().Trigger, Is.EqualTo(NewsTrigger.EnteredCrisis));
    }

    [Test]
    public void Compose_HeadlineFromTheStrongestSignalAndAtMostTwoNotes()
    {
        var signals = NewsEditor.DetectSignals(Values(), Values(overrides: new[]
        {
            (CityParameterType.Energia, 8f),
            (CityParameterType.Saude, 20f),
            (CityParameterType.Seguranca, 60f),
            (CityParameterType.Mobilidade, 40f),
        }), Rules());
        var stories = new List<FakeStory>
        {
            Story("ColapsoEnergia", NewsTrigger.EnteredCollapse, CityParameterType.Energia),
            Story("CriseSaude", NewsTrigger.EnteredCrisis, CityParameterType.Saude),
            Story("AltaSeguranca", NewsTrigger.SharpRise, CityParameterType.Seguranca),
            Story("QuedaMobilidade", NewsTrigger.SharpDrop, CityParameterType.Mobilidade),
        };

        var edition = NewsEditor.Compose(4, signals, stories, null, new Random(1), 30f, -5f);

        Assert.That(edition.Items.Select(item => item.StoryId), Is.EqualTo(new[] { "ColapsoEnergia", "CriseSaude", "QuedaMobilidade" }));
        Assert.That(edition.Strong, Is.True);
        Assert.That(edition.Items[0].Value, Is.EqualTo(8f));
        Assert.That(edition.TurnIndex, Is.EqualTo(4));
    }

    [Test]
    public void Compose_SignalWithoutStoryIsSkipped_AndNothingLeftFallsBackToQuietTurn()
    {
        var signals = NewsEditor.DetectSignals(Values(), Values(overrides: (CityParameterType.Saude, 40f)), Rules());
        var stories = new List<FakeStory> { Story("Calmaria", NewsTrigger.QuietTurn) };

        var edition = NewsEditor.Compose(2, signals, stories, null, new Random(1), 50f, 0f);

        Assert.That(edition.Items.Single().StoryId, Is.EqualTo("Calmaria"));
        Assert.That(edition.Strong, Is.False);
    }

    [Test]
    public void Compose_AvoidsStoriesFromRecentEditionsWhileThereIsAnother()
    {
        var stories = new List<FakeStory> { Story("A", NewsTrigger.QuietTurn), Story("B", NewsTrigger.QuietTurn) };
        var previous = new List<NewsEdition> { new NewsEdition { TurnIndex = 1, Items = { new NewsItem { StoryId = "A" } } } };

        for (var seed = 0; seed < 20; seed++)
            Assert.That(NewsEditor.Compose(2, new List<NewsSignal>(), stories, previous, new Random(seed), 50f, 0f).Items.Single().StoryId, Is.EqualTo("B"));

        var onlyA = new List<FakeStory> { stories[0] };
        Assert.That(NewsEditor.Compose(2, new List<NewsSignal>(), onlyA, previous, new Random(0), 50f, 0f).Items.Single().StoryId, Is.EqualTo("A"), "sem outra, repete");
    }

    [Test]
    public void Compose_SameSeedAndTurn_GivesTheSameEdition()
    {
        var stories = Enumerable.Range(0, 6).Select(i => Story("Q" + i, NewsTrigger.QuietTurn)).ToList();

        var first = NewsEditor.Compose(7, new List<NewsSignal>(), stories, null, NewsEditor.EditionRandom(99UL, 7), 50f, 0f);
        var again = NewsEditor.Compose(7, new List<NewsSignal>(), stories, null, NewsEditor.EditionRandom(99UL, 7), 50f, 0f);

        Assert.That(again.Items.Single().StoryId, Is.EqualTo(first.Items.Single().StoryId));
    }

    [Test]
    public void Inauguration_IsTurnZero()
    {
        var edition = NewsEditor.Inauguration(new List<FakeStory> { Story("Posse", NewsTrigger.Inauguration) }, new Random(0), 47f);

        Assert.That(edition.TurnIndex, Is.EqualTo(0));
        Assert.That(edition.Items.Single().StoryId, Is.EqualTo("Posse"));
        Assert.That(edition.Wellbeing, Is.EqualTo(47f));
    }

    [Test]
    public void TryMainCause_PicksTheLargestShareInTheDirectionOfTheChange()
    {
        var breakdown = new TurnBreakdown(3);
        breakdown.Add(CityParameterType.Saude, ContributionSource.Interaction, -4f, CityParameterType.Energia);
        breakdown.Add(CityParameterType.Saude, ContributionSource.Interaction, -1f, CityParameterType.Energia);
        breakdown.Add(CityParameterType.Saude, ContributionSource.Deriva, -3f);
        breakdown.Add(CityParameterType.Saude, ContributionSource.Card, 2f);
        breakdown.Add(CityParameterType.Saude, ContributionSource.Clamp, -9f);

        Assert.That(NewsEditor.TryMainCause(breakdown, CityParameterType.Saude, -6f, out var source, out var from), Is.True);
        Assert.That(source, Is.EqualTo(ContributionSource.Interaction));
        Assert.That(from, Is.EqualTo(CityParameterType.Energia));
        Assert.That(NewsEditor.TryMainCause(breakdown, CityParameterType.Saude, 2f, out source, out _), Is.True);
        Assert.That(source, Is.EqualTo(ContributionSource.Card));
        Assert.That(NewsEditor.TryMainCause(null, CityParameterType.Saude, -6f, out _, out _), Is.False);
    }

    [Test]
    public void Archive_TracksUnreadAndStrongUntilRead()
    {
        var archive = new NewsArchive(5UL);
        var changes = 0;
        archive.Changed += () => changes++;

        archive.Add(new NewsEdition { TurnIndex = 0 });
        Assert.That(archive.HasUnread, Is.True);
        Assert.That(archive.HasStrongUnread, Is.False);

        archive.Add(new NewsEdition { TurnIndex = 1, Strong = true });
        Assert.That(archive.HasStrongUnread, Is.True);

        archive.MarkAllRead();
        Assert.That(archive.HasUnread, Is.False);
        Assert.That(archive.HasStrongUnread, Is.False);
        Assert.That(archive.LastReadTurn, Is.EqualTo(1));
        Assert.That(changes, Is.EqualTo(3));
    }

    [Test]
    public void Archive_SameTurnAgain_ReplacesTheEdition()
    {
        var archive = new NewsArchive(5UL);
        archive.Add(new NewsEdition { TurnIndex = 2, Items = { new NewsItem { StoryId = "Velha" } } });
        archive.Add(new NewsEdition { TurnIndex = 2, Items = { new NewsItem { StoryId = "Nova" } } });

        Assert.That(archive.Editions.Single().Items.Single().StoryId, Is.EqualTo("Nova"));
    }

    [Test]
    public void SaveRoundTrip_KeepsSeedEditionsAndWhatWasRead()
    {
        var archive = new NewsArchive(123UL);
        archive.Add(new NewsEdition { TurnIndex = 0 });
        archive.MarkAllRead();
        archive.Add(new NewsEdition { TurnIndex = 1, Strong = true, Items = { new NewsItem { StoryId = "X", HasCause = true, CauseSource = ContributionSource.Event } } });
        var phase = new PhaseSaveData();

        RunSaveRules.CaptureNews(phase, archive);
        var restored = RunSaveRules.RestoreNews(phase, 999UL);

        Assert.That(restored.Seed, Is.EqualTo(123UL));
        Assert.That(restored.Editions.Select(edition => edition.TurnIndex), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(restored.LastReadTurn, Is.EqualTo(0));
        Assert.That(restored.HasStrongUnread, Is.True);
        Assert.That(restored.Editions[1].Items.Single().CauseSource, Is.EqualTo(ContributionSource.Event));
    }

    [Test]
    public void RestoreNews_SaveFromBeforeTheNewspaper_GetsAFreshSeedAndNoEditions()
    {
        var restored = RunSaveRules.RestoreNews(new PhaseSaveData(), 999UL);

        Assert.That(restored.Seed, Is.EqualTo(999UL));
        Assert.That(restored.Editions, Is.Empty);
        Assert.That(restored.HasUnread, Is.False);
    }

    [TestCase("Ar em {valor}. Fim", "26 m²/hab.", "Ar em 26 m²/hab. Fim")]
    [TestCase("Ar em {valor}. Fim", "26 km", "Ar em 26 km. Fim")]
    [TestCase("{valor} e {valor}", "26 m²/hab.", "26 m²/hab. e 26 m²/hab.")]
    [TestCase("Ar em {valor}. Fim", "26<size=60%> m²/hab.</size>", "Ar em 26<size=60%> m²/hab.</size> Fim")]
    [TestCase("Ar em {valor}. Fim", "26<size=60%> km</size>", "Ar em 26<size=60%> km</size>. Fim")]
    public void FillMarker_DoesNotDoubleTheFinalPeriod(string text, string value, string expected)
    {
        Assert.That(NewsEditor.FillMarker(text, "{valor}", value), Is.EqualTo(expected));
    }

    private static float[][] Series(CityParameterType parameter, params float[] values)
    {
        return values.Select(value => Values(50f, (parameter, value))).ToArray();
    }

    [Test]
    public void DetectTrends_ThreeTurnsSameDirection_IsATrendWithTheTotalChange()
    {
        var signals = NewsEditor.DetectTrends(Series(CityParameterType.Renda, 40f, 43f, 46f, 49f), Rules(), null);

        Assert.That(signals, Has.Count.EqualTo(1));
        Assert.That(signals[0].Trigger, Is.EqualTo(NewsTrigger.TrendUp));
        Assert.That(signals[0].Parameter, Is.EqualTo(CityParameterType.Renda));
        Assert.That(signals[0].Delta, Is.EqualTo(9f));
        Assert.That(signals[0].Value, Is.EqualTo(49f));
    }

    [Test]
    public void DetectTrends_FallingSeries_IsTrendDown()
    {
        var signals = NewsEditor.DetectTrends(Series(CityParameterType.Saude, 60f, 57f, 54f, 51f), Rules(), null);

        Assert.That(signals.Single().Trigger, Is.EqualTo(NewsTrigger.TrendDown));
        Assert.That(signals.Single().Delta, Is.EqualTo(-9f));
    }

    [TestCase(40f, 43f, 41f, 49f)]
    [TestCase(40f, 43f, 43f, 49f)]
    [TestCase(40f, 42f, 44f, 47f)]
    public void DetectTrends_BrokenOrSmallSeries_IsNotATrend(float a, float b, float c, float d)
    {
        Assert.That(NewsEditor.DetectTrends(Series(CityParameterType.Renda, a, b, c, d), Rules(), null), Is.Empty);
    }

    [Test]
    public void DetectTrends_TooFewTurns_IsNotATrend()
    {
        Assert.That(NewsEditor.DetectTrends(Series(CityParameterType.Renda, 40f, 45f, 50f), Rules(), null), Is.Empty);
    }

    [Test]
    public void DetectTrends_SameParameterRecentlyPublished_WaitsFiveEditions()
    {
        var trendEdition = new NewsEdition { TurnIndex = 1 };
        trendEdition.Items.Add(new NewsItem { Trigger = NewsTrigger.TrendDown, Parameter = CityParameterType.Renda });
        var series = Series(CityParameterType.Renda, 40f, 43f, 46f, 49f);

        Assert.That(NewsEditor.DetectTrends(series, Rules(), new[] { trendEdition }), Is.Empty);

        var editions = new List<NewsEdition> { trendEdition };
        editions.AddRange(Enumerable.Range(2, NewsEditor.RecentEditions).Select(turn => new NewsEdition { TurnIndex = turn }));
        Assert.That(NewsEditor.DetectTrends(series, Rules(), editions), Has.Count.EqualTo(1));
    }

    [Test]
    public void RecentSnapshots_UsesStartEarlyAndOnlyTheLastTurnsLater()
    {
        var start = new[] { 1f };
        PhaseTurnRecord Turn(int index) => new PhaseTurnRecord { TurnIndex = index, EndValues = new[] { index * 10f } };
        var turns = Enumerable.Range(1, 5).Select(Turn).ToList();

        Assert.That(NewsEditor.RecentSnapshots(start, turns, turns[0]).Select(values => values[0]), Is.EqualTo(new[] { 1f, 10f }));
        Assert.That(NewsEditor.RecentSnapshots(start, turns, turns[2]).Select(values => values[0]), Is.EqualTo(new[] { 1f, 10f, 20f, 30f }));
        Assert.That(NewsEditor.RecentSnapshots(start, turns, turns[4]).Select(values => values[0]), Is.EqualTo(new[] { 20f, 30f, 40f, 50f }));
    }

    [Test]
    public void BuildSignals_TurnSignalBeatsTrendOfTheSameParameter_AndTargetsComeLast()
    {
        var signals = NewsEditor.BuildSignals(Series(CityParameterType.Renda, 60f, 55f, 50f, 40f), Rules(), null, "CardX", "EventY");

        Assert.That(signals.Select(signal => signal.Trigger), Is.EqualTo(new[] { NewsTrigger.SharpDrop, NewsTrigger.EventHappened, NewsTrigger.CardPlayed }));
        Assert.That(signals[1].TargetId, Is.EqualTo("EventY"));
        Assert.That(signals[2].TargetId, Is.EqualTo("CardX"));
    }

    [Test]
    public void BuildSignals_TrendOfAnotherParameterIsKept()
    {
        var snapshots = Series(CityParameterType.Renda, 40f, 43f, 46f, 49f);
        snapshots[3][(int)CityParameterType.Saude] = 40f;

        var signals = NewsEditor.BuildSignals(snapshots, Rules(), null, string.Empty, string.Empty);

        Assert.That(signals.Select(signal => signal.Trigger), Is.EqualTo(new[] { NewsTrigger.SharpDrop, NewsTrigger.TrendUp }));
    }

    private static NewsEdition ComposeTargets(string cardId, string eventId, IReadOnlyList<NewsEdition> previous = null, params FakeStory[] stories)
    {
        var signals = NewsEditor.BuildSignals(new[] { Values(), Values() }, Rules(), previous, cardId, eventId);
        return NewsEditor.Compose(1, signals, stories, previous, new Random(1), 50f, 0f);
    }

    [Test]
    public void Compose_QuietTurn_HeadlineIsTheEventThenTheCard()
    {
        var card = new FakeStory { Id = "NCard", Trigger = NewsTrigger.CardPlayed, TargetId = "CardX" };
        var ev = new FakeStory { Id = "NEvent", Trigger = NewsTrigger.EventHappened, TargetId = "EventY" };
        var quiet = Story("Quiet", NewsTrigger.QuietTurn);

        Assert.That(ComposeTargets("CardX", "EventY", null, card, ev, quiet).Items.Select(item => item.StoryId), Is.EqualTo(new[] { "NEvent", "NCard" }));
        Assert.That(ComposeTargets("CardX", string.Empty, null, card, ev, quiet).Items.Single().StoryId, Is.EqualTo("NCard"));
        Assert.That(ComposeTargets("CardX", "EventY", null, card, ev, quiet).Items[0].TargetId, Is.EqualTo("EventY"));
    }

    [Test]
    public void Compose_TargetWithoutStory_FallsBackToTheQuietTurn()
    {
        var edition = ComposeTargets("CardSemTexto", "EventoSemTexto", null, Story("Quiet", NewsTrigger.QuietTurn));

        Assert.That(edition.Items.Single().StoryId, Is.EqualTo("Quiet"));
    }

    [Test]
    public void Compose_StoryOfAnotherCardIsNotUsed()
    {
        var other = new FakeStory { Id = "NOther", Trigger = NewsTrigger.CardPlayed, TargetId = "CardZ" };

        Assert.That(ComposeTargets("CardX", string.Empty, null, other, Story("Quiet", NewsTrigger.QuietTurn)).Items.Single().StoryId, Is.EqualTo("Quiet"));
    }

    [Test]
    public void Compose_TargetStoryPublishedRecently_DoesNotRepeat()
    {
        var card = new FakeStory { Id = "NCard", Trigger = NewsTrigger.CardPlayed, TargetId = "CardX" };
        var last = new NewsEdition { TurnIndex = 0 };
        last.Items.Add(new NewsItem { StoryId = "NCard" });

        Assert.That(ComposeTargets("CardX", string.Empty, new[] { last }, card, Story("Quiet", NewsTrigger.QuietTurn)).Items.Single().StoryId, Is.EqualTo("Quiet"));
    }

    [TestCase(70f, 4, NewsMoodBand.Good)]
    [TestCase(55f, 3, NewsMoodBand.Good)]
    [TestCase(54f, 2, NewsMoodBand.Neutral)]
    [TestCase(40f, 2, NewsMoodBand.Neutral)]
    [TestCase(39f, 1, NewsMoodBand.Bad)]
    [TestCase(10f, 0, NewsMoodBand.Bad)]
    public void MoodLevelAndBand_FollowTheWellbeingRanges(float wellbeing, int level, NewsMoodBand band)
    {
        Assert.That(NewsRules.MoodLevel(wellbeing), Is.EqualTo(level));
        Assert.That(NewsRules.MoodBand(wellbeing), Is.EqualTo(band));
    }

    [TestCase(25f, NewsMoodBand.Bad)]
    [TestCase(26f, NewsMoodBand.Neutral)]
    [TestCase(75f, NewsMoodBand.Good)]
    public void ForecastBand_UsesTheZoneThresholds(float value, NewsMoodBand band)
    {
        Assert.That(Rules().ForecastBand(value), Is.EqualTo(band));
    }

    private static List<FakeColumn> AllColumns()
    {
        var columns = new List<FakeColumn> { new FakeColumn { Id = "Class", Kind = NewsColumnKind.Classified } };
        foreach (NewsMoodBand band in Enum.GetValues(typeof(NewsMoodBand)))
        {
            columns.Add(new FakeColumn { Id = $"Horo{band}", Kind = NewsColumnKind.Horoscope, Mood = band });
            columns.Add(new FakeColumn { Id = $"Fore{band}", Kind = NewsColumnKind.PollutionForecast, Mood = band });
            foreach (NewsCitizen citizen in Enum.GetValues(typeof(NewsCitizen)))
                columns.Add(new FakeColumn { Id = $"Quote{citizen}{band}", Kind = NewsColumnKind.CitizenQuote, Citizen = citizen, Mood = band });
        }
        return columns;
    }

    private static NewsEdition Edition(int turn, float wellbeing, NewsTrigger? trigger = null, CityParameterType parameter = default)
    {
        var edition = new NewsEdition { TurnIndex = turn, Wellbeing = wellbeing };
        if (trigger.HasValue)
            edition.Items.Add(new NewsItem { Trigger = trigger.Value, Parameter = parameter });
        return edition;
    }

    [TestCase(CityParameterType.Saude, NewsCitizen.DonaCida)]
    [TestCase(CityParameterType.Energia, NewsCitizen.SeuArnaldo)]
    [TestCase(CityParameterType.Pesquisa, NewsCitizen.Julia)]
    public void ComposeColumns_CitizenFollowsTheHeadlineParameter(CityParameterType parameter, NewsCitizen citizen)
    {
        var edition = Edition(1, 60f, NewsTrigger.SharpDrop, parameter);

        NewsEditor.ComposeColumns(edition, AllColumns(), null, 7UL, 50f, Rules());

        Assert.That(edition.QuoteId, Is.EqualTo($"Quote{citizen}Good"));
    }

    [Test]
    public void ComposeColumns_WithoutExpert_CitizensTakeTurns()
    {
        var quotes = Enumerable.Range(1, 3).Select(turn =>
        {
            var edition = Edition(turn, 50f, NewsTrigger.QuietTurn);
            NewsEditor.ComposeColumns(edition, AllColumns(), null, 7UL, 50f, Rules());
            return edition.QuoteId;
        }).ToList();

        Assert.That(quotes.Distinct().Count(), Is.EqualTo(3));
        Assert.That(quotes, Has.All.EndsWith("Neutral"));
    }

    [TestCase(3, 60f, 50f, "Class")]
    [TestCase(4, 60f, 50f, "HoroGood")]
    [TestCase(4, 30f, 50f, "HoroBad")]
    [TestCase(5, 60f, 20f, "ForeBad")]
    [TestCase(5, 30f, 80f, "ForeGood")]
    public void ComposeColumns_RotatesKindsAndUsesTheRightBand(int turn, float wellbeing, float sustainability, string expected)
    {
        var edition = Edition(turn, wellbeing);

        NewsEditor.ComposeColumns(edition, AllColumns(), null, 7UL, sustainability, Rules());

        Assert.That(edition.ColumnId, Is.EqualTo(expected));
        Assert.That(edition.ColumnValue, Is.EqualTo(sustainability));
    }

    [Test]
    public void ComposeColumns_IsDeterministicAndAvoidsRecentTexts()
    {
        var columns = Enumerable.Range(0, 6).Select(i => new FakeColumn { Id = $"C{i}", Kind = NewsColumnKind.Classified }).ToList();
        var first = Edition(3, 50f);
        var again = Edition(3, 50f);
        NewsEditor.ComposeColumns(first, columns, null, 11UL, 50f, Rules());
        NewsEditor.ComposeColumns(again, columns, null, 11UL, 50f, Rules());
        Assert.That(again.ColumnId, Is.EqualTo(first.ColumnId));

        var previous = columns.Take(5).Select((column, i) => new NewsEdition { TurnIndex = i, ColumnId = column.Id }).ToList();
        var next = Edition(6, 50f);
        NewsEditor.ComposeColumns(next, columns, previous, 11UL, 50f, Rules());
        Assert.That(next.ColumnId, Is.EqualTo("C5"));
    }

    [Test]
    public void ComposeColumns_DoesNotChangeTheStories()
    {
        var edition = Edition(1, 50f, NewsTrigger.QuietTurn);
        var before = edition.Items.Single();

        NewsEditor.ComposeColumns(edition, AllColumns(), null, 7UL, 50f, Rules());

        Assert.That(edition.Items.Single(), Is.SameAs(before));
    }

    [Test]
    public void ComposeColumns_NoTexts_LeavesTheAreaEmpty()
    {
        var edition = Edition(1, 50f);

        NewsEditor.ComposeColumns(edition, new List<FakeColumn>(), null, 7UL, 50f, Rules());

        Assert.That(edition.QuoteId, Is.Empty);
        Assert.That(edition.ColumnId, Is.Empty);
    }

    private static NewsImportFormat.StoryDto ValidStory(string id, string trigger, string target = null)
    {
        return new NewsImportFormat.StoryDto { Id = id, Trigger = trigger, HeadlinePt = "M", HeadlineEn = "H", BodyPt = "C", BodyEn = "B", Target = target };
    }

    private static List<string> ValidateColumn(NewsImportFormat.ColumnDto column)
    {
        var file = new NewsImportFormat.FileDto { Stories = new[] { ValidStory("S", "QuietTurn") }, Columns = new[] { column } };
        return NewsImportFormat.Validate(file);
    }

    [Test]
    public void Import_TargetStoryNeedsAKnownTarget()
    {
        var cards = new[] { "CardX" };
        var events = new[] { "EventY" };
        List<string> Check(NewsImportFormat.StoryDto story) => NewsImportFormat.Validate(new NewsImportFormat.FileDto { Stories = new[] { story } }, cards, events);

        Assert.That(Check(ValidStory("A", "CardPlayed", "CardX")), Is.Empty);
        Assert.That(Check(ValidStory("A", "EventHappened", "EventY")), Is.Empty);
        Assert.That(Check(ValidStory("A", "CardPlayed")).Any(error => error.Contains("precisa citar")), Is.True);
        Assert.That(Check(ValidStory("A", "CardPlayed", "EventY")).Any(error => error.Contains("alvo")), Is.True);
        Assert.That(Check(ValidStory("A", "QuietTurn", "CardX")).Any(error => error.Contains("nao cita")), Is.True);
    }

    [Test]
    public void Import_TrendUsesNoFixedParameterAndLimitsLength()
    {
        Assert.That(NewsImportFormat.Validate(new NewsImportFormat.FileDto { Stories = new[] { ValidStory("A", "TrendUp") } }), Is.Empty);
        var tooLong = ValidStory("A", "TrendUp");
        tooLong.HeadlinePt = new string('x', NewsImportFormat.MaxHeadlineLength + 1);
        Assert.That(NewsImportFormat.Validate(new NewsImportFormat.FileDto { Stories = new[] { tooLong } }).Any(error => error.Contains("limite")), Is.True);
    }

    [Test]
    public void Import_ValidColumnsPass()
    {
        Assert.That(ValidateColumn(new NewsImportFormat.ColumnDto { Id = "Q", Kind = "CitizenQuote", Citizen = "Julia", Mood = "Good", TextPt = "a", TextEn = "b" }), Is.Empty);
        Assert.That(ValidateColumn(new NewsImportFormat.ColumnDto { Id = "C", Kind = "Classified", TextPt = "a", TextEn = "b" }), Is.Empty);
        Assert.That(ValidateColumn(new NewsImportFormat.ColumnDto { Id = "F", Kind = "PollutionForecast", Mood = "Bad", TextPt = "em {valor}", TextEn = "at {valor}" }), Is.Empty);
    }

    [TestCase("Q", "CitizenQuote", "Ninguem", "Good", "a", "morador")]
    [TestCase("Q", "CitizenQuote", "Julia", "Otimo", "a", "faixa")]
    [TestCase("C", "Classified", "Julia", "", "a", "morador")]
    [TestCase("C", "Classified", "", "Good", "a", "faixa")]
    [TestCase("H", "Horoscope", "", "Good", "em {valor}", "marcador")]
    [TestCase("F", "PollutionForecast", "", "Good", "em {turno}", "previsao")]
    [TestCase("X", "Outro", "", "", "a", "tipo")]
    [TestCase("D", "Classified", "", "", "a \u2014 b", "travessao")]
    public void Import_InvalidColumnsAreReported(string id, string kind, string citizen, string mood, string text, string message)
    {
        var errors = ValidateColumn(new NewsImportFormat.ColumnDto { Id = id, Kind = kind, Citizen = citizen, Mood = mood, TextPt = text, TextEn = text });

        Assert.That(errors.Any(error => error.Contains(message)), Is.True, string.Join("; ", errors));
    }

    [Test]
    public void Import_ColumnLengthAndRepeatedIdAreReported()
    {
        var tooLong = new NewsImportFormat.ColumnDto { Id = "C", Kind = "Classified", TextPt = new string('x', NewsImportFormat.MaxColumnLength + 1), TextEn = "b" };
        Assert.That(ValidateColumn(tooLong).Any(error => error.Contains("limite")), Is.True);

        var sameAsStory = new NewsImportFormat.ColumnDto { Id = "S", Kind = "Classified", TextPt = "a", TextEn = "b" };
        Assert.That(ValidateColumn(sameAsStory).Any(error => error.Contains("repetido")), Is.True);
    }

    [Test]
    public void Import_MissingColumnCoverageListsEveryGap()
    {
        var file = new NewsImportFormat.FileDto { Stories = new[] { ValidStory("S", "QuietTurn") }, Columns = new[] { new NewsImportFormat.ColumnDto { Id = "C", Kind = "Classified", Citizen = null, Mood = null } } };

        var missing = NewsImportFormat.MissingColumnCoverage(file);

        Assert.That(missing, Does.Not.Contain("Classified"));
        Assert.That(missing, Has.Member("CitizenQuote/Julia/Bad"));
        Assert.That(missing, Has.Member("Horoscope/Good"));
        Assert.That(missing, Has.Member("PollutionForecast/Neutral"));
    }

    [Test]
    public void Import_CardAndEventStoriesAreNotRequiredByTheTriggerCoverage()
    {
        var missing = NewsImportFormat.MissingCoverage(new NewsImportFormat.FileDto());

        Assert.That(missing, Does.Not.Contain("CardPlayed"));
        Assert.That(missing, Does.Not.Contain("EventHappened"));
        Assert.That(missing, Has.Member("TrendUp"));
    }
}

public class NewsImportFormatTests
{
    private static NewsImportFormat.StoryDto Valid(string id = "S1", string trigger = "SharpDrop", string parameter = "Saude")
    {
        return new NewsImportFormat.StoryDto
        {
            Id = id,
            Trigger = trigger,
            Parameter = parameter,
            HeadlinePt = "Saúde cai para {valor}",
            HeadlineEn = "Health drops to {valor}",
            BodyPt = "Queda de {variacao} no turno {turno}.",
            BodyEn = "A drop of {variacao} on turn {turno}.",
        };
    }

    private static List<string> Errors(params NewsImportFormat.StoryDto[] stories)
    {
        return NewsImportFormat.Validate(new NewsImportFormat.FileDto { Stories = stories });
    }

    [Test]
    public void Validate_GoodStory_HasNoError()
    {
        Assert.That(Errors(Valid(), Valid("Calma", "QuietTurn", "")), Is.Empty);
    }

    [Test]
    public void Validate_ReportsEachProblem()
    {
        var dash = Valid("Travessao");
        dash.BodyPt = "Caiu — muito.";
        var placeholder = Valid("Marcador");
        placeholder.HeadlineEn = "Health hits {value}";
        var english = Valid("SemIngles");
        english.BodyEn = "";

        var errors = Errors(
            Valid("Gatilho", "Explodiu"),
            Valid("SemParametro", "SharpDrop", ""),
            Valid("PesquisaEmCrise", "EnteredCrisis", "Pesquisa"),
            Valid("ParametroSobrando", "QuietTurn", "Saude"),
            dash, placeholder, english,
            Valid("Repetida"), Valid("Repetida"),
            Valid("com espaço"));

        Assert.That(errors, Has.Some.Contains("Gatilho: gatilho desconhecido"));
        Assert.That(errors, Has.Some.Contains("SemParametro: parametro desconhecido"));
        Assert.That(errors, Has.Some.Contains("PesquisaEmCrise: EnteredCrisis nunca acontece com Pesquisa"));
        Assert.That(errors, Has.Some.Contains("ParametroSobrando: QuietTurn nao usa parametro"));
        Assert.That(errors, Has.Some.Contains("Travessao: texto com travessao"));
        Assert.That(errors, Has.Some.Contains("Marcador: marcador desconhecido '{value}'"));
        Assert.That(errors, Has.Some.Contains("SemIngles: BodyEn vazio"));
        Assert.That(errors, Has.Some.Contains("Id repetido: Repetida"));
        Assert.That(errors, Has.Some.Contains("com espaço: Id so pode ter"));
    }

    [Test]
    public void MissingCoverage_ListsTriggersAndParametersWithoutStories()
    {
        var file = new NewsImportFormat.FileDto { Stories = new[] { Valid(), Valid("Calma", "QuietTurn", "") } };

        var missing = NewsImportFormat.MissingCoverage(file);

        Assert.That(missing, Has.Member("SharpDrop/Energia"));
        Assert.That(missing, Has.Member("Inauguration"));
        Assert.That(missing, Has.No.Member("SharpDrop/Saude"));
        Assert.That(missing, Has.No.Member("QuietTurn"));
        Assert.That(missing, Has.No.Member("EnteredCollapse/BemEstar"), "combinacao que nunca acontece");
    }
}
