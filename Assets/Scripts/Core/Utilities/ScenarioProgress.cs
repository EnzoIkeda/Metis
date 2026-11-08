using UnityEngine;

// Estrelas dos cenarios, persistidas via PlayerPrefs. E progresso do jogador: fica fora do save e sobrevive a ele.
public static class ScenarioProgress
{
    private const string PrefsKey = "Metis.Scenarios";

    // Chave alternativa so pros testes, pra nunca tocar no progresso de verdade.
    private static string _prefsKeyOverride;

    private static ScenarioProgressState _state;

    private static string Key => _prefsKeyOverride ?? PrefsKey;

    private static ScenarioProgressState State
    {
        get
        {
            if (_state == null)
                _state = ScenarioProgressState.Parse(PlayerPrefs.GetString(Key, string.Empty));
            return _state;
        }
    }

    public static int BestStars(string scenarioId) => State.BestStars(scenarioId);

    public static void Record(string scenarioId, int stars)
    {
        if (State.Record(scenarioId, stars) == false)
            return;
        PlayerPrefs.SetString(Key, State.Serialize());
        PlayerPrefs.Save();
    }
}
