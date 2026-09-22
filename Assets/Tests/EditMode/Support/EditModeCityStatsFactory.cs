using System;
using System.Collections.Generic;

// Duplica o helper equivalente da Camada 1 porque os dois projetos de teste sao assemblies independentes (esse roda dentro do Editor).
internal static class EditModeCityStatsFactory
{
    public const float NeutralValue = 50f;

    public static CityStats Build(Action<Dictionary<CityParameterType, CityParameterConfig>> customize = null)
    {
        var configs = new Dictionary<CityParameterType, CityParameterConfig>();
        foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
        {
            configs[parameter] = new CityParameterConfig
            {
                Parameter = parameter,
                InitialValue = NeutralValue,
                MinValue = 0f,
                MaxValue = 100f,
                CriticalLevel = -1f,
                Deriva = 0f,
            };
        }

        customize?.Invoke(configs);
        return new CityStats(configs.Values);
    }
}
