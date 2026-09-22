// Nomes de exibicao abreviados de cada parametro, no idioma atual. Fica fora de Data/ por depender de LocalizationManager e ser usado so pela UI.
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
            case CityParameterType.BemEstar: return isPt ? "Satisf" : "Well";
            case CityParameterType.Saude: return isPt ? "Saúde" : "Health";
            case CityParameterType.Mobilidade: return isPt ? "Mob" : "Mob";
            default: return parameter.ToString();
        }
    }
}
