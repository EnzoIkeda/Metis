using System;
using System.Collections.Generic;

namespace Metis.Core.Tests;

// Monta um CityStats com os 9 parametros em valores neutros e nunca criticos por padrao,
// pra cada teste customizar so o que importa pro caso em questao.
internal static class CityStatsTestFactory
{
    public const float NeutralValue = 50f;

    public static CityStats Build(
        InteractionMatrix interactions = null,
        float colapsoPenaltyPerParameter = 0f,
        Action<Dictionary<CityParameterType, CityParameterConfig>> customize = null)
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
        return new CityStats(configs.Values, interactions, colapsoPenaltyPerParameter);
    }
}
