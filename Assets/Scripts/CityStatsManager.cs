using System;
using UnityEngine;

// Constantes globais da matriz de interacoes, incluindo zonas dinamicas e escala.
[Serializable]
public struct InteractionConfig
{
    public float ValorNeutro;
    public float EscalaGlobal;
    public float ZonaCrise;
    public float ZonaExcesso;
    public float MultiplicadorPressaoCrise;
    public float MultiplicadorApoioCrise;
    public float MultiplicadorApoioExcesso;
    public float DerivaCorrecaoExcesso;
    public float PenalidadeColapso;
}

// Ponte entre a logica de dominio pura e a cena.
public class CityStatsManager : MonoBehaviour
{
    [SerializeField] private CityParameterConfig[] _initialParameters = Array.Empty<CityParameterConfig>();
    [SerializeField] private InteractionConfig _interactionConfig;
    [SerializeField] private CityParameterPresetData[] _phasePresets = Array.Empty<CityParameterPresetData>();

    public CityStats Stats { get; private set; }

    // Nome do asset do preset aplicado nesta fase, vazio na cidade calibrada.
    public string PresetName { get; private set; } = string.Empty;

    private void Awake()
    {
        var interactions = new InteractionMatrix(
            _interactionConfig.ValorNeutro,
            _interactionConfig.EscalaGlobal,
            _interactionConfig.ZonaCrise,
            _interactionConfig.ZonaExcesso,
            _interactionConfig.MultiplicadorPressaoCrise,
            _interactionConfig.MultiplicadorApoioCrise,
            _interactionConfig.MultiplicadorApoioExcesso,
            _interactionConfig.DerivaCorrecaoExcesso);

        var savedPhase = MetaProgressionManager.SavedPhase;
        CityParameterPresetData savedPreset = null;
        if (savedPhase != null && TryFindPreset(savedPhase.PresetName, out savedPreset) == false)
        {
            Debug.LogWarning($"[CityStatsManager] Preset salvo '{savedPhase.PresetName}' não existe mais, a fase recomeça do zero.");
            MetaProgressionManager.DiscardSavedPhase();
            savedPhase = null;
        }

        var preset = savedPhase != null ? savedPreset : DrawPreset();
        Stats = new CityStats(BuildInitialParameters(preset), interactions, _interactionConfig.PenalidadeColapso);

        if (savedPhase != null)
            RunSaveRules.RestoreStats(savedPhase, Stats);
    }

    // Fase 1 de uma rodada usa a cidade calibrada padrao; da fase 2 em diante sorteia um preset.
    private CityParameterPresetData DrawPreset()
    {
        if (MetaProgressionManager.IsNewPhase == false || _phasePresets.Length == 0)
            return null;

        return _phasePresets[UnityEngine.Random.Range(0, _phasePresets.Length)];
    }

    // Nome vazio e a cidade calibrada, sem preset.
    private bool TryFindPreset(string presetName, out CityParameterPresetData preset)
    {
        preset = null;
        if (string.IsNullOrEmpty(presetName))
            return true;

        foreach (var candidate in _phasePresets)
        {
            if (candidate != null && candidate.name == presetName)
            {
                preset = candidate;
                return true;
            }
        }
        return false;
    }

    private CityParameterConfig[] BuildInitialParameters(CityParameterPresetData preset)
    {
        if (preset == null)
            return _initialParameters;

        PresetName = preset.name;
        Debug.Log($"[CityStatsManager] Preset '{preset.PresetName}' aplicado.");
        return CityPresetRules.Apply(_initialParameters, preset.Overrides, preset.DerivaMultiplier);
    }

    private void OnEnable()
    {
        Stats.OnParameterChanged += HandleParameterChanged;
        Stats.OnParameterCritical += HandleParameterCritical;
        Stats.OnGameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        Stats.OnParameterChanged -= HandleParameterChanged;
        Stats.OnParameterCritical -= HandleParameterCritical;
        Stats.OnGameOver -= HandleGameOver;
    }

    // Preenche os valores ja calibrados na planilha de balanceamento ao adicionar o componente.
    private void Reset()
    {
        // Nivel critico negativo significa que o parametro nunca fica critico, caso so de Pesquisa.
        var calibrado = new (CityParameterType Parametro, float Inicial, float Critico, float Deriva)[]
        {
            (CityParameterType.Renda, 55f, 10f, 1f),
            (CityParameterType.Energia, 50f, 10f, -1f),
            (CityParameterType.Seguranca, 50f, 10f, -1f),
            (CityParameterType.Populacao, 40f, 10f, 0f),
            (CityParameterType.Pesquisa, 10f, -1f, 0f),
            (CityParameterType.Sustentabilidade, 45f, 10f, -1f),
            (CityParameterType.BemEstar, 50f, 10f, 0f),
            (CityParameterType.Saude, 50f, 10f, -1f),
            (CityParameterType.Mobilidade, 45f, 10f, -1f),
        };

        _initialParameters = new CityParameterConfig[calibrado.Length];
        for (int i = 0; i < calibrado.Length; i++)
        {
            var (parametro, inicial, critico, deriva) = calibrado[i];
            _initialParameters[i] = new CityParameterConfig
            {
                Parameter = parametro,
                InitialValue = inicial,
                MinValue = 0f,
                MaxValue = 100f,
                CriticalLevel = critico,
                Deriva = deriva,
            };
        }

        _interactionConfig = new InteractionConfig
        {
            ValorNeutro = 50f,
            EscalaGlobal = 1f,
            ZonaCrise = 25f,
            ZonaExcesso = 75f,
            MultiplicadorPressaoCrise = 1.6f,
            MultiplicadorApoioCrise = 0.5f,
            MultiplicadorApoioExcesso = 0.6f,
            DerivaCorrecaoExcesso = 0.1f,
            PenalidadeColapso = 12f,
        };
    }

    private void HandleParameterChanged(CityParameterType parameter, float value)
    {
        Debug.Log($"[CityStats] {parameter} = {value}");
    }

    private void HandleParameterCritical(CityParameterType parameter)
    {
        var papel = parameter == CityStats.AnchorParameter ? "ÂNCORA, Game Over" : "Colapso, pressiona a âncora";
        Debug.LogWarning($"[CityStats] {parameter} em nível crítico ({Stats.GetValue(parameter)}), {papel}");
    }

    private void HandleGameOver()
    {
        Debug.LogWarning($"[CityStats] Game Over: {CityStats.AnchorParameter} (parâmetro âncora) atingiu o nível crítico.");
    }

    [ContextMenu("Log Parameter Values")]
    private void LogAllParameterValues()
    {
        foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
            Debug.Log($"[CityStats] {parameter} = {Stats.GetValue(parameter)}");
    }

    // Debug.
    [ContextMenu("Debug: Aplicar +10 em Renda")]
    private void DebugApplyRendaModifier()
    {
        Stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = 10f });
    }

    [ContextMenu("Debug: Recalcular parâmetros derivados (Bem-Estar)")]
    private void DebugRecomputeDerivedParameters()
    {
        Stats.RecomputeDerivedParameters();
    }

    [ContextMenu("Debug: Resolver turno (interações + deriva + Colapso)")]
    private void DebugResolveTurn()
    {
        Stats.ResolveTurn();
    }

    [ContextMenu("Debug: Forçar Renda a nível crítico (Colapso)")]
    private void DebugForceCriticalRenda()
    {
        Stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = -1000f });
    }
}
