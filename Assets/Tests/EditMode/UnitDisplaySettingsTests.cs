using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class UnitDisplaySettingsTests
{
    private const string PrefsKey = "Metis.RealUnits";

    private static void ResetCache()
    {
        var field = typeof(UnitDisplaySettings).GetField("_showRealUnits", BindingFlags.NonPublic | BindingFlags.Static);
        field.SetValue(null, null);
    }

    private static void ResetSubscribers()
    {
        var field = typeof(UnitDisplaySettings).GetField(nameof(UnitDisplaySettings.OnChanged), BindingFlags.NonPublic | BindingFlags.Static);
        field.SetValue(null, null);
    }

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        ResetCache();
        ResetSubscribers();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        ResetCache();
        ResetSubscribers();
    }

    [Test]
    public void ShowRealUnits_NoStoredValue_DefaultsToTrue()
    {
        Assert.That(UnitDisplaySettings.ShowRealUnits, Is.True);
    }

    [Test]
    public void ShowRealUnits_Set_PersistsAcrossCacheReload()
    {
        UnitDisplaySettings.ShowRealUnits = false;

        ResetCache();

        Assert.That(UnitDisplaySettings.ShowRealUnits, Is.False);
    }

    [Test]
    public void ShowRealUnits_Change_RaisesOnChangedOnce()
    {
        var calls = 0;
        UnitDisplaySettings.OnChanged += () => calls++;

        UnitDisplaySettings.ShowRealUnits = false;
        UnitDisplaySettings.ShowRealUnits = false;

        Assert.That(calls, Is.EqualTo(1));
    }
}
