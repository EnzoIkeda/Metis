using System;

// Decide quando a faixa de crise toca: entra com algum parametro na zona Crise e so sai com todos
// acima dela com folga, para nao trocar de faixa a cada turno quando um valor oscila em volta do limite.
public class CrisisMusicRule
{
    public const float ExitMargin = 3f;

    private static readonly CityParameterType[] Parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));

    public bool IsActive { get; private set; }

    // Avalia o estado num momento estavel do turno e devolve se a faixa de crise deve tocar.
    public bool Update(Func<CityParameterType, float> valueOf, float crisisThreshold)
    {
        if (valueOf == null)
            return IsActive;

        if (IsActive)
            IsActive = AnyAtOrBelow(valueOf, crisisThreshold + ExitMargin);
        else
            IsActive = AnyAtOrBelow(valueOf, crisisThreshold);
        return IsActive;
    }

    // Fim da fase: a crise para de tocar ate a proxima avaliacao.
    public void Reset()
    {
        IsActive = false;
    }

    private static bool AnyAtOrBelow(Func<CityParameterType, float> valueOf, float limit)
    {
        foreach (var parameter in Parameters)
        {
            // Mesmo criterio da zona Crise da matriz: a Pesquisa nunca entra.
            if (InteractionMatrix.HasCrisisZone(parameter) && valueOf(parameter) <= limit)
                return true;
        }
        return false;
    }
}
