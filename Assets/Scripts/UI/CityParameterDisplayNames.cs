// Nomes curtos de cada parametro no idioma atual; fica na UI por depender do idioma.
public static class CityParameterTypeExtensions
{
    public static string GetDisplayName(this CityParameterType parameter)
    {
        var isPt = LocalizationManager.Current == Language.Portuguese;
        switch (parameter)
        {
            case CityParameterType.Renda: return isPt ? "Renda" : "Income";
            case CityParameterType.Energia: return isPt ? "Energ" : "Enrg";
            case CityParameterType.Seguranca: return isPt ? "Seg" : "Sec";
            case CityParameterType.Populacao: return isPt ? "Pop" : "Pop";
            case CityParameterType.Pesquisa: return isPt ? "Pesq" : "Res";
            case CityParameterType.Sustentabilidade: return isPt ? "Sust" : "Sust";
            case CityParameterType.BemEstar: return isPt ? "Bem-estar" : "Wellbeing";
            case CityParameterType.Saude: return isPt ? "Saúde" : "Health";
            case CityParameterType.Mobilidade: return isPt ? "Mob" : "Mob";
            default: return parameter.ToString();
        }
    }

    // Nome completo, pra textos com espaco (explicacoes, popups).
    public static string GetFullName(this CityParameterType parameter)
    {
        var isPt = LocalizationManager.Current == Language.Portuguese;
        switch (parameter)
        {
            case CityParameterType.Renda: return isPt ? "Renda" : "Income";
            case CityParameterType.Energia: return isPt ? "Energia" : "Energy";
            case CityParameterType.Seguranca: return isPt ? "Segurança" : "Safety";
            case CityParameterType.Populacao: return isPt ? "População" : "Population";
            case CityParameterType.Pesquisa: return isPt ? "Pesquisa" : "Research";
            case CityParameterType.Sustentabilidade: return isPt ? "Sustentabilidade" : "Sustainability";
            case CityParameterType.BemEstar: return isPt ? "Bem-estar" : "Well-being";
            case CityParameterType.Saude: return isPt ? "Saúde" : "Health";
            case CityParameterType.Mobilidade: return isPt ? "Mobilidade" : "Mobility";
            default: return parameter.ToString();
        }
    }
}
