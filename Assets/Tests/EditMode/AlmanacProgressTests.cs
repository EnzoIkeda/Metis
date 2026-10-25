using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class AlmanacProgressTests
{
    // Chave so dos testes, pra nunca apagar o progresso de verdade do Editor.
    private const string TestKey = "Metis.Almanac.Tests";

    private static void SetStatic(string field, object value)
    {
        typeof(AlmanacProgress).GetField(field, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
    }

    private static void ResetSubscribers()
    {
        typeof(AlmanacProgress).GetField(nameof(AlmanacProgress.OnUnlocked), BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
    }

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(TestKey);
        SetStatic("_prefsKeyOverride", TestKey);
        SetStatic("_state", null);
        ResetSubscribers();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(TestKey);
        SetStatic("_prefsKeyOverride", null);
        SetStatic("_state", null);
        ResetSubscribers();
    }

    [Test]
    public void Unlock_PersistsAcrossCacheReload()
    {
        AlmanacProgress.Unlock("Conceito_Colapso");

        SetStatic("_state", null);

        Assert.That(AlmanacProgress.IsUnlocked("Conceito_Colapso"), Is.True);
        Assert.That(AlmanacProgress.UnlockedCount, Is.EqualTo(1));
    }

    [Test]
    public void Unlock_RaisesOnUnlockedOnlyTheFirstTime()
    {
        var received = new System.Collections.Generic.List<string>();
        AlmanacProgress.OnUnlocked += received.Add;

        AlmanacProgress.Unlock("Carta_VLTMetroLeve");
        AlmanacProgress.Unlock("Carta_VLTMetroLeve");

        Assert.That(received, Is.EqualTo(new[] { "Carta_VLTMetroLeve" }));
    }

    [Test]
    public void Unlock_NullEntry_IsIgnored()
    {
        Assert.That(AlmanacProgress.Unlock((KnowledgeEntryData)null), Is.False);
        Assert.That(AlmanacProgress.IsUnlocked((KnowledgeEntryData)null), Is.False);
        Assert.That(AlmanacProgress.UnlockedCount, Is.Zero);
    }

    [Test]
    public void Reset_ClearsEverythingAndTheStoredKey()
    {
        AlmanacProgress.Unlock("Conceito_Colapso");

        AlmanacProgress.Reset();

        Assert.That(AlmanacProgress.UnlockedCount, Is.Zero);
        Assert.That(PlayerPrefs.HasKey(TestKey), Is.False);
        SetStatic("_state", null);
        Assert.That(AlmanacProgress.IsUnlocked("Conceito_Colapso"), Is.False);
    }

    [Test]
    public void UnlockAll_CountsOnlyTheNewEntries()
    {
        var first = ScriptableObject.CreateInstance<KnowledgeEntryData>();
        first.name = "A";
        var second = ScriptableObject.CreateInstance<KnowledgeEntryData>();
        second.name = "B";
        try
        {
            AlmanacProgress.Unlock(first);

            Assert.That(AlmanacProgress.UnlockAll(new[] { first, second, null }), Is.EqualTo(1));
            Assert.That(AlmanacProgress.UnlockedCount, Is.EqualTo(2));
            Assert.That(AlmanacProgress.UnlockAll(null), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void ResetRun_DoesNotTouchTheAlmanac()
    {
        AlmanacProgress.Unlock("Conceito_Colapso");
        var saveField = typeof(MetaProgressionManager).GetField("_savePathOverride", BindingFlags.NonPublic | BindingFlags.Static);
        var tempSave = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "metis-almanac-" + System.Guid.NewGuid().ToString("N") + ".json");
        saveField.SetValue(null, tempSave);
        try
        {
            MetaProgressionManager.ResetRun();
        }
        finally
        {
            saveField.SetValue(null, null);
            typeof(MetaProgressionManager).GetField("_save", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            if (System.IO.File.Exists(tempSave))
                System.IO.File.Delete(tempSave);
        }

        SetStatic("_state", null);
        Assert.That(AlmanacProgress.IsUnlocked("Conceito_Colapso"), Is.True);
    }
}
