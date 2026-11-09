using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ThemedPhaseTests
{
    private string _directory;
    private GameObject _host;

    private static void SetStatic(string name, object value)
    {
        typeof(MetaProgressionManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
    }

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "metis-theme-" + System.Guid.NewGuid().ToString("N"));
        SetStatic("_savePathOverride", Path.Combine(_directory, "run_save.json"));
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null)
            Object.DestroyImmediate(_host);
        MetaProgressionManager.ResetRun();
        SetStatic("_savePathOverride", null);
        SetStatic("_save", null);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private static ThemedPhaseLibraryData Library(string themeName)
    {
        var theme = ScriptableObject.CreateInstance<ThemedPhaseData>();
        theme.name = themeName;
        TestDataFactory.SetField(theme, "_overrides", new[] { new CityParameterOverride { Parameter = CityParameterType.Renda, InitialValue = 31f } });
        var library = ScriptableObject.CreateInstance<ThemedPhaseLibraryData>();
        TestDataFactory.SetField(library, "_themes", new[] { theme });
        return library;
    }

    private CityStatsManager Build(ThemedPhaseLibraryData library)
    {
        _host = new GameObject("Stats");
        _host.SetActive(false);
        var manager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.SetField(manager, "_initialParameters", new[]
        {
            new CityParameterConfig { Parameter = CityParameterType.Renda, InitialValue = 55f, MaxValue = 100f, CriticalLevel = 10f },
        });
        TestDataFactory.SetField(manager, "_themes", library);
        _host.SetActive(true);
        return manager;
    }

    private static void Choose(string themeName)
    {
        MetaProgressionManager.ChooseMapNode(new PhaseMapNode { Id = themeName, Type = PhaseMapNodeType.Themed, IsAvailable = true });
    }

    [UnityTest]
    public IEnumerator ChosenTheme_SetsTheCityValues()
    {
        Choose("Tema");

        var manager = Build(Library("Tema"));
        yield return null;

        Assert.That(manager.Theme, Is.Not.Null);
        Assert.That(manager.Stats.GetValue(CityParameterType.Renda), Is.EqualTo(31f));
        Assert.That(manager.PresetName, Is.Empty);
    }

    [UnityTest]
    public IEnumerator ThemeThatNoLongerExists_RestartsThePhaseWithoutIt()
    {
        Choose("Sumiu");
        MetaProgressionManager.SavePhase(new PhaseSaveData { TurnIndex = 4 });

        var manager = Build(Library("Tema"));
        yield return null;

        Assert.That(manager.Theme, Is.Null);
        Assert.That(MetaProgressionManager.SavedPhase, Is.Null);
    }
}
