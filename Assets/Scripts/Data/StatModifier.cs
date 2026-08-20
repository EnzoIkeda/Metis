using System;

// Parametros que compoem o estado da cidade.
public enum CityParameterType
{
    Renda,
    Energia,
    Seguranca,
    Populacao,
    Pesquisa,
    Sustentabilidade,
    BemEstar,
    Saude,
    Mobilidade
}


[Serializable]
public struct StatModifier
{
    public CityParameterType Parameter;
    public float Amount;
}

// Nomes de exibicao abreviados de cada parametro.
public static class CityParameterTypeExtensions
{
    public static string GetDisplayName(this CityParameterType parameter)
    {
        switch (parameter)
        {
            case CityParameterType.Renda: return "Renda";
            case CityParameterType.Energia: return "Energ";
            case CityParameterType.Seguranca: return "Seg";
            case CityParameterType.Populacao: return "Pop";
            case CityParameterType.Pesquisa: return "Pesq";
            case CityParameterType.Sustentabilidade: return "Sust";
            case CityParameterType.BemEstar: return "Satisf";
            case CityParameterType.Saude: return "Saúde";
            case CityParameterType.Mobilidade: return "Mob";
            default: return parameter.ToString();
        }
    }
}
