using System.Collections.Generic;
using UnityEngine;

// Perfil de desafio pras fases 2+ de uma rodada (a fase 1 sempre usa a cidade calibrada padrao).
[CreateAssetMenu(fileName = "New Parameter Preset", menuName = "Metis/City Parameter Preset Data")]
public class CityParameterPresetData : ScriptableObject
{
    [SerializeField] private string _presetName;
    [SerializeField] private string _presetNameEn;
    [SerializeField] private CityParameterOverride[] _overrides;

    // Multiplica as derivas negativas na fase com este preset; 1 deixa a deriva da cidade padrao.
    [SerializeField, Min(0f)] private float _derivaMultiplier = 1f;

    public string PresetName => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_presetNameEn) == false
        ? _presetNameEn
        : _presetName;

    public IReadOnlyList<CityParameterOverride> Overrides => _overrides;

    public float DerivaMultiplier => _derivaMultiplier;
}
