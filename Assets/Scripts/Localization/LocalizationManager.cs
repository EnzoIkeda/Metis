using System;
using UnityEngine;

// Estado global de idioma do jogo, persistido entre cenas.
public static class LocalizationManager
{
    private const string PrefsKey = "Metis.Language";

    private static Language? _current;

    public static event Action OnLanguageChanged;

    public static Language Current
    {
        get
        {
            if (_current == null)
                _current = (Language)PlayerPrefs.GetInt(PrefsKey, (int)Language.English);
            return _current.Value;
        }
        set
        {
            if (_current == value)
                return;

            _current = value;
            PlayerPrefs.SetInt(PrefsKey, (int)value);
            PlayerPrefs.Save();
            OnLanguageChanged?.Invoke();
        }
    }
}
