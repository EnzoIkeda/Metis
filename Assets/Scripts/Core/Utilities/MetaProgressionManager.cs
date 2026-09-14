using System;
using System.Collections.Generic;
using UnityEngine;

// Estado salvo de uma rodada (baralho escolhido, recompensas carregadas, fases completadas).
[Serializable]
public class MetaProgressionState
{
    public CardArchetype Archetype;
    public List<string> LoadedCardNames = new List<string>();
    public List<string> LoadedAdvantageNames = new List<string>();
    public int PhasesCompleted;
}

// Meta-progressao da rodada atual, persistida entre cenas via PlayerPrefs, mesmo padrao ja usado pro idioma.
public static class MetaProgressionManager
{
    private const string PrefsKey = "Metis.MetaProgression";

    private static MetaProgressionState _state;

    private static MetaProgressionState State
    {
        get
        {
            if (_state == null)
            {
                var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                _state = string.IsNullOrEmpty(json) ? new MetaProgressionState() : JsonUtility.FromJson<MetaProgressionState>(json);
            }
            return _state;
        }
    }

    public static CardArchetype Archetype => State.Archetype;

    public static IReadOnlyList<string> LoadedCardNames => State.LoadedCardNames;

    public static IReadOnlyList<string> LoadedAdvantageNames => State.LoadedAdvantageNames;

    public static int PhasesCompleted => State.PhasesCompleted;

    // A primeira fase de uma rodada usa a cidade e os parametros calibrados fixos; da segunda em diante e semialeatorio.
    public static bool IsNewPhase => State.PhasesCompleted > 0;

    public static void SetArchetype(CardArchetype archetype)
    {
        State.Archetype = archetype;
        Save();
    }

    public static void AddCardReward(string cardName)
    {
        if (State.LoadedCardNames.Contains(cardName) == false)
            State.LoadedCardNames.Add(cardName);
        Save();
    }

    public static void AddAdvantageReward(string advantageName)
    {
        if (State.LoadedAdvantageNames.Contains(advantageName) == false)
            State.LoadedAdvantageNames.Add(advantageName);
        Save();
    }

    public static void IncrementPhasesCompleted()
    {
        State.PhasesCompleted++;
        Save();
    }

    // Encerra a rodada inteira, usado no Game Over.
    public static void ResetRun()
    {
        _state = new MetaProgressionState();
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(State));
        PlayerPrefs.Save();
    }
}
