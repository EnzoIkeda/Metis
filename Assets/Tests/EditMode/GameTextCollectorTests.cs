using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

// Confere a coleta sobre os assets reais: toda origem aparece e nenhum asset e alterado.
public class GameTextCollectorTests
{
    [Test]
    public void EveryOriginHasTexts()
    {
        var items = GameTextCollector.Collect();
        foreach (GameTextOrigin origin in Enum.GetValues(typeof(GameTextOrigin)))
            Assert.That(items.Any(item => item.Origin == origin), Is.True, origin.ToString());
    }

    [Test]
    public void CoversEveryCardAndNewsStory()
    {
        var items = GameTextCollector.Collect();
        Assert.That(items.Count(item => item.Origin == GameTextOrigin.Card), Is.EqualTo(AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data" }).Length * 2));
        Assert.That(items.Count(item => item.Origin == GameTextOrigin.News), Is.EqualTo(AssetDatabase.FindAssets("t:NewsStoryData", new[] { "Assets/Data" }).Length * 2 + AssetDatabase.FindAssets("t:NewsColumnData", new[] { "Assets/Data" }).Length));
    }

    [Test]
    public void CoversEveryNewsColumn()
    {
        var columns = GameTextCollector.Collect().Where(item => item.EditKind == GameTextEditKind.NewsColumn).ToList();
        Assert.That(columns.Count, Is.EqualTo(AssetDatabase.FindAssets("t:NewsColumnData", new[] { "Assets/Data" }).Length));
        Assert.That(columns.Count, Is.GreaterThan(0));
        Assert.That(columns.All(item => item.Origin == GameTextOrigin.News), Is.True);
    }

    [Test]
    public void EveryTextPointsToAnExistingAsset()
    {
        var locations = GameTextCollector.Collect().Select(item => item.Location).Distinct();
        foreach (var location in locations)
            Assert.That(AssetDatabase.LoadMainAssetAtPath(location), Is.Not.Null, location);
    }

    [Test]
    public void CollectingLeavesNoDirtyAssets()
    {
        GameTextCollector.Collect();
        var card = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data" }).First();
        Assert.That(EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(card))), Is.False);
    }
}
