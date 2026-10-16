using System;
using UnityEngine;

[Serializable]
public struct ParameterDisplayEntry
{
    public CityParameterType Parameter;
    public Sprite Icon;

    // Sem unidade real (indice composto): mostra sempre o indice.
    public bool ShowAsIndex;

    [Tooltip("Valor real equivalente ao indice 0.")]
    public double RealMin;

    [Tooltip("Valor real equivalente ao indice 100.")]
    public double RealMax;

    [Tooltip("Casas decimais quando o numero nao e abreviado (mil, mi, bi).")]
    public int Decimals;

    [Tooltip("Mostra + em valores positivos (saldos que podem ficar negativos).")]
    public bool ShowSign;

    [Tooltip("Numero inteiro com separador de milhar em vez de abreviar com mil, mi, bi.")]
    public bool NoAbbreviation;

    [Tooltip("Modelo da unidade com {0} no lugar do numero, ex. \"{0} km/h\".")]
    public string UnitPt;

    [Tooltip("Unit template with {0} for the number, e.g. \"{0} km/h\".")]
    public string UnitEn;
}

// Dados so de apresentacao de cada parametro, sem nenhuma regra de jogo.
[CreateAssetMenu(fileName = "ParameterDisplay", menuName = "Metis/Parameter Display Data")]
public class ParameterDisplayData : ScriptableObject
{
    private const string SmallUnitOpen = "<size=60%>";
    private const string SmallUnitClose = "</size>";

    [SerializeField] private ParameterDisplayEntry[] _entries = Array.Empty<ParameterDisplayEntry>();

    public Sprite GetIcon(CityParameterType parameter)
    {
        return TryGetEntry(parameter, out var entry) ? entry.Icon : null;
    }

    // Nivel atual do parametro, em unidade real ou indice conforme a preferencia do jogador.
    // Sem unidade (withUnit falso), so o numero, pra espacos curtos onde o icone ja indica o parametro.
    public string FormatValue(CityParameterType parameter, float index, bool smallUnit, bool withUnit = true)
    {
        if (UsesRealUnit(parameter, out var entry) == false)
            return index.ToString("0");

        var real = RealUnitConversion.ToReal(index, entry.RealMin, entry.RealMax);
        var number = RealUnitConversion.FormatNumber(real, entry.Decimals, entry.ShowSign, IsPortuguese, entry.NoAbbreviation == false);
        return withUnit ? ApplyUnit(entry, number, smallUnit) : number;
    }

    // Quantidade que muda o parametro (custo, efeito), sem o deslocamento do minimo real.
    // extraDecimals acrescenta casas, pra parcelas pequenas de uma variacao nao virarem zero.
    public string FormatAmount(CityParameterType parameter, float amount, bool smallUnit, int extraDecimals = 0)
    {
        if (UsesRealUnit(parameter, out var entry) == false)
            return extraDecimals > 0 ? FormatIndexDecimals(amount, extraDecimals) : amount.ToString("0");

        var real = RealUnitConversion.ToRealDelta(amount, entry.RealMin, entry.RealMax);
        var number = RealUnitConversion.FormatNumber(real, entry.Decimals + extraDecimals, false, IsPortuguese, entry.NoAbbreviation == false);
        return ApplyUnit(entry, number, smallUnit);
    }

    private static string FormatIndexDecimals(float amount, int decimals)
    {
        var number = amount.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture);
        return IsPortuguese ? number.Replace('.', ',') : number;
    }

    private static bool IsPortuguese => LocalizationManager.Current == Language.Portuguese;

    private bool UsesRealUnit(CityParameterType parameter, out ParameterDisplayEntry entry)
    {
        return TryGetEntry(parameter, out entry)
            && entry.ShowAsIndex == false
            && UnitDisplaySettings.ShowRealUnits;
    }

    private bool TryGetEntry(CityParameterType parameter, out ParameterDisplayEntry entry)
    {
        foreach (var candidate in _entries)
        {
            if (candidate.Parameter == parameter)
            {
                entry = candidate;
                return true;
            }
        }
        entry = default;
        return false;
    }

    // Encaixa o numero no modelo da unidade; com smallUnit, o texto da unidade sai menor que o numero.
    private static string ApplyUnit(ParameterDisplayEntry entry, string number, bool smallUnit)
    {
        var template = IsPortuguese ? entry.UnitPt : entry.UnitEn;
        if (string.IsNullOrEmpty(template))
            template = string.IsNullOrEmpty(entry.UnitPt) ? entry.UnitEn : entry.UnitPt;
        if (string.IsNullOrEmpty(template))
            return number;

        var split = template.IndexOf("{0}", StringComparison.Ordinal);
        if (split < 0)
            return number;

        var prefix = template.Substring(0, split);
        var suffix = template.Substring(split + 3);
        if (smallUnit)
        {
            if (prefix.Trim().Length > 0)
                prefix = SmallUnitOpen + prefix + SmallUnitClose;
            if (suffix.Trim().Length > 0)
                suffix = SmallUnitOpen + suffix + SmallUnitClose;
        }
        return prefix + number + suffix;
    }
}
