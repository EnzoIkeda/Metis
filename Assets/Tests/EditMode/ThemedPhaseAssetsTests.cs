using System.Linq;
using NUnit.Framework;
using UnityEditor;

// Confere as fases tematicas reais: textos nos dois idiomas, eventos que existem e ficha ligada.
public class ThemedPhaseAssetsTests
{
    private static ThemedPhaseLibraryData Library => AssetDatabase.LoadAssetAtPath<ThemedPhaseLibraryData>(ThemedPhaseImporter.LibraryPath);

    [Test]
    public void EveryTheme_HasBothLanguages()
    {
        var fields = new[] { "_themeName", "_themeNameEn", "_summary", "_summaryEn", "_opening", "_openingEn", "_victory", "_victoryEn", "_inspiration", "_inspirationEn" };
        foreach (var theme in Library.Themes)
        {
            var so = new SerializedObject(theme);
            foreach (var field in fields)
                Assert.That(so.FindProperty(field).stringValue, Is.Not.Empty, $"{theme.name}.{field}");
        }
    }

    [Test]
    public void EveryTheme_UsesRealEventsAndHasAnEntryWithSources()
    {
        var events = AssetDatabase.FindAssets("t:RandomEventData", new[] { "Assets/Data" })
            .Select(guid => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid))).ToHashSet();

        Assert.That(Library.Themes.Count, Is.EqualTo(3));
        foreach (var theme in Library.Themes)
        {
            Assert.That(theme.AllowedEventIds, Is.SubsetOf(events), theme.name);
            Assert.That(theme.KnowledgeEntry, Is.Not.Null, theme.name);
            Assert.That(theme.KnowledgeEntry.Sources.Count, Is.GreaterThan(0), theme.name);
        }
    }
}
