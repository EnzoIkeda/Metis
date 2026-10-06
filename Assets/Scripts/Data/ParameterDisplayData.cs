using System;
using UnityEngine;

[Serializable]
public struct ParameterDisplayEntry
{
    public CityParameterType Parameter;
    public Sprite Icon;
}

// Dados so de apresentacao de cada parametro, sem nenhuma regra de jogo.
[CreateAssetMenu(fileName = "ParameterDisplay", menuName = "Metis/Parameter Display Data")]
public class ParameterDisplayData : ScriptableObject
{
    [SerializeField] private ParameterDisplayEntry[] _entries = Array.Empty<ParameterDisplayEntry>();

    public Sprite GetIcon(CityParameterType parameter)
    {
        foreach (var entry in _entries)
        {
            if (entry.Parameter == parameter)
                return entry.Icon;
        }
        return null;
    }
}
