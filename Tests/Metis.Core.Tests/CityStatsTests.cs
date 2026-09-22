namespace Metis.Core.Tests;

public class CityStatsTests
{
    [Test]
    public void Constructor_ClampsInitialValueToRange()
    {
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            var cfg = configs[CityParameterType.Renda];
            cfg.InitialValue = 150f;
            cfg.MaxValue = 100f;
            configs[CityParameterType.Renda] = cfg;
        });

        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(100f));
    }

    [Test]
    public void ApplyModifier_ClampsResultToRange()
    {
        var stats = CityStatsTestFactory.Build();

        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = -1000f });

        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(0f));
    }

    [Test]
    public void ApplyModifier_UnknownParameter_IsIgnoredSilently()
    {
        var stats = CityStatsTestFactory.Build();

        Assert.DoesNotThrow(() =>
            stats.ApplyModifier(new StatModifier { Parameter = (CityParameterType)999, Amount = 10f }));
    }

    [Test]
    public void IsAnchorCritical_OnlyBemEstarCrossingCriticalCounts()
    {
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            var bemEstar = configs[CityParameterType.BemEstar];
            bemEstar.InitialValue = 10f;
            bemEstar.CriticalLevel = 20f;
            configs[CityParameterType.BemEstar] = bemEstar;

            var saude = configs[CityParameterType.Saude];
            saude.InitialValue = 10f;
            saude.CriticalLevel = 20f;
            configs[CityParameterType.Saude] = saude;
        });

        Assert.That(stats.IsAnchorCritical(), Is.True);
        Assert.That(stats.IsCritical(CityParameterType.Saude), Is.True);
        Assert.That(stats.IsInCollapse(CityParameterType.BemEstar), Is.False, "a ancora nunca entra em colapso, ela termina o jogo");
    }

    [Test]
    public void IsInCollapse_ExemptsPesquisaEvenWhenCritical()
    {
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            var pesquisa = configs[CityParameterType.Pesquisa];
            pesquisa.InitialValue = 5f;
            pesquisa.CriticalLevel = 20f;
            configs[CityParameterType.Pesquisa] = pesquisa;
        });

        Assert.That(stats.IsCritical(CityParameterType.Pesquisa), Is.True);
        Assert.That(stats.IsInCollapse(CityParameterType.Pesquisa), Is.False);
    }

    [Test]
    public void CountCollapsedParameters_CountsOnlyNonAnchorNonPesquisaCritical()
    {
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            foreach (var parameter in new[] { CityParameterType.Saude, CityParameterType.Seguranca, CityParameterType.Pesquisa, CityParameterType.BemEstar })
            {
                var cfg = configs[parameter];
                cfg.InitialValue = 5f;
                cfg.CriticalLevel = 20f;
                configs[parameter] = cfg;
            }
        });

        // Saude e Seguranca colapsam; Pesquisa e isenta; BemEstar e a ancora, nao conta como colapso.
        Assert.That(stats.CountCollapsedParameters(), Is.EqualTo(2));
    }

    [Test]
    public void CriticalLevel_Negative_MeansNeverCritical()
    {
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            var cfg = configs[CityParameterType.Renda];
            cfg.InitialValue = 0f;
            cfg.CriticalLevel = -1f;
            configs[CityParameterType.Renda] = cfg;
        });

        Assert.That(stats.IsCritical(CityParameterType.Renda), Is.False);
    }

    [Test]
    public void OnGameOver_FiresOnce_OnlyWhenAnchorCrossesCritical()
    {
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            var bemEstar = configs[CityParameterType.BemEstar];
            bemEstar.CriticalLevel = 20f;
            configs[CityParameterType.BemEstar] = bemEstar;

            var saude = configs[CityParameterType.Saude];
            saude.CriticalLevel = 20f;
            configs[CityParameterType.Saude] = saude;
        });

        var gameOverCount = 0;
        stats.OnGameOver += () => gameOverCount++;

        // Saude cruzando o critico nao deve disparar Game Over.
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Saude, Amount = -100f });
        Assert.That(gameOverCount, Is.EqualTo(0));

        // BemEstar cruzando dispara, mas so uma vez mesmo se aplicado de novo.
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.BemEstar, Amount = -100f });
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.BemEstar, Amount = -1f });

        Assert.That(gameOverCount, Is.EqualTo(1));
    }

    [Test]
    public void RecomputeDerivedParameters_AveragesThePositivesMinusPopulationOvershoot()
    {
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            void SetInitial(CityParameterType parameter, float value)
            {
                var cfg = configs[parameter];
                cfg.InitialValue = value;
                configs[parameter] = cfg;
            }

            SetInitial(CityParameterType.Mobilidade, 80f);
            SetInitial(CityParameterType.Saude, 60f);
            SetInitial(CityParameterType.Seguranca, 40f);
            SetInitial(CityParameterType.Sustentabilidade, 20f);
            // Populacao com range 0-100, baseline 50; 70 excede o baseline em 20.
            SetInitial(CityParameterType.Populacao, 70f);
        });

        stats.RecomputeDerivedParameters();

        // Media dos 4 positivos = (80+60+40+20)/4 = 50; excedente de populacao = 70-50 = 20.
        Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(30f).Within(0.001f));
    }

    [Test]
    public void RecomputeDerivedParameters_PopulationBelowBaseline_DoesNotBoostBemEstar()
    {
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            var populacao = configs[CityParameterType.Populacao];
            populacao.InitialValue = 10f; // abaixo do baseline (50), nao deve virar bonus negativo de excedente
            configs[CityParameterType.Populacao] = populacao;
        });

        stats.RecomputeDerivedParameters();

        // Os 4 positivos estao todos no neutro (50) por padrao: media = 50, excedente = max(0, 10-50) = 0.
        Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(50f).Within(0.001f));
    }

    [Test]
    public void ResolveTurn_AppliesCollapsePenaltyToAnchor_ProportionalToCollapsedCountBeforeResolve()
    {
        // Renda e Energia nao entram na formula de recomputo de BemEstar, entao colapsa-los isola o efeito so na penalidade.
        var stats = CityStatsTestFactory.Build(colapsoPenaltyPerParameter: 5f, customize: configs =>
        {
            foreach (var parameter in new[] { CityParameterType.Renda, CityParameterType.Energia })
            {
                var cfg = configs[parameter];
                cfg.InitialValue = 5f;
                cfg.CriticalLevel = 20f;
                configs[parameter] = cfg;
            }
        });

        var bemEstarAntes = stats.GetValue(CityParameterType.BemEstar);
        stats.ResolveTurn();

        // Recomputo de BemEstar roda antes da penalidade e nao muda nada aqui (os 4 positivos seguem neutros); a unica diferenca esperada e a penalidade: -5 * 2 parametros colapsados = -10.
        Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(bemEstarAntes - 10f).Within(0.001f));
    }

    [Test]
    public void ResolveTurn_NoCollapse_AppliesNoPenalty()
    {
        var stats = CityStatsTestFactory.Build(colapsoPenaltyPerParameter: 5f);

        var bemEstarAntes = stats.GetValue(CityParameterType.BemEstar);
        stats.ResolveTurn();

        Assert.That(stats.GetValue(CityParameterType.BemEstar), Is.EqualTo(bemEstarAntes).Within(0.001f));
    }
}
