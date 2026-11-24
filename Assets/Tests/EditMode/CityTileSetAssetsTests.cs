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
    public void Coast_HasEveryPiece_WithoutDependingOnExtras()
    {
        var tileSet = LoadTileSet();
        Assert.That(tileSet.Water, Is.Not.Null);
        Assert.That(tileSet.Beach, Is.Not.Null);
        var pieces = new System.Collections.Generic.List<GameObject> { tileSet.Water, tileSet.Beach };
        foreach (CoastDecorKind kind in System.Enum.GetValues(typeof(CoastDecorKind)))
        {
            Assert.That(tileSet.CoastPieces(kind).Count, Is.GreaterThan(0), kind.ToString());
            pieces.AddRange(tileSet.CoastPieces(kind));
        }
        foreach (var piece in pieces)
        {
            Assert.That(piece, Is.Not.Null);
            var path = UnityEditor.AssetDatabase.GetAssetPath(piece);
            foreach (var dependency in UnityEditor.AssetDatabase.GetDependencies(path, true))
                Assert.That(dependency, Does.Not.StartWith("Assets/Extras"), path);
        }
    }

    [Test]
    public void ThePortTheme_HasTheCoast_AndTheOthersDoNot()
    {
        var library = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemedPhaseLibraryData>("Assets/Data/Themes/ThemedPhaseLibrary.asset");
        foreach (var theme in library.Themes)
            Assert.That(theme.Coast, Is.EqualTo(theme.name == "Tema_PortoDaTemporada" ? CoastSide.South : CoastSide.None), theme.name);
    }

    [Test]
    public void TrafficCars_ExistAndDoNotDependOnExtras()
    {
        var previous = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        try
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/City_Scene.unity");
            var cars = Object.FindFirstObjectByType<CarTrafficController>(FindObjectsInactive.Include);
            var list = new UnityEditor.SerializedObject(cars).FindProperty("_carPrefabs");
            Assert.That(list.arraySize, Is.GreaterThanOrEqualTo(11));
            for (var i = 0; i < list.arraySize; i++)
            {
                var car = list.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(car, Is.Not.Null, $"carro {i}");
                foreach (var dependency in UnityEditor.AssetDatabase.GetDependencies(UnityEditor.AssetDatabase.GetAssetPath(car), true))
                    Assert.That(dependency, Does.Not.StartWith("Assets/Extras"), car.name);
            }
        }
        finally
        {
            if (string.IsNullOrEmpty(previous) == false)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previous);
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
