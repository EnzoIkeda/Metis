using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CardMarkControllerTests
{
    private readonly List<Object> _assets = new List<Object>();
    private readonly List<GameObject> _scene = new List<GameObject>();
    private string _saveDirectory;
    private CityTileSetData _tileSet;

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-marks-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_saveDirectory, "run_save.json"));
        MetaProgressionManager.ResetRun();
        MetaProgressionManager.SetArchetype(CardArchetype.Sustentabilidade);
        _tileSet = TileSet();
    }

    [TearDown]
    public void TearDown()
    {
        DestroyScene();
        foreach (var asset in _assets)
            if (asset != null)
                Object.DestroyImmediate(asset);
        _assets.Clear();
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_saveDirectory))
            Directory.Delete(_saveDirectory, true);
    }

    private void DestroyScene()
    {
        foreach (var host in _scene)
            if (host != null)
                Object.DestroyImmediate(host);
        _scene.Clear();
    }

    private GameObject Template(string name)
    {
        var template = new GameObject(name);
        template.SetActive(false);
        _assets.Add(template);
        return template;
    }

    private StructureData Structure(string name)
    {
        var structure = ScriptableObject.CreateInstance<StructureData>();
        structure.name = name;
        TestDataFactory.SetField(structure, "_prefab", Template(name));
        TestDataFactory.SetField(structure, "_footprintCellType", CellType.Structure);
        _assets.Add(structure);
        return structure;
    }

    private CityTileSetData TileSet()
    {
        var set = ScriptableObject.CreateInstance<CityTileSetData>();
        _assets.Add(set);
        var roads = new List<CityRoadPiece>();
        foreach (var shape in new[] { RoadShape.Straight, RoadShape.Corner, RoadShape.TJunction, RoadShape.Cross })
            roads.Add(new CityRoadPiece { Shape = shape, Prefab = Template("Road" + shape) });
        var lots = new List<CityLotOptions>();
        foreach (CityLotUse use in System.Enum.GetValues(typeof(CityLotUse)))
            lots.Add(new CityLotOptions { Use = use, Structures = new[] { Structure("Old" + use) } });
        TestDataFactory.SetField(set, "_roads", roads.ToArray());
        TestDataFactory.SetField(set, "_lots", lots.ToArray());
        return set;
    }

    private CardMarkLibraryData Library(CardData card)
    {
        var library = ScriptableObject.CreateInstance<CardMarkLibraryData>();
        _assets.Add(library);
        var entry = new CardMarkEntry { Card = card, Kind = CardMarkKind.ReplaceLot, Size = 4, Pieces = new[] { Template("NewLot") } };
        TestDataFactory.SetField(library, "_entries", new[] { entry });
        return library;
    }

    private (TurnManager Turns, PlacementManager Placement, CardMarkController Marks) Phase(CardData card, CardMarkLibraryData library)
    {
        var city = new GameObject("PlacementManager");
        city.SetActive(false);
        _scene.Add(city);
        var placement = city.AddComponent<PlacementManager>();
        TestDataFactory.SetField(placement, "_width", 15);
        TestDataFactory.SetField(placement, "_height", 15);
        TestDataFactory.SetField(placement, "_tileSet", _tileSet);
        city.SetActive(true);

        var host = new GameObject("Phase");
        host.SetActive(false);
        _scene.Add(host);
        var statsManager = host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");
        var effects = host.AddComponent<CityEffectsController>();
        var turnManager = host.AddComponent<TurnManager>();
        TestDataFactory.SetField(turnManager, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(turnManager, "_placementManager", placement);
        TestDataFactory.SetField(turnManager, "_cardPool", new[] { card });
        TestDataFactory.SetField(turnManager, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(turnManager, "_advantagePool", new PassiveAdvantageData[0]);
        TestDataFactory.SetField(turnManager, "_effects", effects);
        TestDataFactory.SetField(turnManager, "_handSize", 1);
        var marks = host.AddComponent<CardMarkController>();
        TestDataFactory.SetField(marks, "_turnManager", turnManager);
        TestDataFactory.SetField(marks, "_placementManager", placement);
        TestDataFactory.SetField(marks, "_library", library);
        host.SetActive(true);
        return (turnManager, placement, marks);
    }

    private static IEnumerator PlayTurn(TurnManager turnManager, CardData card)
    {
        var turn = turnManager.Machine.TurnIndex;
        Assert.That(turnManager.PlayCard(card), Is.True);
        var deadline = Time.realtimeSinceStartup + 8f;
        while (turnManager.Machine.TurnIndex == turn && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(turn + 1));
    }

    private static IEnumerator WaitIdle(CardMarkController marks)
    {
        var deadline = Time.realtimeSinceStartup + 8f;
        while (marks.IsAnimating && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(marks.IsAnimating, Is.False);
    }

    private static string NameAt(PlacementManager placement, CardMarkSpot spot)
    {
        var lot = placement.LotObjectAt(new Vector3Int(spot.X, 0, spot.Z));
        return lot == null || lot.transform.childCount == 0 ? string.Empty : lot.transform.GetChild(0).name;
    }

    [UnityTest]
    public IEnumerator PlayingACard_ReplacesFourLots_AfterTheDrop()
    {
        var card = TestDataFactory.CreateCard("CardMarkTest");
        _assets.Add(card);
        var phase = Phase(card, Library(card));
        yield return null;
        yield return null;
        Assert.That(phase.Marks.Marks.Count, Is.EqualTo(0));

        yield return PlayTurn(phase.Turns, card);
        yield return WaitIdle(phase.Marks);

        Assert.That(phase.Marks.Marks.Count, Is.EqualTo(1));
        var mark = phase.Marks.Marks[0];
        Assert.That(mark.Spots.Length, Is.EqualTo(4));
        foreach (var spot in mark.Spots)
        {
            Assert.That(NameAt(phase.Placement, spot), Does.StartWith("NewLot"));
            var lot = phase.Placement.LotObjectAt(new Vector3Int(spot.X, 0, spot.Z));
            Assert.That(lot.transform.localScale, Is.EqualTo(Vector3.one), "terminou de pousar");
            Assert.That(lot.transform.localPosition, Is.EqualTo(phase.Placement.CellToLocalPosition(new Vector3Int(spot.X, 0, spot.Z))));
        }
    }

    [UnityTest]
    public IEnumerator FreeActionAndCardsWithoutEntry_LeaveNoMark()
    {
        var marked = TestDataFactory.CreateCard("CardMarkTest");
        var reveal = TestDataFactory.CreateCard("CardReveal", ability: CardAbility.RevealHand);
        _assets.Add(marked);
        _assets.Add(reveal);
        var library = Library(marked);
        var entries = new[] { library.Entries[0], new CardMarkEntry { Card = reveal, Kind = CardMarkKind.ReplaceLot, Size = 4, Pieces = new[] { Template("Nope") } } };
        TestDataFactory.SetField(library, "_entries", entries);
        var plain = TestDataFactory.CreateCard("CardPlain");
        _assets.Add(plain);
        var phase = Phase(plain, library);
        yield return null;
        yield return null;

        // Acao livre nao gasta a jogada nem marca, mesmo com entrada.
        var handle = typeof(CardMarkController).GetMethod("HandleCardPlayed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        handle.Invoke(phase.Marks, new object[] { reveal });
        yield return PlayTurn(phase.Turns, plain);
        yield return WaitIdle(phase.Marks);

        Assert.That(phase.Marks.Marks.Count, Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator ContinuingThePhase_RebuildsTheSameMarks_WithoutAnimation()
    {
        var card = TestDataFactory.CreateCard("CardMarkTest");
        _assets.Add(card);
        var library = Library(card);
        var phase = Phase(card, library);
        yield return null;
        yield return null;
        yield return PlayTurn(phase.Turns, card);
        yield return WaitIdle(phase.Marks);
        var seed = phase.Placement.LayoutSeed;
        var spots = phase.Marks.Marks[0].Spots;
        Assert.That(MetaProgressionManager.SavedPhase, Is.Not.Null, "o comeco do turno 2 salvou a fase");

        DestroyScene();
        var resumed = Phase(card, library);
        yield return null;
        yield return null;

        Assert.That(resumed.Placement.LayoutSeed, Is.EqualTo(seed));
        Assert.That(resumed.Marks.IsAnimating, Is.False);
        Assert.That(resumed.Marks.Marks.Count, Is.EqualTo(1));
        var again = resumed.Marks.Marks[0].Spots;
        Assert.That(again.Length, Is.EqualTo(spots.Length));
        for (var i = 0; i < spots.Length; i++)
        {
            Assert.That((again[i].X, again[i].Z), Is.EqualTo((spots[i].X, spots[i].Z)));
            Assert.That(NameAt(resumed.Placement, again[i]), Does.StartWith("NewLot"));
        }
    }
}
