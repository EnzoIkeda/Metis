using System;
using UnityEngine;

// Preferencia de ligar o brilho (bloom) das luzes da cidade, persistida via PlayerPrefs.
public static class BloomSettings
{
    private const string PrefsKey = "Metis.Bloom";

    // Testes trocam a chave para nao mexer na preferencia real.
    private static string _prefsKeyOverride;

    private static bool? _enabled;

    public static event Action OnChanged;

    private static string Key => _prefsKeyOverride ?? PrefsKey;

    // Padrao: desligado, pelo custo no celular.
    public static bool Enabled
    {
        get
        {
            if (_enabled == null)
                _enabled = PlayerPrefs.GetInt(Key, 0) == 1;
            return _enabled.Value;
        }
        set
        {
            if (_enabled == value)
                return;

            _enabled = value;
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
