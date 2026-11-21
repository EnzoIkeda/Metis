using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CityTileSetAssetsTests
{
    private const string TileSetPath = "Assets/Data/City/CityTileSet.asset";
    private const string CityScenePath = "Assets/Scenes/City_Scene.unity";

    private static CityTileSetData LoadTileSet()
    {
        var set = AssetDatabase.LoadAssetAtPath<CityTileSetData>(TileSetPath);
        Assert.That(set, Is.Not.Null, TileSetPath);
        return set;
    }

    [Test]
    public void EveryRoadShapeTheGeneratorUses_HasAPrefab()
    {
        var set = LoadTileSet();
        foreach (var shape in new[] { RoadShape.Straight, RoadShape.Corner, RoadShape.TJunction, RoadShape.Cross })
        {
            Assert.That(set.TryGetRoad(shape, out var piece), Is.True, shape.ToString());
            Assert.That(piece.RotationOffset % 90f, Is.EqualTo(0f), shape.ToString());
        }
    }

    [Test]
    public void EveryLotUse_HasStructuresWithPrefabs()
    {
        var set = LoadTileSet();
        foreach (CityLotUse use in System.Enum.GetValues(typeof(CityLotUse)))
        {
            var options = set.StructuresFor(use);
            Assert.That(options.Count, Is.GreaterThan(0), use.ToString());
            Assert.That(options.All(option => option != null && option.Prefab != null), Is.True, use.ToString());
        }
    }

    [Test]
    public void Decor_HasStreetLightAndTrafficSignal()
    {
        var set = LoadTileSet();
        Assert.That(set.StreetLight, Is.Not.Null);
        Assert.That(set.TrafficSignal, Is.Not.Null);
    }

    [Test]
    public void CityScene_BuildsFromTheTileSet_WithoutTheHandMadeCity()
    {
        var scene = EditorSceneManager.OpenScene(CityScenePath, OpenSceneMode.Additive);
        try
        {
            var placement = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlacementManager>(true)).Single();
            Assert.That(new SerializedObject(placement).FindProperty("_tileSet").objectReferenceValue, Is.EqualTo(LoadTileSet()));
            Assert.That(placement.transform.childCount, Is.EqualTo(0), "a cidade e montada em execucao, nada fica na cena");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
