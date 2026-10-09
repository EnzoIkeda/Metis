using System.Collections.Generic;

// Detecta a entrada de um parametro em Colapso, ignorando os avisos repetidos enquanto ele continua la.
public class CollapseTracker
{
    private readonly HashSet<CityParameterType> _inCollapse = new HashSet<CityParameterType>();

    // Verdadeiro so na passagem de fora pra dentro do Colapso.
    public bool Update(CityParameterType parameter, bool isInCollapse)
    {
        if (isInCollapse)
            return _inCollapse.Add(parameter);

        _inCollapse.Remove(parameter);
        return false;
    }
}
