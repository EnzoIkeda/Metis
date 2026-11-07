using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Metis.Simulator;

// Campo que nao se aplica vem vazio no arquivo, por isso os enums sao lidos como texto.
public sealed class SimNewsStory : INewsStoryDefinition
{
    [JsonPropertyName("Id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("Trigger")]
    public string TriggerName { get; init; } = "";

    [JsonPropertyName("Parameter")]
    public string ParameterName { get; init; } = "";

    public string Target { get; init; } = "";

    [JsonIgnore]
    public NewsTrigger Trigger => Enum.Parse<NewsTrigger>(TriggerName);

    [JsonIgnore]
    public CityParameterType Parameter => Enum.TryParse<CityParameterType>(ParameterName, out var parameter) ? parameter : default;

    [JsonIgnore]
    public string TargetId => Target ?? "";
}

public sealed class SimNewsColumn : INewsColumnDefinition
{
    public string Id { get; init; } = "";

    [JsonPropertyName("Kind")]
    public string KindName { get; init; } = "";

    [JsonPropertyName("Citizen")]
    public string CitizenName { get; init; } = "";

    [JsonPropertyName("Mood")]
    public string MoodName { get; init; } = "";

    [JsonIgnore]
    public NewsColumnKind Kind => Enum.Parse<NewsColumnKind>(KindName);

    [JsonIgnore]
    public NewsCitizen Citizen => Enum.TryParse<NewsCitizen>(CitizenName, out var citizen) ? citizen : default;

    [JsonIgnore]
    public NewsMoodBand Mood => Enum.TryParse<NewsMoodBand>(MoodName, out var mood) ? mood : default;
}

public sealed class SimNewsFile
{
    public List<SimNewsStory> Stories { get; init; } = new();
    public List<SimNewsColumn> Columns { get; init; } = new();

    public static SimNewsFile Load(string path)
    {
        return JsonSerializer.Deserialize<SimNewsFile>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Jornal vazio: '{path}'.");
    }
}

// Totais de uma combinacao de limiares, somados entre as partidas.
public sealed class NewsTally
{
    public int Games, Editions, Items, Strong, Quiet, RepeatedItems, SameHeadlineInARow, RepeatedQuotes, RepeatedColumns, WithCard, WithEvent;
    public readonly Dictionary<string, int> Headlines = new();
    public readonly Dictionary<string, int> Published = new();
    public readonly Dictionary<string, int> Repeats = new();

    public void Merge(NewsTally other)
    {
        Games += other.Games;
        Editions += other.Editions;
        Items += other.Items;
        Strong += other.Strong;
        Quiet += other.Quiet;
        RepeatedItems += other.RepeatedItems;
        SameHeadlineInARow += other.SameHeadlineInARow;
        RepeatedQuotes += other.RepeatedQuotes;
        RepeatedColumns += other.RepeatedColumns;
        WithCard += other.WithCard;
        WithEvent += other.WithEvent;
        Add(Headlines, other.Headlines);
        Add(Published, other.Published);
        Add(Repeats, other.Repeats);
    }

    private static void Add(Dictionary<string, int> target, Dictionary<string, int> source)
    {
        foreach (var entry in source)
            target[entry.Key] = target.GetValueOrDefault(entry.Key) + entry.Value;
    }
}

// Passa as partidas simuladas pela mesma redacao do jogo, para ver o que sai no jornal em cada par de limiares.
public static class NewsCoverage
{
    public static readonly float[] SharpChanges = { 4f, 5f, 6f, 7f, 8f, 10f, 12f };
    public static readonly float[] TrendChanges = { 6f, 8f, 10f, 12f };

    public static void Run(SimulationOptions options, TextWriter output)
    {
        if (string.IsNullOrEmpty(options.NewsPath))
            throw new ArgumentException("O modo jornal precisa de --news <jornal.json>.");
        var data = BalanceData.Load(options.DataPath);
        var news = SimNewsFile.Load(options.NewsPath);
        var jobs = SimulationRunner.BuildJobs(data, options);
        var keys = SharpChanges.SelectMany(sharp => TrendChanges.Select(trend => (sharp, trend))).ToList();
        var totals = keys.ToDictionary(key => key, _ => new NewsTally());
        var total = jobs.Count * options.GamesPerJob;
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.MaxThreads ?? Environment.ProcessorCount };

        Parallel.For(0, total, parallel, index =>
        {
            var job = jobs[index / options.GamesPerJob];
            var seeds = new GameSeeds(options.BaseSeed, index % options.GamesPerJob);
            var history = new PhaseHistory();
            PhaseSimulator.Run(job.Setup, job.Policy, seeds, history);
            var seed = ((ulong)(uint)seeds.Hand << 32) | (uint)seeds.Events;
            foreach (var key in keys)
            {
                var rules = new NewsRules(data.BuildParameterConfigs(job.Setup.Preset), data.Interaction.ZonaCrise, data.Interaction.ZonaExcesso, data.TierThresholds, key.sharp, key.trend);
                var tally = Replay(history, rules, seed, news);
                lock (totals[key])
                    totals[key].Merge(tally);
            }
        });

        Directory.CreateDirectory(options.OutputDirectory);
        WriteThresholds(Path.Combine(options.OutputDirectory, "jornal_limiares.csv"), totals);
        var reference = (NewsRules.DefaultSharpChange, NewsRules.DefaultTrendChange);
        var coverage = WriteCoverage(Path.Combine(options.OutputDirectory, "jornal_cobertura.csv"), news, totals[reference]);
        output.WriteLine($"{total} partidas, limiares de referencia {reference}: {coverage} noticias nunca publicadas de {news.Stories.Count}.");
        output.WriteLine($"Saida em '{options.OutputDirectory}'.");
    }

    public static NewsTally Replay(PhaseHistory history, NewsRules rules, ulong seed, SimNewsFile news)
    {
        var archive = new NewsArchive(seed);
        var tally = new NewsTally { Games = 1 };
        foreach (var turn in history.Turns)
        {
            var edition = NewsEditor.EditionForTurn(history, turn, rules, archive, news.Stories, news.Columns);
            Count(tally, edition, archive.Editions);
            archive.Add(edition);
        }
        return tally;
    }

    private static void Count(NewsTally tally, NewsEdition edition, IReadOnlyList<NewsEdition> previous)
    {
        var recent = previous.Skip(Math.Max(0, previous.Count - NewsEditor.RecentEditions)).ToList();
        var recentStories = recent.SelectMany(old => old.Items.Select(item => item.StoryId)).ToHashSet();
        tally.Editions++;
        tally.Items += edition.Items.Count;
        if (edition.Strong)
            tally.Strong++;
        if (edition.Items.Count > 0 && edition.Items[0].Trigger == NewsTrigger.QuietTurn)
            tally.Quiet++;
        if (edition.Items.Count > 0)
        {
            var headline = edition.Items[0].Trigger.ToString();
            tally.Headlines[headline] = tally.Headlines.GetValueOrDefault(headline) + 1;
            if (previous.Count > 0 && previous[^1].Items.Count > 0 && previous[^1].Items[0].StoryId == edition.Items[0].StoryId)
                tally.SameHeadlineInARow++;
        }
        foreach (var item in edition.Items)
        {
            tally.Published[item.StoryId] = tally.Published.GetValueOrDefault(item.StoryId) + 1;
            if (recentStories.Contains(item.StoryId))
            {
                tally.RepeatedItems++;
                tally.Repeats[item.StoryId] = tally.Repeats.GetValueOrDefault(item.StoryId) + 1;
            }
        }
        if (edition.Items.Exists(item => item.Trigger == NewsTrigger.CardPlayed))
            tally.WithCard++;
        if (edition.Items.Exists(item => item.Trigger == NewsTrigger.EventHappened))
            tally.WithEvent++;
        if (recent.Exists(old => old.QuoteId == edition.QuoteId))
            tally.RepeatedQuotes++;
        if (recent.Exists(old => old.ColumnId == edition.ColumnId))
            tally.RepeatedColumns++;
    }

    private static void WriteThresholds(string path, Dictionary<(float sharp, float trend), NewsTally> totals)
    {
        var triggers = Enum.GetNames(typeof(NewsTrigger));
        var text = new StringBuilder("alta_queda,tendencia,edicoes,itens_por_edicao,turno_tranquilo,forte,item_repetido,manchete_igual_seguida,fala_repetida,secao_repetida,com_carta,com_evento," + string.Join(",", triggers.Select(name => "manchete_" + name)) + "\n");
        foreach (var entry in totals.OrderBy(entry => entry.Key.sharp).ThenBy(entry => entry.Key.trend))
        {
            var tally = entry.Value;
            double Rate(int count) => count / (double)Math.Max(1, tally.Editions);
            var columns = new List<string>
            {
                F(entry.Key.sharp), F(entry.Key.trend), tally.Editions.ToString(CultureInfo.InvariantCulture),
                F(Rate(tally.Items)), F(Rate(tally.Quiet)), F(Rate(tally.Strong)),
                F(tally.RepeatedItems / (double)Math.Max(1, tally.Items)), F(Rate(tally.SameHeadlineInARow)), F(Rate(tally.RepeatedQuotes)), F(Rate(tally.RepeatedColumns)), F(Rate(tally.WithCard)), F(Rate(tally.WithEvent)),
            };
            columns.AddRange(triggers.Select(name => F(Rate(tally.Headlines.GetValueOrDefault(name)))));
            text.Append(string.Join(",", columns)).Append('\n');
        }
        File.WriteAllText(path, text.ToString());
    }

    private static int WriteCoverage(string path, SimNewsFile news, NewsTally tally)
    {
        var text = new StringBuilder("noticia,gatilho,publicada,repetida\n");
        var never = 0;
        foreach (var story in news.Stories)
        {
            var published = tally.Published.GetValueOrDefault(story.Id);
            if (published == 0 && story.Trigger != NewsTrigger.Inauguration)
                never++;
            text.Append(string.Join(",", story.Id, story.Trigger, published, tally.Repeats.GetValueOrDefault(story.Id))).Append('\n');
        }
        File.WriteAllText(path, text.ToString());
        return never;
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
