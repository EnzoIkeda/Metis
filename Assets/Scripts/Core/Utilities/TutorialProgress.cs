using UnityEngine;

// Dicas do tutorial ja vistas, persistidas via PlayerPrefs. E progresso do jogador: fica fora do save
// da rodada e sobrevive ao fim dela.
public static class TutorialProgress
{
    private const string PrefsKey = "Metis.Tutorial";

    // Chave alternativa so pros testes, pra nunca tocar no progresso de verdade.
    private static string _prefsKeyOverride;

    private static AlmanacState _state;

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

    public static bool WasSeen(TutorialTip tip) => State.IsUnlocked(tip.ToString());

    public static void MarkSeen(TutorialTip tip)
    {
        if (tip == TutorialTip.None || State.Unlock(tip.ToString()) == false)
            return;
        PlayerPrefs.SetString(Key, State.Serialize());
        PlayerPrefs.Save();
    }

    // Volta ao comeco pro botao de rever o tutorial.
    public static void Reset()
    {
        State.Clear();
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
