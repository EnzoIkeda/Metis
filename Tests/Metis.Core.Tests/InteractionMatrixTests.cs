namespace Metis.Core.Tests;

// Valores esperados calculados a mao pela formula: efeito = soma_fontes[(valorFonte - valorNeutro) / valorNeutro * coeficiente * fatorZona], delta = efeito * escalaGlobal + deriva + correcaoExcesso; cada teste isola um unico termo, deixando o resto no valor neutro (contribuicao zero) pra prever o resultado exato.
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

    [Test]
    public void Resolve_SourceInCrisisZoneWithPositiveCoefficient_UsesApoioCriseMultiplier()
    {
        // Renda resolve primeiro (indice 0), entao le direto os valores de configuracao, sem contaminacao de Gauss-Seidel; Populacao tem coeficiente +1.2 sobre Renda.
        var matrix = BuildMatrix(multiplicadorApoioCrise: 3f);
        var stats = CityStatsTestFactory.Build(matrix, customize: configs =>
        {
            var populacao = configs[CityParameterType.Populacao];
            populacao.InitialValue = 10f; // <= ZonaCrise
            configs[CityParameterType.Populacao] = populacao;
        });

        stats.ResolveTurn();

        // (10-50)/50 * 1.2 * multiplicadorApoioCrise(3) = -0.8 * 1.2 * 3 = -2.88
        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(50f - 2.88f).Within(0.001f));
    }

    [Test]
    public void Resolve_SourceInCrisisZoneWithNegativeCoefficient_UsesPressaoCriseMultiplier()
    {
        // Sustentabilidade tem coeficiente -0.2 sobre Renda (que resolve primeiro).
        var matrix = BuildMatrix(multiplicadorPressaoCrise: 4f);
        var stats = CityStatsTestFactory.Build(matrix, customize: configs =>
        {
            var sustentabilidade = configs[CityParameterType.Sustentabilidade];
            sustentabilidade.InitialValue = 10f; // <= ZonaCrise
            configs[CityParameterType.Sustentabilidade] = sustentabilidade;
        });

        stats.ResolveTurn();

        // (10-50)/50 * -0.2 * multiplicadorPressaoCrise(4) = -0.8 * -0.2 * 4 = 0.64
        Assert.That(stats.GetValue(CityParameterType.Renda), Is.EqualTo(50f + 0.64f).Within(0.001f));
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
        // Renda (indice 0) tem deriva propria e muda antes de Energia (indice 1, coeficiente +0.3), que sob Gauss-Seidel deve reagir ao valor de Renda ja atualizado nesta mesma passada, nao ao snapshot do inicio do turno.
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
