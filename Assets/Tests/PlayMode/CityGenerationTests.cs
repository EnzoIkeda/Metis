using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CityGenerationTests
{
    private readonly List<Object> _created = new List<Object>();
    private string _saveDirectory;

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-city-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_saveDirectory, "run_save.json"));
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            if (obj != null)
                Object.DestroyImmediate(obj);
        _created.Clear();
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_saveDirectory))
            Directory.Delete(_saveDirectory, true);
    }

    private GameObject Template(string name)
    {
        var template = new GameObject(name);
        template.SetActive(false);
        _created.Add(template);
        return template;
    }

    private StructureData Structure(string name)
    {
        var structure = ScriptableObject.CreateInstance<StructureData>();
        structure.name = name;
        TestDataFactory.SetField(structure, "_prefab", Template(name));
        TestDataFactory.SetField(structure, "_footprintCellType", CellType.Structure);
        _created.Add(structure);
        return structure;
    }

    private CityTileSetData TileSet()
    {
        var set = ScriptableObject.CreateInstance<CityTileSetData>();
        _created.Add(set);
        var roads = new List<CityRoadPiece>();
        foreach (var shape in new[] { RoadShape.Straight, RoadShape.Corner, RoadShape.TJunction, RoadShape.Cross })
            roads.Add(new CityRoadPiece { Shape = shape, Prefab = Template("Road" + shape) });
        var lots = new List<CityLotOptions>();
        foreach (CityLotUse use in System.Enum.GetValues(typeof(CityLotUse)))
            lots.Add(new CityLotOptions { Use = use, Structures = new[] { Structure(use + "A"), Structure(use + "B") } });
        TestDataFactory.SetField(set, "_roads", roads.ToArray());
        TestDataFactory.SetField(set, "_lots", lots.ToArray());
        TestDataFactory.SetField(set, "_streetLight", Template("Light"));
        TestDataFactory.SetField(set, "_trafficSignal", Template("Signal"));
        return set;
    }

    private PlacementManager Build(CityTileSetData set)
    {
        var host = new GameObject("PlacementManager");
        host.SetActive(false);
        _created.Add(host);
        var placement = host.AddComponent<PlacementManager>();
        TestDataFactory.SetField(placement, "_width", 15);
        TestDataFactory.SetField(placement, "_height", 15);
        TestDataFactory.SetField(placement, "_tileSet", set);
        host.SetActive(true);
        return placement;
    }

    [Test]
    public void City_IsBuiltFromThePlan_WithAConnectedRoadNetwork()
    {
        var placement = Build(TileSet());

        var roads = 0;
        for (var x = 0; x < 15; x++)
        {
            for (var z = 0; z < 15; z++)
            {
                var expected = placement.Plan.IsRoad(x, z) ? CellType.Road : CellType.Structure;
                Assert.That(placement.Grid[x, z], Is.EqualTo(expected), $"celula {x},{z}");
                if (expected == CellType.Road)
                    roads++;
            }
        }

        var network = new RoadNetwork(placement.Grid);
        Assert.That(network.RoadCells.Count, Is.EqualTo(roads));
        var seen = new HashSet<(int, int)> { network.RoadCells[0] };
        var stack = new Stack<(int X, int Z)>();
        stack.Push(network.RoadCells[0]);
        while (stack.Count > 0)
            foreach (var next in network.GetNeighbors(stack.Pop()))
                if (seen.Add(next))
                    stack.Push(next);
        Assert.That(seen.Count, Is.EqualTo(roads), "todo carro alcanca toda rua");

        // Uma peca por celula mais o recipiente da decoracao.
        Assert.That(placement.transform.childCount, Is.EqualTo(15 * 15 + 1));
        Assert.That(placement.transform.Find("Decor").childCount, Is.EqualTo(CityDecor.Plan(placement.Plan).Count));
    }

    [Test]
    public void SavedPhase_RebuildsTheSameCity()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Sustentabilidade);
        MetaProgressionManager.SavePhase(new PhaseSaveData { LayoutSeed = 777UL });
        var set = TileSet();

        var first = Build(set);
        var second = Build(set);

        Assert.That(first.LayoutSeed, Is.EqualTo(777UL));
        Assert.That(second.LayoutSeed, Is.EqualTo(777UL));
        for (var x = 0; x < 15; x++)
        {
            for (var z = 0; z < 15; z++)
            {
                Assert.That(second.Plan.RoadAt(x, z), Is.EqualTo(first.Plan.RoadAt(x, z)));
                var position = new Vector3Int(x, 0, z);
                Assert.That(second.GetStructureDataAt(position), Is.EqualTo(first.GetStructureDataAt(position)));
            }
        }
    }

    [Test]
    public void NewPhase_GetsAFreshSeed()
    {
        var set = TileSet();
        var first = Build(set);
        var second = Build(set);

        Assert.That(second.LayoutSeed, Is.Not.EqualTo(first.LayoutSeed));
    }

    [Test]
    public void WithoutTileSet_TheGridStillHasTheCellTypes()
    {
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Sem conjunto de pecas"));
        var placement = Build(null);

        Assert.That(placement.Grid[0, 0], Is.EqualTo(placement.Plan.IsRoad(0, 0) ? CellType.Road : CellType.Structure));
        Assert.That(placement.transform.childCount, Is.EqualTo(0));
    }
}
