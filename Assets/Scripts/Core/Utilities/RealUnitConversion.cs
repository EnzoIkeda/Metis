using System;
using System.Globalization;

// Converte o indice 0 a 100 do motor pra unidade real (normalizacao min-max) e formata o numero de forma compacta.
// So exibicao: o motor continua rodando em indice.
public static class RealUnitConversion
{
    public const float IndexMin = 0f;
    public const float IndexMax = 100f;

    // Valor absoluto: indice 0 vira o minimo real, indice 100 vira o maximo real.
    public static double ToReal(float index, double realMin, double realMax)
    {
        return realMin + ((double)index - IndexMin) / (IndexMax - IndexMin) * (realMax - realMin);
    }

    // Variacao: sem o deslocamento do minimo, pra custos e efeitos.
    public static double ToRealDelta(float deltaIndex, double realMin, double realMax)
    {
        return (double)deltaIndex / (IndexMax - IndexMin) * (realMax - realMin);
    }

    private static readonly double[] Scales = { 1, 1e3, 1e6, 1e9 };
    private static readonly string[] SuffixesPt = { "", " mil", " mi", " bi" };
    private static readonly string[] SuffixesEn = { "", "k", "M", "B" };

    // Numero compacto: mil/mi/bi (ou k/M/B em ingles), com 1 casa abaixo de 10 e nenhuma acima.
    // Sem escala, usa as casas pedidas. Virgula decimal em portugues.
    public static string FormatCompact(double value, int decimals, bool showSign, bool portuguese)
    {
        return FormatNumber(value, decimals, showSign, portuguese, abbreviate: true);
    }

    // Sem abreviar, o numero sai inteiro com separador de milhar (1.350 em portugues, 1,350 em ingles).
    public static string FormatNumber(double value, int decimals, bool showSign, bool portuguese, bool abbreviate)
    {
        if (abbreviate == false)
            return FormatGrouped(value, decimals, showSign, portuguese);

        var magnitude = Math.Abs(value);
        var scale = 0;
        while (scale < Scales.Length - 1 && magnitude >= Scales[scale + 1])
            scale++;

        var places = 0;
        var rounded = 0.0;
        while (true)
        {
            var scaled = magnitude / Scales[scale];
            places = scale > 0 ? (scaled < 10 ? 1 : 0) : Math.Max(0, decimals);
            rounded = Math.Round(scaled, places, MidpointRounding.AwayFromZero);
            // Arredondar pode chegar em 1000 (ex. 999,96 mil): sobe pra proxima escala.
            if (rounded < 1000 || scale == Scales.Length - 1)
                break;
            scale++;
        }

        var number = rounded.ToString("F" + places, CultureInfo.InvariantCulture);
        if (portuguese)
            number = number.Replace('.', ',');
        if (rounded == 0)
            return number;

        var suffix = portuguese ? SuffixesPt[scale] : SuffixesEn[scale];
        var sign = value < 0 ? "-" : (showSign ? "+" : string.Empty);
        return sign + number + suffix;
    }

    private static string FormatGrouped(double value, int decimals, bool showSign, bool portuguese)
    {
        var places = Math.Max(0, decimals);
        var rounded = Math.Round(Math.Abs(value), places, MidpointRounding.AwayFromZero);
        var number = rounded.ToString("N" + places, CultureInfo.InvariantCulture);
        if (portuguese)
            number = number.Replace(',', '_').Replace('.', ',').Replace('_', '.');
        if (rounded == 0)
            return number;

        var sign = value < 0 ? "-" : (showSign ? "+" : string.Empty);
        return sign + number;
    }
}
