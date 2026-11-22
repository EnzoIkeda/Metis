using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CardMarkLibraryAssetsTests
{
    private const string LibraryPath = "Assets/Data/City/CardMarkLibrary.asset";

    private static CardMarkLibraryData Library()
    {
        var library = AssetDatabase.LoadAssetAtPath<CardMarkLibraryData>(LibraryPath);
        Assert.That(library, Is.Not.Null, LibraryPath);
        return library;
    }

    private static List<CardData> AllCards()
    {
        return AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Card" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
            .ToList();
    }

    [Test]
    public void EveryCard_HasExactlyOneEntry()
    {
        var entries = Library().Entries;
        foreach (var card in AllCards())
            Assert.That(entries.Count(entry => entry.Card == card), Is.EqualTo(1), card.name);
        Assert.That(entries.All(entry => entry.Card != null), Is.True, "entrada sem carta");
    }

    [Test]
    public void EveryMark_HasPiecesAndAValidSize()
    {
        foreach (var entry in Library().Entries.Where(entry => entry.Kind != CardMarkKind.None))
        {
            var name = entry.Card.name;
            Assert.That(entry.Pieces, Is.Not.Null.And.Not.Empty, name);
            Assert.That(entry.Pieces.All(piece => piece != null), Is.True, name);
            Assert.That(entry.Size, Is.InRange(1, CardMarkPlanner.MaxSize), name);
            Assert.That(entry.ResultIsGreen && entry.ResultHasFlatRoof, Is.False, name);
            if (entry.Merge || entry.ResultIsGreen || entry.ResultHasFlatRoof)
                Assert.That(entry.Kind, Is.EqualTo(CardMarkKind.ReplaceLot), name);
        }
    }

    [Test]
    public void FreeActionsAndExtraCards_LeaveNoMark()
    {
        var entries = Library().Entries;
        foreach (var card in AllCards().Where(card => CardRules.IsFreeAction(card) || card.IsExtraOnly))
            Assert.That(entries.Single(entry => entry.Card == card).Kind, Is.EqualTo(CardMarkKind.None), card.name);
    }

    [Test]
    public void MostCards_ChangeTheCity()
    {
        // A marca so faz sentido se a maior parte das jogadas aparecer na cidade.
        var entries = Library().Entries;
        Assert.That(entries.Count(entry => entry.Kind != CardMarkKind.None), Is.GreaterThanOrEqualTo(entries.Count * 3 / 4));
        Assert.That(entries.Count(entry => entry.Kind == CardMarkKind.ReplaceLot), Is.GreaterThan(entries.Count / 3));
    }

    [Test]
    public void Pieces_AndTheDust_DoNotDependOnExtras()
    {
        var library = Library();
        Assert.That(library.LandingDust, Is.Not.Null);
        var roots = library.Entries.Where(entry => entry.Pieces != null).SelectMany(entry => entry.Pieces).Where(piece => piece != null)
            .Select(AssetDatabase.GetAssetPath).Append(AssetDatabase.GetAssetPath(library.LandingDust)).Distinct().ToArray();
        var extras = AssetDatabase.GetDependencies(roots, true).Where(path => path.StartsWith("Assets/Extras/")).ToList();
        Assert.That(extras, Is.Empty);
    }

    [Test]
    public void CityScene_HasTheMarkControllerWired()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/City_Scene.unity", OpenSceneMode.Additive);
        try
        {
            var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardMarkController>(true)).Single();
            var serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("_library").objectReferenceValue, Is.EqualTo(Library()));
            Assert.That(serialized.FindProperty("_turnManager").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_placementManager").objectReferenceValue, Is.Not.Null);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
