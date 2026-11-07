using Metis.Simulator;

namespace Metis.Simulator.Tests;

public class NewsCoverageTests
{
    private static SimNewsFile News(IEnumerable<SimNewsStory> extra = null)
    {
        var stories = new List<SimNewsStory>
        {
            new SimNewsStory { Id = "Quiet", TriggerName = nameof(NewsTrigger.QuietTurn) },
            new SimNewsStory { Id = "Quiet2", TriggerName = nameof(NewsTrigger.QuietTurn) },
        };
        if (extra != null)
            stories.AddRange(extra);
        return new SimNewsFile { Stories = stories };
    }

    private static NewsRules Rules(BalanceData data)
    {
        return new NewsRules(data.BuildParameterConfigs(null), data.Interaction.ZonaCrise, data.Interaction.ZonaExcesso, data.TierThresholds);
    }

    private static (BalanceData Data, PhaseHistory History) Game()
    {
        var cards = Enumerable.Range(0, 12).Select(i => TestData.Card($"C{i}", (CityParameterType.Renda, 1f))).ToList();
        var data = TestData.Build(cards, new List<SimEvent>());
        var history = new PhaseHistory();
        PhaseSimulator.Run(TestData.Setup(data), SimulationRunner.CreatePolicy("gulosa"), new GameSeeds(1, 3), history);
        return (data, history);
    }

    [Test]
    public void Replay_PublishesOneEditionPerTurn_AndRepeatsTheSameWay()
    {
        var (data, history) = Game();

        var first = NewsCoverage.Replay(history, Rules(data), 42UL, News());
        var second = NewsCoverage.Replay(history, Rules(data), 42UL, News());

        Assert.That(first.Editions, Is.EqualTo(history.Turns.Count));
        Assert.That(second.Published, Is.EqualTo(first.Published));
    }

    [Test]
    public void Replay_CountsThePlayedCardStory()
    {
        var (data, history) = Game();
        var card = history.Turns[0].CardId;
        var news = News(new[] { new SimNewsStory { Id = "NCard", TriggerName = nameof(NewsTrigger.CardPlayed), Target = card } });

        var tally = NewsCoverage.Replay(history, Rules(data), 42UL, news);

        Assert.That(tally.Published["NCard"], Is.GreaterThan(0));
        Assert.That(tally.WithCard, Is.GreaterThan(0));
    }
}
