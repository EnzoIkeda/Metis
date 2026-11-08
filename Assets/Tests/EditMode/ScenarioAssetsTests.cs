using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Confere os cenarios reais e a persistencia das estrelas.
public class ScenarioAssetsTests
{
    private const string TestKey = "Metis.Scenarios.Test";

    private static ScenarioLibraryData Library => AssetDatabase.LoadAssetAtPath<ScenarioLibraryData>(ScenarioImporter.LibraryPath);

    private static void SetProgressField(string name, object value)
    {
        typeof(ScenarioProgress).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
    }

    [SetUp]
    public void SetUp()
    {
        SetProgressField("_prefsKeyOverride", TestKey);
        SetProgressField("_state", null);
        PlayerPrefs.DeleteKey(TestKey);
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(TestKey);
        SetProgressField("_prefsKeyOverride", null);
        SetProgressField("_state", null);
    }

    [Test]
    public void EveryScenario_HasAThemeAndValidObjectives()
    {
        Assert.That(Library.Scenarios.Count, Is.EqualTo(3));
        foreach (var scenario in Library.Scenarios)
        {
            Assert.That(scenario.Theme, Is.Not.Null, scenario.name);
            Assert.That(scenario.Objectives.Count, Is.InRange(ScenarioImportFormat.MinObjectives, ScenarioImportFormat.MaxObjectives), scenario.name);
            foreach (var objective in scenario.Objectives)
                Assert.That(objective.ByTurn, Is.InRange(1, TurnMachine.DefaultVictoryTurnCount), scenario.name);
        }
    }

    [Test]
    public void ScenarioSelection_IsInTheBuild()
    {
        var path = $"Assets/Scenes/{MetaProgressionManager.ScenarioSelectionSceneName}.unity";
        Assert.That(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path), Has.Member(path));
    }

    [Test]
    public void Progress_KeepsTheBestAndSurvivesAReload()
    {
        ScenarioProgress.Record("A", 2);
        ScenarioProgress.Record("A", 1);

        SetProgressField("_state", null);
        Assert.That(ScenarioProgress.BestStars("A"), Is.EqualTo(2));
    }
}
