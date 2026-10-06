using Metis.Simulator;
using Metis.Simulator.Policies;

namespace Metis.Simulator.Tests;

public class PolicyTests
{
    private static DecisionContext Context(BalanceData data, CityStats stats, params SimCard[] playable)
    {
        return new DecisionContext { Data = data, Stats = stats, PlayableCards = playable, TurnIndex = 1, Random = new Random(0) };
    }

    private static CityStats Stats(BalanceData data)
    {
        return new CityStats(data.BuildParameterConfigs(null), data.BuildInteractionMatrix(), data.Interaction.PenalidadeColapso);
    }

    [Test]
    public void Greedy_PicksCardThatLeavesTheCityInBetterShape()
    {
        var bad = TestData.Card("Ruim", (CityParameterType.Saude, -10f));
        var good = TestData.Card("Boa", (CityParameterType.Saude, 10f));
        var data = TestData.Build(cards: new[] { bad, good });

        var chosen = new GreedyPolicy().Choose(Context(data, Stats(data), bad, good));

        Assert.That(chosen, Is.SameAs(good));
    }

    [Test]
    public void Greedy_DoesNotChangeTheRealCityWhileEvaluating()
    {
        var card = TestData.Card("Boa", (CityParameterType.Saude, 10f));
        var data = TestData.Build(cards: new[] { card });
        var stats = Stats(data);

        new GreedyPolicy().Choose(Context(data, stats, card));

        Assert.That(stats.GetValue(CityParameterType.Saude), Is.EqualTo(TestData.NeutralValue));
    }

    [Test]
    public void Balanced_PrioritizesTheParameterClosestToCritical()
    {
        var parameters = TestData.NeutralParameters();
        var energia = parameters.First(config => config.Parameter == CityParameterType.Energia);
        parameters.Remove(energia);
        energia.InitialValue = 15f;
        parameters.Add(energia);
        var data = TestData.Build(parameters: parameters);

        var saude = TestData.Card("Saude", (CityParameterType.Saude, 8f));
        var energiaCard = TestData.Card("Energia", (CityParameterType.Energia, 4f));

        var chosen = new BalancedPolicy().Choose(Context(data, Stats(data), saude, energiaCard));

        Assert.That(chosen, Is.SameAs(energiaCard), "Energia a 5 do critico vale mais que o dobro de efeito em Saude a 40 do critico");
    }

    [Test]
    public void StateScore_GameOverIsWorseThanAnyLivingCity()
    {
        var data = TestData.Build();
        var dead = Stats(data);
        dead.ApplyModifier(new StatModifier { Parameter = CityParameterType.BemEstar, Amount = -100f });
        var collapsed = Stats(data);
        foreach (var parameter in PhaseSimulator.Parameters)
            collapsed.ApplyModifier(new StatModifier { Parameter = parameter, Amount = -39f });

        Assert.That(StateScore.Evaluate(dead, data), Is.LessThan(StateScore.Evaluate(collapsed, data)));
    }
}
