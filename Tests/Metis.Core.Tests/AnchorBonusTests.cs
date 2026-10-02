namespace Metis.Core.Tests;

public class AnchorBonusTests
{
    private static StatModifier BemEstar(float amount) => new StatModifier { Parameter = CityParameterType.BemEstar, Amount = amount };

    [Test]
    public void DirectWellBeingEffect_SurvivesTheTurnResolution()
    {
        var stats = CityStatsTestFactory.Build();
        stats.ApplyModifier(BemEstar(8f));

        stats.ResolveTurn();
        stats.ResolveTurn();

        Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(CityStatsTestFactory.NeutralValue + 8f));
        Assert.That(stats.AnchorBonus, Is.EqualTo(8f));
    }

    [Test]
    public void DirectWellBeingEffects_Accumulate_PositiveAndNegative()
    {
        var stats = CityStatsTestFactory.Build();

        stats.ApplyModifier(BemEstar(5f));
        stats.ApplyModifier(BemEstar(-2f));
        stats.RecomputeDerivedParameters();

        Assert.That(stats.AnchorBonus, Is.EqualTo(3f));
        Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(CityStatsTestFactory.NeutralValue + 3f));
    }

    [Test]
    public void BonusAddsToTheValueDerivedFromOtherParameters()
    {
        var stats = CityStatsTestFactory.Build();
        stats.ApplyModifier(BemEstar(4f));

        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Saude, Amount = 8f });
        stats.RecomputeDerivedParameters();

        Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(CityStatsTestFactory.NeutralValue + 2f + 4f), "Saude +8 sobe a media dos 4 positivos em 2");
    }

    [Test]
    public void CollapsePenalty_IsNotAccumulatedIntoTheBonus()
    {
        var stats = CityStatsTestFactory.Build(colapsoPenaltyPerParameter: 5f, customize: configs =>
        {
            var renda = configs[CityParameterType.Renda];
            renda.CriticalLevel = 60f;
            configs[CityParameterType.Renda] = renda;
        });

        stats.ResolveTurn();
        var afterFirst = stats.GetValue(CityParameterType.BemEstar);
        stats.ResolveTurn();

        Assert.That(stats.AnchorBonus, Is.EqualTo(0f));
        Assert.That(afterFirst, Is.EqualTo(CityStatsTestFactory.NeutralValue - 5f));
        Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(afterFirst), "penalidade de um colapso deveria valer igual todo turno, nao somar");
    }

    [Test]
    public void Clone_CarriesTheBonus()
    {
        var stats = CityStatsTestFactory.Build();
        stats.ApplyModifier(BemEstar(6f));

        var clone = stats.Clone();
        clone.ResolveTurn();

        Assert.That(clone.AnchorBonus, Is.EqualTo(6f));
        Assert.That(clone.GetValue(CityParameterType.BemEstar), Is.EqualTo(CityStatsTestFactory.NeutralValue + 6f));
    }
}
