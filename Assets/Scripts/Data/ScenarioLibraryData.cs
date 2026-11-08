using System;
using System.Collections.Generic;
using UnityEngine;

// Todos os cenarios da tela de selecao, na ordem em que aparecem.
[CreateAssetMenu(fileName = "ScenarioLibrary", menuName = "Metis/Scenario Library")]
public class ScenarioLibraryData : ScriptableObject
{
    [SerializeField] private ScenarioData[] _scenarios = Array.Empty<ScenarioData>();

    public IReadOnlyList<ScenarioData> Scenarios => _scenarios;

    public ScenarioData Find(string scenarioId)
    {
        if (string.IsNullOrEmpty(scenarioId))
            return null;
        foreach (var scenario in _scenarios)
        {
            if (scenario != null && scenario.name == scenarioId)
                return scenario;
        }
        return null;
    }
}
