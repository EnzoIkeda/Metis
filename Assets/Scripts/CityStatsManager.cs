using System;
using UnityEngine;

/// <summary>
/// Wrapper de CityStats, Bridge entre a logica de dominio pura e a cena Unity
/// </summary>
public class CityStatsManager : MonoBehaviour
{
    [SerializeField] private CityParameterConfig[] _initialParameters = Array.Empty<CityParameterConfig>();

    public CityStats Stats { get; private set; }

    private void Awake()
    {
        Stats = new CityStats(_initialParameters);
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

    // Preenche valores placeholder ao adicionar o componente; ajustar no Inspector conforme o
    // balanceamento real do jogo
    private void Reset()
    {
        var parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));
        _initialParameters = new CityParameterConfig[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            _initialParameters[i] = new CityParameterConfig
            {
                Parameter = parameters[i],
                InitialValue = 50f,
                MinValue = 0f,
                MaxValue = 100f
            };
        }
    }

    private void HandleParameterChanged(CityParameterType parameter, float value)
    {
        Debug.Log($"[CityStats] {parameter} = {value}");
    }

    private void HandleParameterCritical(CityParameterType parameter)
    {
        Debug.LogWarning($"[CityStats] {parameter} em nível crítico ({Stats.GetValue(parameter)})");
    }

    private void HandleGameOver()
    {
        Debug.LogWarning("[CityStats] Game Over: algum parâmetro atingiu o limite crítico.");
    }

    [ContextMenu("Log Parameter Values")]
    private void LogAllParameterValues()
    {
        foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
            Debug.Log($"[CityStats] {parameter} = {Stats.GetValue(parameter)}");
    }

    
    // DEBUG
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

    [ContextMenu("Debug: Forçar Renda a nível crítico")]
    private void DebugForceCriticalRenda()
    {
        Stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Renda, Amount = -1000f });
    }
}
