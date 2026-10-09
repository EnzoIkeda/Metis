using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Rodada atual (meta-progressao e fase em andamento), persistida num arquivo JSON em persistentDataPath.
// O arquivo existir e o mesmo que haver uma rodada pra continuar.
public static class MetaProgressionManager
{
    private const string FileName = "run_save.json";

    // Chave antiga, de quando a meta-progressao morava no PlayerPrefs.
    private const string LegacyPrefsKey = "Metis.MetaProgression";

    public const string CitySceneName = "City_Scene";
    public const string PhaseMapSceneName = "PhaseMap";

    // Caminho alternativo so pros testes, pra nunca tocar no save de verdade.
    private static string _savePathOverride;

    private static RunSaveData _save;
    private static bool _runInProgress;
    private static bool _discardedIncompatibleSave;

    private static string SavePath => _savePathOverride ?? Path.Combine(Application.persistentDataPath, FileName);

    private static RunSaveData Save
    {
        get
        {
            if (_save == null)
                Load();
            return _save;
        }
    }

    private static MetaProgressionState State => Save.Meta;

    public static CardArchetype Archetype => State.Archetype;

    public static IReadOnlyList<string> LoadedCardNames => State.LoadedCardNames;

    public static IReadOnlyList<string> LoadedAdvantageNames => State.LoadedAdvantageNames;

    public static int PhasesCompleted => State.PhasesCompleted;

    // A primeira fase de uma rodada usa a cidade e os parametros calibrados fixos; da segunda em diante e semialeatorio.
    public static bool IsNewPhase => State.PhasesCompleted > 0;

    public static bool HasRunInProgress
    {
        get
        {
            _ = Save;
            return _runInProgress;
        }
    }

    // Cena onde a rodada salva recomeca.
    public static string ContinueSceneName => Save.InPhaseMap ? PhaseMapSceneName : CitySceneName;

    // Fase em andamento pra retomar, ou null quando a fase deve comecar do zero.
    public static PhaseSaveData SavedPhase => Save.HasPhase ? Save.Phase : null;

    // Um save de versao incompativel foi descartado na leitura; o aviso so e devolvido uma vez.
    public static bool ConsumeDiscardedSaveNotice()
    {
        _ = Save;
        var discarded = _discardedIncompatibleSave;
        _discardedIncompatibleSave = false;
        return discarded;
    }

    public static void SetArchetype(CardArchetype archetype)
    {
        State.Archetype = archetype;
        Write();
    }

    public static void AddCardReward(string cardName)
    {
        if (State.LoadedCardNames.Contains(cardName) == false)
            State.LoadedCardNames.Add(cardName);
        Write();
    }

    public static void AddAdvantageReward(string advantageName)
    {
        if (State.LoadedAdvantageNames.Contains(advantageName) == false)
            State.LoadedAdvantageNames.Add(advantageName);
        Write();
    }

    // Sai do mapa pra proxima fase, que comeca do zero.
    public static void IncrementPhasesCompleted()
    {
        State.PhasesCompleted++;
        Save.InPhaseMap = false;
        ClearPhase();
        Write();
    }

    public static void SavePhase(PhaseSaveData phase)
    {
        Save.InPhaseMap = false;
        Save.HasPhase = true;
        Save.Phase = phase;
        Write();
    }

    // A fase salva nao pode ser retomada (conteudo do jogo mudou), entao ela recomeca do zero.
    public static void DiscardSavedPhase()
    {
        ClearPhase();
        Write();
    }

    // Fase vencida e recompensa escolhida: a rodada continua a partir do mapa.
    public static void EnterPhaseMap()
    {
        Save.InPhaseMap = true;
        ClearPhase();
        Write();
    }

    // Encerra a rodada inteira, usado no Game Over e ao escolher o baralho de uma rodada nova.
    public static void ResetRun()
    {
        _save = new RunSaveData();
        _runInProgress = false;
        try
        {
            RunSaveFile.Delete(SavePath);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning($"[MetaProgression] Não foi possível apagar o save: {exception.Message}");
        }
    }

    private static void ClearPhase()
    {
        Save.HasPhase = false;
        Save.Phase = new PhaseSaveData();
    }

    private static void Load()
    {
        _save = new RunSaveData();
        _runInProgress = false;

        if (PlayerPrefs.HasKey(LegacyPrefsKey))
        {
            PlayerPrefs.DeleteKey(LegacyPrefsKey);
            PlayerPrefs.Save();
        }

        string json;
        try
        {
            json = RunSaveFile.Read(SavePath);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning($"[MetaProgression] Não foi possível ler o save: {exception.Message}");
            return;
        }

        if (json == null)
            return;

        RunSaveData loaded = null;
        try
        {
            loaded = JsonUtility.FromJson<RunSaveData>(json);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"[MetaProgression] Save ilegível: {exception.Message}");
        }

        if (RunSaveRules.IsCompatible(loaded) == false)
        {
            Debug.LogWarning("[MetaProgression] Save de versão incompatível ou corrompido, descartado.");
            _discardedIncompatibleSave = true;
            ResetRun();
            return;
        }

        _save = loaded;
        _runInProgress = true;
    }

    private static void Write()
    {
        try
        {
            RunSaveFile.Write(SavePath, JsonUtility.ToJson(Save));
            _runInProgress = true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning($"[MetaProgression] Não foi possível gravar o save: {exception.Message}");
        }
    }
}
