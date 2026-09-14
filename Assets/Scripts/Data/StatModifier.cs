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
