using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Gravacao em asset temporario e preparo sobre os dados reais, que nao pode mudar nenhum arquivo.
public class GameTextSaverTests
{
    private const string Folder = "Assets/GameTextSaverTemp";
    private const string AssetPath = Folder + "/CardTemp.asset";

    [SetUp]
    public void CreateAsset()
    {
        AssetDatabase.CreateFolder("Assets", "GameTextSaverTemp");
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CardData>(), AssetPath);
    }

    [TearDown]
    public void DeleteAsset()
    {
        AssetDatabase.DeleteAsset(Folder);
    }

    private static string Field(string name)
    {
        return new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AssetPath)).FindProperty(name).stringValue;
    }

    [Test]
    public void AssetEditWritesBothLanguages()
    {
        GameTextSaver.ApplyToAsset(AssetPath, "_cardName", "_cardNameEn", "Nome", "Name");

        Assert.That(Field("_cardName"), Is.EqualTo("Nome"));
        Assert.That(Field("_cardNameEn"), Is.EqualTo("Name"));
    }

    [Test]
    public void PrepareBlocksDashWithoutWriting()
    {
        GameTextSaver.ApplyToAsset(AssetPath, "_cardName", "_cardNameEn", "Nome", "Name");
        var item = new GameTextItem { Origin = GameTextOrigin.Card, Pt = "Nome", En = "Name", EditKind = GameTextEditKind.Asset, Location = AssetPath, PtField = "_cardName", EnField = "_cardNameEn" };

        var plan = GameTextSaver.Prepare(item, "Nome \u2014 novo", "Name");

        Assert.That(plan.CanCommit, Is.False);
        Assert.That(GameTextSaver.Commit(plan, out _), Is.False);
        Assert.That(Field("_cardName"), Is.EqualTo("Nome"));
    }

    [TestCase(GameTextOrigin.Knowledge, GameTextSaver.FichasPath)]
    [TestCase(GameTextOrigin.News, GameTextSaver.NewsPath)]
    [TestCase(GameTextOrigin.RelationNote, GameTextSaver.RelationsPath)]
    public void PrepareOnRealContentChangesOnlyThatTextAndWritesNothing(GameTextOrigin origin, string sourcePath)
    {
        if (File.Exists(sourcePath) == false)
            Assert.Ignore("Fonte fora do repositório, ausente nesta máquina.");
        var before = File.ReadAllText(sourcePath);
        var item = GameTextCollector.Collect().First(text => text.Origin == origin && text.EditKind != GameTextEditKind.ReadOnly);

        var plan = GameTextSaver.Prepare(item, item.Pt + " teste", item.En);

        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Warnings.Where(w => w.StartsWith("A importação")), Is.Empty);
        Assert.That(plan.NewContent, Does.Contain(" teste"));
        Assert.That(plan.NewContent.Length, Is.EqualTo(before.Length + " teste".Length));
        Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(before));
    }

    [Test]
    public void EveryEditableTextKnowsWhereToWrite()
    {
        foreach (var item in GameTextCollector.Collect())
        {
            if (item.EditKind == GameTextEditKind.ReadOnly)
            {
                Assert.That(item.ReadOnlyReason, Is.Not.Empty, item.Id);
                continue;
            }
            if (item.EditKind == GameTextEditKind.Interface)
            {
                Assert.That(item.PtBranch.IsEditable, Is.True, item.Id);
                continue;
            }
            Assert.That(item.PtField, Is.Not.Empty, item.Id);
            if (item.EditKind == GameTextEditKind.News || item.EditKind == GameTextEditKind.Knowledge)
                Assert.That(item.JsonPtKey, Is.Not.Empty, item.Id);
        }
    }

    [Test]
    public void ReviewKeysAreUnique()
    {
        var keys = GameTextCollector.Collect().Select(GameTextReview.KeyOf).ToList();
        Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count));
    }
}
