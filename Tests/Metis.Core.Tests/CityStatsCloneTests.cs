namespace Metis.Core.Tests;

public class CityStatsCloneTests
{
    [Test]
    public void Clone_CopiesCurrentValues()
    {
        var stats = CityStatsTestFactory.Build();
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = 12f });

        var clone = stats.Clone();

        Assert.That(clone.GetValue(CityParameterType.Renda), Is.EqualTo(stats.GetValue(CityParameterType.Renda)));
    }

    [Test]
    public void Clone_ChangesDoNotLeakBetweenCopies()
    {
        var stats = CityStatsTestFactory.Build();
        var clone = stats.Clone();

        clone.ApplyModifier(new StatModifier { Parameter = CityParameterType.Saude, Amount = -20f });
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Energia, Amount = 5f });

        Assert.That(stats.GetValue(CityParameterType.Saude), Is.EqualTo(CityStatsTestFactory.NeutralValue));
        Assert.That(clone.GetValue(CityParameterType.Energia), Is.EqualTo(CityStatsTestFactory.NeutralValue));
    }

    [Test]
    public void Clone_KeepsLimitsCriticalLevelsAndResolution()
    {
        var interactions = new InteractionMatrix(50f, 1f, 25f, 75f, 1.6f, 0.5f, 0.6f, 0.1f);
        var stats = CityStatsTestFactory.Build(interactions, colapsoPenaltyPerParameter: 2f, customize: configs =>
        {
            var renda = configs[CityParameterType.Renda];
            renda.InitialValue = 20f;
            renda.CriticalLevel = 25f;
            renda.Deriva = 1.5f;
            configs[CityParameterType.Renda] = renda;
        });
        var clone = stats.Clone();

        clone.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = 500f });
        Assert.That(clone.GetValue(CityParameterType.Renda), Is.EqualTo(100f), "clamp no maximo deveria ter sido copiado");

        var original = stats.Clone();
        stats.ResolveTurn();
        original.ResolveTurn();
        foreach (CityParameterType parameter in System.Enum.GetValues(typeof(CityParameterType)))
            Assert.That(original.GetValue(parameter), Is.EqualTo(stats.GetValue(parameter)), parameter.ToString());
        Assert.That(stats.IsInCollapse(CityParameterType.Renda), Is.EqualTo(original.IsInCollapse(CityParameterType.Renda)));
    }

    [Test]
    public void Clone_DoesNotCarryEventSubscribers()
    {
        var stats = CityStatsTestFactory.Build();
        var notified = false;
        stats.OnParameterChanged += (_, _) => notified = true;

        stats.Clone().ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = 1f });

        Assert.That(notified, Is.False);
    }
}
