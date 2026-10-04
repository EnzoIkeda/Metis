using System;
using System.Collections.Generic;

// Override do valor inicial de um parametro, usado pelos presets de fases 2+.
[Serializable]
public struct CityParameterOverride
{
    public CityParameterType Parameter;
    public float InitialValue;
}

// Como um preset de fase muda a cidade calibrada padrao, igual no jogo e no simulador de balanceamento.
public static class CityPresetRules
{
    // Troca os valores iniciais sobrescritos e multiplica as derivas negativas, pra fases com preset poderem ser mais duras que a fase 1.
    public static CityParameterConfig[] Apply(IReadOnlyList<CityParameterConfig> baseConfigs, IEnumerable<CityParameterOverride> overrides, float derivaMultiplier)
    {
        var configs = new CityParameterConfig[baseConfigs.Count];
        for (int i = 0; i < configs.Length; i++)
            configs[i] = baseConfigs[i];

        foreach (var overrideValue in overrides)
        {
            for (int i = 0; i < configs.Length; i++)
            {
                if (configs[i].Parameter == overrideValue.Parameter)
                    configs[i].InitialValue = overrideValue.InitialValue;
            }
        }

        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i].Deriva < 0f)
                configs[i].Deriva *= derivaMultiplier;
        }
        return configs;
    }
}
