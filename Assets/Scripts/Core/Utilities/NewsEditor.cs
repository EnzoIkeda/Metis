using System;
using System.Collections.Generic;

// O que faz uma noticia sair no jornal. So acrescentar valores no fim.
public enum NewsTrigger
{
    QuietTurn,
    Inauguration,
    EnteredCrisis,
    EnteredExcess,
    EnteredCollapse,
    LeftCollapse,
    SharpRise,
    SharpDrop,
    ResearchTier,
    Crowding,
}

// Regras de uma noticia, sem o texto, pra selecao rodar igual no jogo e nos testes.
public interface INewsStoryDefinition
{
    string Id { get; }
    NewsTrigger Trigger { get; }

    // So vale nos gatilhos que dependem de um parametro.
    CityParameterType Parameter { get; }
}

// Algo que aconteceu no turno e pode virar noticia.
public struct NewsSignal
{
    public NewsTrigger Trigger;
    public CityParameterType Parameter;
    public float Value;
    public float Delta;
    public int Priority;
}

// Uma noticia publicada, com os numeros que o texto cita; o texto sai do asset na hora de mostrar.
[Serializable]
public class NewsItem
{
    public string StoryId = string.Empty;
    public NewsTrigger Trigger;
    public CityParameterType Parameter;
    public float Value;
    public float Delta;

    // Maior parcela da variacao no turno, pra dizer por que o parametro mudou.
    public bool HasCause;
    public ContributionSource CauseSource;
    public CityParameterType CauseFrom;
}

// Edicao de um turno: a primeira noticia e a manchete, as outras sao notas.
[Serializable]
public class NewsEdition
{
    public int TurnIndex;
    public bool Strong;
    public float Wellbeing;
    public float WellbeingDelta;
    public List<NewsItem> Items = new List<NewsItem>();
}

// Limiares do que vira noticia, tirados da configuracao da fase.
public sealed class NewsRules
{
    // Variacao no turno, no indice, a partir da qual a alta ou a queda vira noticia.
    public const float DefaultSharpChange = 6f;

    private readonly Dictionary<CityParameterType, CityParameterConfig> _configs = new Dictionary<CityParameterType, CityParameterConfig>();

    public NewsRules(IEnumerable<CityParameterConfig> parameters, float crisisThreshold, float excessThreshold, IReadOnlyList<float> tierThresholds, float sharpChange = DefaultSharpChange)
    {
        foreach (var config in parameters)
            _configs[config.Parameter] = config;
        CrisisThreshold = crisisThreshold;
        ExcessThreshold = excessThreshold;
        TierThresholds = tierThresholds ?? Array.Empty<float>();
        SharpChange = sharpChange;
    }

    public float CrisisThreshold { get; }
    public float ExcessThreshold { get; }
    public IReadOnlyList<float> TierThresholds { get; }
    public float SharpChange { get; }

    public static bool UsesParameter(NewsTrigger trigger)
    {
        switch (trigger)
        {
            case NewsTrigger.EnteredCrisis:
            case NewsTrigger.EnteredExcess:
            case NewsTrigger.EnteredCollapse:
            case NewsTrigger.LeftCollapse:
            case NewsTrigger.SharpRise:
            case NewsTrigger.SharpDrop:
                return true;
            default:
                return false;
        }
    }

    // Combinacoes que podem acontecer: Pesquisa nao tem crise nem Colapso, e o Bem-estar critico e fim de jogo.
    public static bool Applies(NewsTrigger trigger, CityParameterType parameter)
    {
        switch (trigger)
        {
            case NewsTrigger.EnteredCrisis:
            case NewsTrigger.EnteredExcess:
                return parameter != CityParameterType.Pesquisa;
            case NewsTrigger.EnteredCollapse:
            case NewsTrigger.LeftCollapse:
                return parameter != CityParameterType.Pesquisa && parameter != CityStats.AnchorParameter;
            case NewsTrigger.SharpRise:
            case NewsTrigger.SharpDrop:
                return true;
            default:
                return false;
        }
    }

    // Manchete forte faz o selo do jornal pulsar ate o jogador ler.
    public static bool IsStrong(NewsTrigger trigger)
    {
        return trigger == NewsTrigger.EnteredCollapse
            || trigger == NewsTrigger.LeftCollapse
            || trigger == NewsTrigger.EnteredCrisis
            || trigger == NewsTrigger.ResearchTier;
    }

    public static int BasePriority(NewsTrigger trigger)
    {
        switch (trigger)
        {
            case NewsTrigger.EnteredCollapse: return 100;
            case NewsTrigger.LeftCollapse: return 85;
            case NewsTrigger.ResearchTier: return 80;
            case NewsTrigger.EnteredCrisis: return 70;
            case NewsTrigger.SharpDrop: return 55;
            case NewsTrigger.SharpRise: return 50;
            case NewsTrigger.EnteredExcess: return 45;
            case NewsTrigger.Crowding: return 40;
            default: return 0;
        }
    }

    public bool IsCollapsed(CityParameterType parameter, float value)
    {
        if (Applies(NewsTrigger.EnteredCollapse, parameter) == false || _configs.TryGetValue(parameter, out var config) == false)
            return false;
        return config.CriticalLevel >= 0f && value <= config.CriticalLevel;
    }

    // Metade da escala de Populacao, de onde o adensamento comeca a tirar do Bem-estar.
    public float CrowdingBaseline
    {
        get
        {
            return _configs.TryGetValue(CityParameterType.Populacao, out var config)
                ? (config.MinValue + config.MaxValue) / 2f
                : 50f;
        }
    }
}

// Monta as edicoes do jornal so lendo o que aconteceu: nenhuma conta do jogo muda.
public static class NewsEditor
{
    // Bem-estar e o que a populacao mais sente, entao pesa mais na manchete.
    public const int AnchorPriorityBonus = 15;

    public const int MaxItems = 3;

    // Noticia publicada nestas ultimas edicoes so volta se nao houver outra.
    public const int RecentEditions = 5;

    // Um sinal por parametro (o mais importante) e os sinais sem parametro, do mais ao menos importante.
    public static List<NewsSignal> DetectSignals(float[] before, float[] after, NewsRules rules)
    {
        var signals = new List<NewsSignal>();
        if (before == null || after == null || before.Length != PhaseHistory.Parameters.Length || after.Length != before.Length)
            return signals;

        foreach (var parameter in PhaseHistory.Parameters)
        {
            var from = PhaseHistory.Read(before, parameter);
            var to = PhaseHistory.Read(after, parameter);
            var candidates = new List<NewsTrigger>();

            if (rules.IsCollapsed(parameter, from) == false && rules.IsCollapsed(parameter, to))
                candidates.Add(NewsTrigger.EnteredCollapse);
            if (rules.IsCollapsed(parameter, from) && rules.IsCollapsed(parameter, to) == false)
                candidates.Add(NewsTrigger.LeftCollapse);
            if (NewsRules.Applies(NewsTrigger.EnteredCrisis, parameter) && from > rules.CrisisThreshold && to <= rules.CrisisThreshold)
                candidates.Add(NewsTrigger.EnteredCrisis);
            if (NewsRules.Applies(NewsTrigger.EnteredExcess, parameter) && from < rules.ExcessThreshold && to >= rules.ExcessThreshold)
                candidates.Add(NewsTrigger.EnteredExcess);
            if (to - from >= rules.SharpChange)
                candidates.Add(NewsTrigger.SharpRise);
            if (from - to >= rules.SharpChange)
                candidates.Add(NewsTrigger.SharpDrop);

            if (candidates.Count == 0)
                continue;
            var best = candidates[0];
            foreach (var candidate in candidates)
            {
                if (NewsRules.BasePriority(candidate) > NewsRules.BasePriority(best))
                    best = candidate;
            }
            var priority = NewsRules.BasePriority(best) + (parameter == CityStats.AnchorParameter ? AnchorPriorityBonus : 0);
            signals.Add(new NewsSignal { Trigger = best, Parameter = parameter, Value = to, Delta = to - from, Priority = priority });
        }

        var pesquisaFrom = PhaseHistory.Read(before, CityParameterType.Pesquisa);
        var pesquisaTo = PhaseHistory.Read(after, CityParameterType.Pesquisa);
        foreach (var threshold in rules.TierThresholds)
        {
            if (pesquisaFrom < threshold && pesquisaTo >= threshold)
            {
                signals.Add(Signal(NewsTrigger.ResearchTier, CityParameterType.Pesquisa, pesquisaTo, pesquisaTo - pesquisaFrom));
                break;
            }
        }

        var populacaoFrom = PhaseHistory.Read(before, CityParameterType.Populacao);
        var populacaoTo = PhaseHistory.Read(after, CityParameterType.Populacao);
        if (populacaoFrom <= rules.CrowdingBaseline && populacaoTo > rules.CrowdingBaseline)
            signals.Add(Signal(NewsTrigger.Crowding, CityParameterType.Populacao, populacaoTo, populacaoTo - populacaoFrom));

        // Ordem estavel: prioridade, depois o tamanho da variacao, depois a ordem do enum.
        signals.Sort((a, b) =>
        {
            var byPriority = b.Priority.CompareTo(a.Priority);
            if (byPriority != 0)
                return byPriority;
            var byDelta = Math.Abs(b.Delta).CompareTo(Math.Abs(a.Delta));
            return byDelta != 0 ? byDelta : a.Parameter.CompareTo(b.Parameter);
        });
        return signals;
    }

    private static NewsSignal Signal(NewsTrigger trigger, CityParameterType parameter, float value, float delta)
    {
        return new NewsSignal { Trigger = trigger, Parameter = parameter, Value = value, Delta = delta, Priority = NewsRules.BasePriority(trigger) };
    }

    // Uma noticia por sinal, ate o limite; sem nenhuma, a edicao sai com o turno tranquilo.
    public static NewsEdition Compose<TStory>(int turnIndex, IReadOnlyList<NewsSignal> signals, IReadOnlyList<TStory> stories,
        IReadOnlyList<NewsEdition> previous, Random random, float wellbeing, float wellbeingDelta)
        where TStory : class, INewsStoryDefinition
    {
        var edition = new NewsEdition { TurnIndex = turnIndex, Wellbeing = wellbeing, WellbeingDelta = wellbeingDelta };
        var recent = RecentStoryIds(previous);

        foreach (var signal in signals)
        {
            if (edition.Items.Count >= MaxItems)
                break;
            var story = Pick(stories, signal.Trigger, signal.Parameter, recent, edition, random);
            if (story == null)
                continue;
            edition.Items.Add(new NewsItem { StoryId = story.Id, Trigger = signal.Trigger, Parameter = signal.Parameter, Value = signal.Value, Delta = signal.Delta });
        }

        if (edition.Items.Count == 0)
        {
            var quiet = Pick(stories, NewsTrigger.QuietTurn, default, recent, edition, random);
            if (quiet != null)
                edition.Items.Add(new NewsItem { StoryId = quiet.Id, Trigger = NewsTrigger.QuietTurn, Parameter = CityStats.AnchorParameter, Value = wellbeing, Delta = wellbeingDelta });
        }

        edition.Strong = edition.Items.Count > 0 && NewsRules.IsStrong(edition.Items[0].Trigger);
        return edition;
    }

    // Edicao de posse, antes do primeiro turno.
    public static NewsEdition Inauguration<TStory>(IReadOnlyList<TStory> stories, Random random, float wellbeing)
        where TStory : class, INewsStoryDefinition
    {
        var edition = new NewsEdition { TurnIndex = 0, Wellbeing = wellbeing };
        var story = Pick(stories, NewsTrigger.Inauguration, default, new HashSet<string>(), edition, random);
        if (story != null)
            edition.Items.Add(new NewsItem { StoryId = story.Id, Trigger = NewsTrigger.Inauguration, Parameter = CityStats.AnchorParameter, Value = wellbeing });
        return edition;
    }

    private static HashSet<string> RecentStoryIds(IReadOnlyList<NewsEdition> previous)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (previous == null)
            return ids;
        for (var i = Math.Max(0, previous.Count - RecentEditions); i < previous.Count; i++)
        {
            foreach (var item in previous[i].Items)
                ids.Add(item.StoryId);
        }
        return ids;
    }

    private static TStory Pick<TStory>(IReadOnlyList<TStory> stories, NewsTrigger trigger, CityParameterType parameter,
        HashSet<string> recent, NewsEdition edition, Random random)
        where TStory : class, INewsStoryDefinition
    {
        var fresh = new List<TStory>();
        var any = new List<TStory>();
        foreach (var story in stories)
        {
            if (story == null || story.Trigger != trigger)
                continue;
            if (NewsRules.UsesParameter(trigger) && story.Parameter != parameter)
                continue;
            if (edition.Items.Exists(item => item.StoryId == story.Id))
                continue;
            any.Add(story);
            if (recent.Contains(story.Id) == false)
                fresh.Add(story);
        }

        var pool = fresh.Count > 0 ? fresh : any;
        return pool.Count == 0 ? null : pool[random.Next(pool.Count)];
    }

    // Maior parcela do turno no mesmo sentido da variacao; o limite da escala nao conta como causa.
    public static bool TryMainCause(TurnBreakdown breakdown, CityParameterType parameter, float delta, out ContributionSource source, out CityParameterType from)
    {
        source = default;
        from = default;
        if (breakdown == null || delta == 0f || parameter == CityStats.AnchorParameter)
            return false;

        var grouped = new Dictionary<(ContributionSource, CityParameterType), float>();
        foreach (var contribution in breakdown.For(parameter))
        {
            if (contribution.Source == ContributionSource.Clamp)
                continue;
            var key = (contribution.Source, contribution.Source == ContributionSource.Interaction ? contribution.From : default(CityParameterType));
            grouped.TryGetValue(key, out var sum);
            grouped[key] = sum + contribution.Amount;
        }

        var best = 0f;
        var found = false;
        foreach (var entry in grouped)
        {
            if (Math.Sign(entry.Value) != Math.Sign(delta) || Math.Abs(entry.Value) <= Math.Abs(best))
                continue;
            best = entry.Value;
            (source, from) = entry.Key;
            found = true;
        }
        return found;
    }

    // Gerador da edicao, pela semente da fase e o turno: reabrir ou continuar a rodada repete a mesma edicao.
    public static SeededRandom EditionRandom(ulong phaseSeed, int turnIndex)
    {
        unchecked
        {
            return new SeededRandom(phaseSeed ^ ((ulong)(uint)turnIndex * 0x9E3779B97F4A7C15UL));
        }
    }
}

// Edicoes da fase e o que o jogador ja leu, salvos junto com a fase.
public class NewsArchive
{
    private readonly List<NewsEdition> _editions = new List<NewsEdition>();

    public NewsArchive(ulong seed)
    {
        Seed = seed;
    }

    public NewsArchive(ulong seed, IEnumerable<NewsEdition> editions, int lastReadTurn)
        : this(seed)
    {
        if (editions != null)
            _editions.AddRange(editions);
        LastReadTurn = lastReadTurn;
    }

    public ulong Seed { get; }

    public IReadOnlyList<NewsEdition> Editions => _editions;

    // Turno da ultima edicao que o jogador abriu; -1 quando nao leu nenhuma.
    public int LastReadTurn { get; private set; } = -1;

    public NewsEdition Latest => _editions.Count > 0 ? _editions[_editions.Count - 1] : null;

    public bool HasUnread => Latest != null && Latest.TurnIndex > LastReadTurn;

    public bool HasStrongUnread
    {
        get
        {
            foreach (var edition in _editions)
            {
                if (edition.TurnIndex > LastReadTurn && edition.Strong)
                    return true;
            }
            return false;
        }
    }

    public event Action Changed;

    // Uma edicao por turno; a de um turno repetido substitui a anterior.
    public void Add(NewsEdition edition)
    {
        _editions.RemoveAll(existing => existing.TurnIndex == edition.TurnIndex);
        _editions.Add(edition);
        _editions.Sort((a, b) => a.TurnIndex.CompareTo(b.TurnIndex));
        Changed?.Invoke();
    }

    public void MarkAllRead()
    {
        if (Latest == null || LastReadTurn >= Latest.TurnIndex)
            return;
        LastReadTurn = Latest.TurnIndex;
        Changed?.Invoke();
    }
}
