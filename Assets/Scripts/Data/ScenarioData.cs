using System;
using System.Collections.Generic;
using UnityEngine;

// Modo Cenarios: uma fase tematica jogada sozinha, com objetivos que valem estrelas.
[CreateAssetMenu(fileName = "New Scenario", menuName = "Metis/Scenario Data")]
public class ScenarioData : ScriptableObject
{
    [SerializeField] private ThemedPhaseData _theme;

    // Livre abre a escolha de baralho; senao vale o arquetipo abaixo.
    [SerializeField] private bool _freeArchetype = true;
    [SerializeField] private CardArchetype _archetype = CardArchetype.Sustentabilidade;
    [SerializeField] private ScenarioObjective[] _objectives = Array.Empty<ScenarioObjective>();

    public ThemedPhaseData Theme => _theme;
    public bool FreeArchetype => _freeArchetype;
    public CardArchetype Archetype => _archetype;
    public IReadOnlyList<ScenarioObjective> Objectives => _objectives;
}
