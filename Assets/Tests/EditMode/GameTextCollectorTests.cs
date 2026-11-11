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
        var hasThemes = AssetDatabase.FindAssets("t:ThemedPhaseData", new[] { "Assets/Data" }).Length > 0;
        var hasDilemmas = AssetDatabase.FindAssets("t:RandomEventData", new[] { "Assets/Data" })
            .Any(guid => AssetDatabase.LoadAssetAtPath<RandomEventData>(AssetDatabase.GUIDToAssetPath(guid)).Choices.Count > 0);
        foreach (GameTextOrigin origin in Enum.GetValues(typeof(GameTextOrigin)))
        {
            if ((origin != GameTextOrigin.Theme || hasThemes) && (origin != GameTextOrigin.Dilemma || hasDilemmas))
                Assert.That(items.Any(item => item.Origin == origin), Is.True, origin.ToString());
        }
    }

    [Test]
    public void DilemmaTexts_ComeWithTheirOptions_AndAreEditedInTheJson()
    {
        const string folder = "Assets/Data/TesteDilema";
        AssetDatabase.CreateFolder("Assets/Data", "TesteDilema");
        try
        {
            var dilemma = TestDataFactory.CreateEvent("DilemaDoTeste");
            TestDataFactory.SetField(dilemma, "_title", "Titulo");
            TestDataFactory.SetField(dilemma, "_titleEn", "Title");
            TestDataFactory.SetField(dilemma, "_choices", new[] { new EventChoiceData { Label = "Opcao", LabelEn = "Option" } });
            AssetDatabase.CreateAsset(dilemma, folder + "/DilemaDoTeste.asset");

            var items = GameTextCollector.Collect().Where(item => item.Id == "DilemaDoTeste").ToList();

            Assert.That(items.Select(item => item.Origin), Is.All.EqualTo(GameTextOrigin.Dilemma));
            Assert.That(items.Select(item => item.EditKind), Is.All.EqualTo(GameTextEditKind.Dilemma));
            var label = items.Single(item => item.Field == "_choices[0].Label");
            Assert.That(label.En, Is.EqualTo("Option"));
            Assert.That(label.JsonListKey, Is.EqualTo("Dilemmas"));
            Assert.That(label.JsonNestedKey, Is.EqualTo("Choices"));
            Assert.That(label.JsonPtKey, Is.EqualTo("LabelPt"));
        }
        finally
        {
            AssetDatabase.DeleteAsset(folder);
        }
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
