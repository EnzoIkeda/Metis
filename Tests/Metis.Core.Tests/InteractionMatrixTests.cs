namespace Metis.Core.Tests;

// Valores esperados calculados a mao; cada teste isola um termo, com o resto no valor neutro (contribuicao zero).
// efeito = soma_fontes[(valorFonte - valorNeutro) / valorNeutro * coeficiente * fatorZona]
// delta = efeito * escalaGlobal + deriva + correcaoExcesso
public class InteractionMatrixTests
{
    private const float ValorNeutro = 50f;
    private const float ZonaCrise = 25f;
    private const float ZonaExcesso = 75f;

    private static InteractionMatrix BuildMatrix(
        float escalaGlobal = 1f,
        float multiplicadorPressaoCrise = 1f,
        float multiplicadorApoioCrise = 1f,
        float multiplicadorApoioExcesso = 1f,
        float derivaCorrecaoExcesso = 0f)
    {
        return new InteractionMatrix(
            ValorNeutro,
            escalaGlobal,
            ZonaCrise,
            ZonaExcesso,
            multiplicadorPressaoCrise,
            multiplicadorApoioCrise,
            multiplicadorApoioExcesso,
            derivaCorrecaoExcesso);
    }

    [Test]
    public void Resolve_AllParametersAtNeutroWithNoDeriva_LeavesEveryTargetUnchanged()
    {
        var matrix = BuildMatrix();
        var stats = CityStatsTestFactory.Build(matrix);

        stats.ResolveTurn();

        foreach (CityParameterType parameter in System.Enum.GetValues(typeof(CityParameterType)))
            Assert.That(stats.GetValue(parameter), Is.EqualTo(CityStatsTestFactory.NeutralValue).Within(0.001f), parameter.ToString());
    }

    // Renda resolve primeiro (indice 0), entao le direto os valores de configuracao, sem contaminacao de Gauss-Seidel.
    private static float RendaAfterTurn(InteractionMatrix matrix, CityParameterType fonte, float valorFonte)
    {
        var stats = CityStatsTestFactory.Build(matrix, customize: configs =>
        {
            var config = configs[fonte];
            config.InitialValue = valorFonte;
            configs[fonte] = config;
        });
        stats.ResolveTurn();
        return stats.GetValue(CityParameterType.Renda);
    }

    [Test]
    public void Resolve_SourceInCrisisWithPositiveCoefficient_TakesFromTargetWithPressaoMultiplier()
    {
        // Populacao tem +1.2 sobre Renda; em crise ela tira da Renda, e o que a fonte tira vale o multiplicador de pressao.
        var matrix = BuildMatrix(multiplicadorPressaoCrise: 3f, multiplicadorApoioCrise: 7f);

        // (10-50)/50 * 1.2 * 3 = -2.88
        Assert.That(RendaAfterTurn(matrix, CityParameterType.Populacao, 10f), Is.EqualTo(50f - 2.88f).Within(0.001f));
    }

    [Test]
    public void Resolve_SourceInCrisisWithNegativeCoefficient_GivesToTargetWithApoioMultiplier()
    {
        // Sustentabilidade tem -0.2 sobre Renda; em crise o termo fica positivo, e o que a fonte da vale o multiplicador de apoio.
        var matrix = BuildMatrix(multiplicadorPressaoCrise: 7f, multiplicadorApoioCrise: 4f);

        // (10-50)/50 * -0.2 * 4 = 0.64
        Assert.That(RendaAfterTurn(matrix, CityParameterType.Sustentabilidade, 10f), Is.EqualTo(50f + 0.64f).Within(0.001f));
    }

    [Test]
    public void Resolve_SourceInExcessWithPositiveCoefficient_GivesWithApoioExcessoMultiplier()
    {
        var matrix = BuildMatrix(multiplicadorApoioExcesso: 0.5f);

        // (90-50)/50 * 1.2 * 0.5 = 0.48
        Assert.That(RendaAfterTurn(matrix, CityParameterType.Populacao, 90f), Is.EqualTo(50f + 0.48f).Within(0.001f));
    }

    [Test]
    public void Resolve_SourceInExcessWithNegativeCoefficient_TakesAtFullStrength()
    {
        var matrix = BuildMatrix(multiplicadorApoioExcesso: 0.5f);

        // (90-50)/50 * -0.2 * 1 = -0.16
        Assert.That(RendaAfterTurn(matrix, CityParameterType.Sustentabilidade, 90f), Is.EqualTo(50f - 0.16f).Within(0.001f));
    }

    [Test]
    public void Resolve_PesquisaBelowCrisisThreshold_HasNoCrisisFactor()
    {
        // Pesquisa tem +0.5 sobre Renda; abaixo da zona de crise ela tira sem o multiplicador de pressao.
        var matrix = BuildMatrix(multiplicadorPressaoCrise: 3f, multiplicadorApoioCrise: 3f);

        // (10-50)/50 * 0.5 = -0.4
        Assert.That(RendaAfterTurn(matrix, CityParameterType.Pesquisa, 10f), Is.EqualTo(50f - 0.4f).Within(0.001f));
    }

    [Test]
    public void Resolve_PesquisaInExcess_StillUsesApoioExcessoMultiplier()
    {
        var matrix = BuildMatrix(multiplicadorApoioExcesso: 0.5f);

        // (90-50)/50 * 0.5 * 0.5 = 0.2
        Assert.That(RendaAfterTurn(matrix, CityParameterType.Pesquisa, 90f), Is.EqualTo(50f + 0.2f).Within(0.001f));
    }

    [Test]
    public void GetZone_PesquisaNeverInCrisis_OthersAre()
    {
        var matrix = BuildMatrix();

        Assert.That(matrix.GetZone(CityParameterType.Pesquisa, 10f), Is.EqualTo(InteractionZone.Saudavel));
        Assert.That(matrix.GetZone(CityParameterType.Pesquisa, 90f), Is.EqualTo(InteractionZone.Excesso));
        Assert.That(matrix.GetZone(CityParameterType.Saude, 10f), Is.EqualTo(InteractionZone.Crise));
        Assert.That(InteractionMatrix.HasCrisisZone(CityParameterType.Pesquisa), Is.False);
        Assert.That(InteractionMatrix.HasCrisisZone(CityParameterType.Renda), Is.True);
    }

    [TestCase(CityParameterType.Populacao, 10f, 1.6f)]
    [TestCase(CityParameterType.Sustentabilidade, 10f, 0.5f)]
    [TestCase(CityParameterType.Populacao, 90f, 0.6f)]
    [TestCase(CityParameterType.Sustentabilidade, 90f, 1f)]
    [TestCase(CityParameterType.Pesquisa, 10f, 1f)]
    [TestCase(CityParameterType.Populacao, 40f, 1f)]
    public void GetTermAndFactor_MatchResolve(CityParameterType fonte, float valor, float fatorEsperado)
    {
        var matrix = BuildMatrix(multiplicadorPressaoCrise: 1.6f, multiplicadorApoioCrise: 0.5f, multiplicadorApoioExcesso: 0.6f);
        var coeficiente = InteractionMatrix.GetCoefficient(CityParameterType.Renda, fonte);

        Assert.That(matrix.GetZoneFactor(fonte, valor, coeficiente), Is.EqualTo(fatorEsperado).Within(0.0001f));
        Assert.That(50f + matrix.GetTerm(CityParameterType.Renda, fonte, valor),
            Is.EqualTo(RendaAfterTurn(matrix, fonte, valor)).Within(0.0001f));
    }

    [Test]
    public void Resolve_TargetItselfInExcessoZone_AppliesSelfCorrectionDrift()
    {
        // Com tudo mais neutro o efeito de interacao e zero; so sobra a autocorrecao sobre o proprio alvo (Renda), calculada sobre o valor dele antes desta resolucao.
        var matrix = BuildMatrix(derivaCorrecaoExcesso: 0.1f);
        var stats = CityStatsTestFactory.Build(matrix, customize: configs =>
        {
            var renda = configs[CityParameterType.Renda];
            renda.InitialValue = 90f; // >= ZonaExcesso
            configs[CityParameterType.Renda] = renda;
        });

        stats.ResolveTurn();

        // -derivaCorrecaoExcesso * (valorAntes - ZonaExcesso) = -0.1 * (90-75) = -1.5
        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(90f - 1.5f).Within(0.001f));
    }

    [Test]
    public void Resolve_IsGaussSeidel_LaterTargetsSeeEarlierTargetsAlreadyUpdatedThisPass()
    {
        // Renda resolve antes de Energia (coeficiente +0.3), que deve reagir a Renda ja atualizada nesta passada (Gauss-Seidel).
        var matrix = BuildMatrix();
        var stats = CityStatsTestFactory.Build(matrix, customize: configs =>
        {
            var renda = configs[CityParameterType.Renda];
            renda.Deriva = 5f;
            configs[CityParameterType.Renda] = renda;
        });

        stats.ResolveTurn();

        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(55f).Within(0.001f), "Renda deveria refletir so a propria deriva");
        // (55-50)/50 * 0.3 = 0.03; sob Jacobi (le o snapshot pre-turno, Renda=50) esse efeito seria zero.
        Assert.That(stats.GetValue(CityParameterType.Energia), Is.EqualTo(50.03f).Within(0.001f));
    }
}
