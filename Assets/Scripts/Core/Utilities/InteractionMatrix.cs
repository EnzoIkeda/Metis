using System;

// Matriz de interacao entre os 9 parametros da cidade, com zonas dinamicas e ordem de resolucao fixa.
public class InteractionMatrix
{
    // Ordem fixa de resolucao por turno, cada alvo ja enxerga os alvos anteriores atualizados neste mesmo turno.
    private static readonly CityParameterType[] ResolutionOrder =
    {
        CityParameterType.Renda,
        CityParameterType.Energia,
        CityParameterType.Mobilidade,
        CityParameterType.Seguranca,
        CityParameterType.Saude,
        CityParameterType.Sustentabilidade,
        CityParameterType.Populacao,
        CityParameterType.Pesquisa,
    };

    private static readonly int ParamCount = Enum.GetValues(typeof(CityParameterType)).Length;

    // Coeficientes de influencia entre alvo e fonte, na ordem canonica do enum de parametros.
    private static readonly float[,] Coefficients =
    {
        /*              Renda  Energ  Seg    Pop    Pesq   Sust   Bem    Saude  Mob   */
        /* Renda    */ { 0f,    0.4f,  0.6f,  1.2f,  0.5f, -0.2f,  0.3f,  0.3f,  0.8f },
        /* Energia  */ { 0.3f,  0f,    0f,   -1.3f,  0.4f,  0.7f,  0f,    0f,   -0.6f },
        /* Seguranca*/ { 0.4f,  0f,    0f,   -0.7f,  0.2f,  0f,    0.8f,  0.2f,  0f   },
        /* Populacao*/ { 0.3f,  0.3f,  0.7f,  0f,    0f,    0.2f,  1f,    0.4f,  0.5f },
        /* Pesquisa */ { 0.5f,  0.2f,  0f,    0.2f,  0f,    0.3f,  0.3f,  0f,    0f   },
        /* Sust.    */ {-0.2f, -0.6f,  0f,   -1f,    0.8f,  0f,    0f,    0f,   -0.5f },
        /* BemEstar */ { 0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f   },
        /* Saude    */ { 0.4f,  0f,    0f,   -0.6f,  0.4f,  0.7f,  0.5f,  0f,    0.2f },
        /* Mobilid. */ { 0.5f,  0.3f,  0f,   -0.9f,  0.3f,  0.2f,  0.4f,  0f,    0f   },
    };

    private readonly float _valorNeutro;
    private readonly float _escalaGlobal;
    private readonly float _zonaCrise;
    private readonly float _zonaExcesso;
    private readonly float _multiplicadorPressaoCrise;
    private readonly float _multiplicadorApoioCrise;
    private readonly float _multiplicadorApoioExcesso;
    private readonly float _derivaCorrecaoExcesso;

    public InteractionMatrix(
        float valorNeutro,
        float escalaGlobal,
        float zonaCrise,
        float zonaExcesso,
        float multiplicadorPressaoCrise,
        float multiplicadorApoioCrise,
        float multiplicadorApoioExcesso,
        float derivaCorrecaoExcesso)
    {
        _valorNeutro = valorNeutro;
        _escalaGlobal = escalaGlobal;
        _zonaCrise = zonaCrise;
        _zonaExcesso = zonaExcesso;
        _multiplicadorPressaoCrise = multiplicadorPressaoCrise;
        _multiplicadorApoioCrise = multiplicadorApoioCrise;
        _multiplicadorApoioExcesso = multiplicadorApoioExcesso;
        _derivaCorrecaoExcesso = derivaCorrecaoExcesso;
    }

    // Aplica interacoes e deriva de manutencao em cada um dos 8 alvos, na ordem fixa de resolucao.
    public void Resolve(CityStats stats)
    {
        // Snapshot do inicio da fase, pra autocorrecao de excesso reagir ao valor original do alvo.
        var valoresAntes = new float[ParamCount];
        foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
            valoresAntes[(int)parameter] = stats.GetValue(parameter);

        foreach (var alvo in ResolutionOrder)
        {
            float efeito = 0f;
            foreach (CityParameterType fonte in Enum.GetValues(typeof(CityParameterType)))
            {
                var coeficiente = Coefficients[(int)alvo, (int)fonte];
                if (coeficiente == 0f)
                    continue;

                // Le o valor ja atualizado nesta mesma passada, se a fonte for um alvo anterior.
                var valorFonte = stats.GetValue(fonte);
                var fator = FatorZona(valorFonte, coeficiente);
                efeito += (valorFonte - _valorNeutro) / _valorNeutro * coeficiente * fator;
            }

            var correcaoExcesso = DerivaCorrecaoExcesso(valoresAntes[(int)alvo]);
            var delta = efeito * _escalaGlobal + stats.GetDeriva(alvo) + correcaoExcesso;
            stats.ApplyModifier(new StatModifier { Parameter = alvo, Amount = delta });
        }
    }

    // Multiplicador aplicado a um coeficiente conforme a zona (crise ou excesso) da fonte.
    private float FatorZona(float valorFonte, float coeficiente)
    {
        if (valorFonte <= _zonaCrise)
            return coeficiente < 0f ? _multiplicadorPressaoCrise : _multiplicadorApoioCrise;
        if (valorFonte >= _zonaExcesso)
            return coeficiente < 0f ? 1f : _multiplicadorApoioExcesso;
        return 1f;
    }

    // Deriva extra negativa sobre o proprio alvo, so quando ele mesmo esta em excesso.
    private float DerivaCorrecaoExcesso(float valorAlvoAntes)
    {
        if (valorAlvoAntes <= _zonaExcesso)
            return 0f;
        return -_derivaCorrecaoExcesso * (valorAlvoAntes - _zonaExcesso);
    }
}
