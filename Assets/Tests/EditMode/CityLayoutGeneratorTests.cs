using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class CityLayoutGeneratorTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            UnityEngine.Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private StructureData Structure(string name)
    {
        var structure = TestDataFactory.CreateStructure(name);
        _created.Add(structure);
        return structure;
    }

    // Retorna sempre os valores dados, na ordem, ignorando maxValue (os testes ja escolhem
    // valores validos pra cada chamada) - pra controlar exatamente qual ramo Generate() segue.
    private class SequenceRandom : System.Random
    {
        private readonly Queue<int> _values;
        public SequenceRandom(params int[] values) { _values = new Queue<int>(values); }
        public override int Next(int maxValue) => _values.Count > 0 ? _values.Dequeue() : 0;
    }

    [Test]
    public void Generate_OnlyProducesCellsForStructureAndSpecialStructure()
    {
        var grid = new Grid(4, 1);
        grid[0, 0] = CellType.Road;
        grid[1, 0] = CellType.Empty;
        grid[2, 0] = CellType.Structure;
        grid[3, 0] = CellType.SpecialStructure;
        var building = Structure("Building");
        var generator = new CityLayoutGenerator(new[] { building }, Array.Empty<StructureData>());

        var cells = generator.Generate(grid);

        Assert.That(cells.Select(c => (c.X, c.Z)), Is.EquivalentTo(new[] { (2, 0), (3, 0) }));
        Assert.That(cells, Has.All.Matches<CityLayoutCell>(c => c.Structure == building));
    }

    [Test]
    public void Generate_EmptyBuildingOptions_SkipsCellsWithoutThrowing()
    {
        var grid = new Grid(1, 1);
        grid[0, 0] = CellType.Structure;
        var generator = new CityLayoutGenerator(Array.Empty<StructureData>(), Array.Empty<StructureData>());

        var cells = generator.Generate(grid);

        Assert.That(cells, Is.Empty);
    }

    [Test]
    public void Generate_UsesGreenLotOnlyWhenRollHitsAndOptionsExist()
    {
        var grid = new Grid(2, 1);
        grid[0, 0] = CellType.Structure;
        grid[1, 0] = CellType.Structure;
        var building = Structure("Building");
        var greenLot = Structure("GreenLot");
        // celula (0,0): rolagem de lote verde acerta (0) -> escolhe lote verde (indice 0).
        // celula (1,0): rolagem erra (5) -> cai pro predio normal (indice 0).
        var random = new SequenceRandom(0, 0, 5, 0);
        var generator = new CityLayoutGenerator(new[] { building }, new[] { greenLot }, random);

        var cells = generator.Generate(grid);

        Assert.That(cells.First(c => c.X == 0).Structure, Is.EqualTo(greenLot));
        Assert.That(cells.First(c => c.X == 1).Structure, Is.EqualTo(building));
    }

    private static CityLayoutCell GenerateSingleCell(Grid grid)
    {
        var generator = new CityLayoutGenerator(new[] { (StructureData)null }, Array.Empty<StructureData>());
        return generator.Generate(grid).Single();
    }

    [Test]
    public void Generate_FacesStreetToTheSouth_WhenOnlyRoadThere()
    {
        var grid = new Grid(3, 3);
        grid[1, 1] = CellType.Structure;
        grid[1, 0] = CellType.Road; // z-1 relativo ao centro

        Assert.That(GenerateSingleCell(grid).FacingDegrees, Is.EqualTo(180f));
    }

    [Test]
    public void Generate_FacesStreetToTheNorth_WhenOnlyRoadThere()
    {
        var grid = new Grid(3, 3);
        grid[1, 1] = CellType.Structure;
        grid[1, 2] = CellType.Road; // z+1 relativo ao centro

        Assert.That(GenerateSingleCell(grid).FacingDegrees, Is.EqualTo(0f));
    }

    [Test]
    public void Generate_FacesStreetToTheWest_WhenOnlyRoadThere()
    {
        var grid = new Grid(3, 3);
        grid[1, 1] = CellType.Structure;
        grid[0, 1] = CellType.Road; // x-1 relativo ao centro

        Assert.That(GenerateSingleCell(grid).FacingDegrees, Is.EqualTo(270f));
    }

    [Test]
    public void Generate_FacesStreetToTheEast_WhenOnlyRoadThere()
    {
        var grid = new Grid(3, 3);
        grid[1, 1] = CellType.Structure;
        grid[2, 1] = CellType.Road; // x+1 relativo ao centro

        Assert.That(GenerateSingleCell(grid).FacingDegrees, Is.EqualTo(90f));
    }

    [Test]
    public void Generate_TiedDistance_PrefersSouthOverWest_MatchingCheckOrder()
    {
        var grid = new Grid(3, 3);
        grid[1, 1] = CellType.Structure;
        grid[1, 0] = CellType.Road; // z-1, checado primeiro
        grid[0, 1] = CellType.Road; // x-1, checado depois, mesma distancia

        Assert.That(GenerateSingleCell(grid).FacingDegrees, Is.EqualTo(180f));
    }

    [Test]
    public void Generate_PrefersNearestRoad_OverFartherOneInAnotherDirection()
    {
        var grid = new Grid(5, 5);
        grid[2, 2] = CellType.Structure;
        grid[2, 0] = CellType.Road; // z-1, distancia 2
        grid[3, 2] = CellType.Road; // x+1, distancia 1 (mais perto)

        Assert.That(GenerateSingleCell(grid).FacingDegrees, Is.EqualTo(90f));
    }

    [Test]
    public void Generate_NoRoadAnywhere_FacingStaysAtDefault()
    {
        var grid = new Grid(3, 3);
        grid[1, 1] = CellType.Structure;

        Assert.That(GenerateSingleCell(grid).FacingDegrees, Is.EqualTo(0f));
    }
}
