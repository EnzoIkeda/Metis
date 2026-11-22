using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BloomSettingsTests
{
    private const string TestKey = "Metis.Tests.Bloom";

    private static void SetStatic(string name, object value)
    {
        typeof(BloomSettings).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
    }

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(TestKey);
        SetStatic("_prefsKeyOverride", TestKey);
        SetStatic("_enabled", null);
        SetStatic(nameof(BloomSettings.OnChanged), null);
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(TestKey);
        SetStatic("_enabled", null);
        SetStatic(nameof(BloomSettings.OnChanged), null);
        SetStatic("_prefsKeyOverride", null);
    }

    [Test]
    public void Enabled_NoStoredValue_DefaultsToOff()
    {
        Assert.That(BloomSettings.Enabled, Is.False);
    }

    [Test]
    public void Enabled_Set_PersistsAndRaisesOnce()
    {
        var raised = 0;
        BloomSettings.OnChanged += () => raised++;

        BloomSettings.Enabled = true;
        BloomSettings.Enabled = true;

        Assert.That(raised, Is.EqualTo(1));
        Assert.That(PlayerPrefs.GetInt(TestKey, 0), Is.EqualTo(1));
    }

    [Test]
    public void Enabled_ReadsStoredValue()
    {
        PlayerPrefs.SetInt(TestKey, 1);
        Assert.That(BloomSettings.Enabled, Is.True);
    }

    [Test]
    public void ButtonLabel_FollowsTheSetting()
    {
        var off = UIStrings.BloomButtonLabel;
        BloomSettings.Enabled = true;
        Assert.That(UIStrings.BloomButtonLabel, Is.Not.EqualTo(off));
    }
}
