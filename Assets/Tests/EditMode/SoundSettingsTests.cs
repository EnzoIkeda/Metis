using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class SoundSettingsTests
{
    private const string PrefsKey = "Metis.Sound";

    private static void ResetCache()
    {
        var field = typeof(SoundSettings).GetField("_state", BindingFlags.NonPublic | BindingFlags.Static);
        field.SetValue(null, null);
    }

    private static void ResetSubscribers()
    {
        var field = typeof(SoundSettings).GetField(nameof(SoundSettings.OnChanged), BindingFlags.NonPublic | BindingFlags.Static);
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
    public void Volumes_NoStoredValue_DefaultToFull()
    {
        Assert.That(SoundSettings.MusicVolume, Is.EqualTo(1f));
        Assert.That(SoundSettings.SfxVolume, Is.EqualTo(1f));
    }

    [Test]
    public void Volumes_SetValues_PersistAcrossCacheReload()
    {
        SoundSettings.MusicVolume = 0.4f;
        SoundSettings.SfxVolume = 0.7f;

        ResetCache();

        Assert.That(SoundSettings.MusicVolume, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(SoundSettings.SfxVolume, Is.EqualTo(0.7f).Within(0.0001f));
    }

    [Test]
    public void Volumes_OutOfRange_AreClamped()
    {
        SoundSettings.MusicVolume = 3f;
        SoundSettings.SfxVolume = -1f;

        Assert.That(SoundSettings.MusicVolume, Is.EqualTo(1f));
        Assert.That(SoundSettings.SfxVolume, Is.EqualTo(0f));
    }

    [Test]
    public void Volume_SetSameValue_DoesNotRaiseChangedEvent()
    {
        SoundSettings.MusicVolume = 0.5f;
        var raised = false;
        SoundSettings.OnChanged += () => raised = true;

        SoundSettings.MusicVolume = 0.5f;

        Assert.That(raised, Is.False);
    }

    [Test]
    public void Volume_SetDifferentValue_RaisesChangedEvent()
    {
        var raised = 0;
        SoundSettings.OnChanged += () => raised++;

        SoundSettings.MusicVolume = 0.5f;
        SoundSettings.SfxVolume = 0.2f;

        Assert.That(raised, Is.EqualTo(2));
    }
}
