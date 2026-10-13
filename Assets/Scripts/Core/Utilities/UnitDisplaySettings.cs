using System;
using UnityEngine;

// Preferencia de exibir os parametros em unidade real ou em indice, persistida via PlayerPrefs.
public static class UnitDisplaySettings
{
    private const string PrefsKey = "Metis.RealUnits";

    private static bool? _showRealUnits;

    public static event Action OnChanged;

    // Padrao: unidade real.
    public static bool ShowRealUnits
    {
        get
        {
            if (_showRealUnits == null)
                _showRealUnits = PlayerPrefs.GetInt(PrefsKey, 1) == 1;
            return _showRealUnits.Value;
        }
        set
        {
            if (_showRealUnits == value)
                return;

            _showRealUnits = value;
            PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
