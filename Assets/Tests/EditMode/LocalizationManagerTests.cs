using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LocalizationManagerTests
{
    private const string PrefsKey = "Metis.Language";

    private static void ResetCache()
    {
        var field = typeof(LocalizationManager).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static);
        field.SetValue(null, null);
    }

    private static void ResetSubscribers()
    {
        var field = typeof(LocalizationManager).GetField(nameof(LocalizationManager.OnLanguageChanged), BindingFlags.NonPublic | BindingFlags.Static);
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
        // PlayerPrefs e persistencia de verdade (registro/plist), nao deixar estado de teste vazando.
        PlayerPrefs.DeleteKey(PrefsKey);
        ResetCache();
        ResetSubscribers();
    }

    [Test]
    public void Current_NoStoredValue_DefaultsToEnglish()
    {
        Assert.That(LocalizationManager.Current, Is.EqualTo(Language.English));
    }

    [Test]
    public void Current_SetValue_PersistsAcrossCacheReload()
    {
        LocalizationManager.Current = Language.Portuguese;

        ResetCache();

        Assert.That(LocalizationManager.Current, Is.EqualTo(Language.Portuguese));
    }

    [Test]
    public void Current_SetSameValue_DoesNotRaiseChangedEvent()
    {
        LocalizationManager.Current = Language.Portuguese;
        var raised = false;
        LocalizationManager.OnLanguageChanged += () => raised = true;

        LocalizationManager.Current = Language.Portuguese;

        Assert.That(raised, Is.False);
    }

    [Test]
    public void Current_SetDifferentValue_RaisesChangedEvent()
    {
        var raised = false;
        LocalizationManager.OnLanguageChanged += () => raised = true;

        LocalizationManager.Current = Language.Portuguese;

        Assert.That(raised, Is.True);
    }
}
