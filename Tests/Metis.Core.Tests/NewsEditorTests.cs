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
