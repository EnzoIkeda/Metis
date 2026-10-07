using System;
using UnityEngine;

// Volumes salvos (0 a 1), padrao 100%.
[Serializable]
public class SoundSettingsState
{
    public float MusicVolume = 1f;
    public float SfxVolume = 1f;
}

// Preferencias de som persistidas entre sessoes via PlayerPrefs, mesmo padrao do idioma.
public static class SoundSettings
{
    private const string PrefsKey = "Metis.Sound";

    private static SoundSettingsState _state;

    public static event Action OnChanged;

    private static SoundSettingsState State
    {
        get
        {
            if (_state == null)
            {
                var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                _state = string.IsNullOrEmpty(json) ? new SoundSettingsState() : JsonUtility.FromJson<SoundSettingsState>(json);
            }
            return _state;
        }
    }

    public static float MusicVolume
    {
        get => State.MusicVolume;
        set
        {
            var clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(State.MusicVolume, clamped))
                return;

            State.MusicVolume = clamped;
            SaveAndNotify();
        }
    }

    public static float SfxVolume
    {
        get => State.SfxVolume;
        set
        {
            var clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(State.SfxVolume, clamped))
                return;

            State.SfxVolume = clamped;
            SaveAndNotify();
        }
    }

    private static void SaveAndNotify()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(State));
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}
