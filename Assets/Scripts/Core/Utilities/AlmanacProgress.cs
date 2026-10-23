using System;
using UnityEngine;

// Fichas do almanaque ja desbloqueadas, persistidas via PlayerPrefs. E progresso do jogador:
// sobrevive ao fim da rodada e nunca e apagado junto com o save dela.
public static class AlmanacProgress
{
    private const string PrefsKey = "Metis.Almanac";

    // Chave alternativa so pros testes, pra nunca tocar no progresso de verdade.
    private static string _prefsKeyOverride;

    private static AlmanacState _state;

    // Recebe o id da ficha recem-desbloqueada.
    public static event Action<string> OnUnlocked;

    private static string Key => _prefsKeyOverride ?? PrefsKey;

    private static AlmanacState State
    {
        get
        {
            if (_state == null)
                _state = AlmanacState.Parse(PlayerPrefs.GetString(Key, string.Empty));
            return _state;
        }
    }

    public static int UnlockedCount => State.UnlockedCount;

    public static bool IsUnlocked(string id) => State.IsUnlocked(id);

    public static bool IsUnlocked(KnowledgeEntryData entry) => entry != null && State.IsUnlocked(entry.Id);

    public static bool Unlock(KnowledgeEntryData entry)
    {
        return entry != null && Unlock(entry.Id);
    }

    // Desbloqueia todas as fichas da lista; devolve quantas eram novas.
    public static int UnlockAll(System.Collections.Generic.IEnumerable<KnowledgeEntryData> entries)
    {
        var count = 0;
        if (entries == null)
            return count;
        foreach (var entry in entries)
        {
            if (Unlock(entry))
                count++;
        }
        return count;
    }

    // Volta o almanaque ao zero, como num jogo novo.
    public static void Reset()
    {
        State.Clear();
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    // Grava na hora: o desbloqueio nao pode se perder se o app fechar no meio da fase.
    public static bool Unlock(string id)
    {
        if (State.Unlock(id) == false)
            return false;

        PlayerPrefs.SetString(Key, State.Serialize());
        PlayerPrefs.Save();
        OnUnlocked?.Invoke(id.Trim());
        return true;
    }
}
