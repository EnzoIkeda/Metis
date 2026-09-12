using System.Collections.Generic;
using UnityEngine;

// Override do valor inicial de um parametro, usado pelos presets de fases 2+.
[System.Serializable]
public struct CityParameterOverride
{
    public CityParameterType Parameter;
    public float InitialValue;
}

// Perfil de desafio pras fases 2+ de uma rodada (a fase 1 sempre usa a cidade calibrada padrao).
[CreateAssetMenu(fileName = "New Parameter Preset", menuName = "Metis/City Parameter Preset Data")]
public class CityParameterPresetData : ScriptableObject
{
    [SerializeField] private string _presetName;
    [SerializeField] private string _presetNameEn;
    [SerializeField] private CityParameterOverride[] _overrides;

    public string PresetName => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_presetNameEn) == false
        ? _presetNameEn
        : _presetName;

    public IReadOnlyList<CityParameterOverride> Overrides => _overrides;
}
