using System;

namespace Metis.Core.Tests;

public class GridTests
{
    [Test]
    public void Constructor_ExposesWidthAndHeight()
    {
        var grid = new Grid(15, 9);

        Assert.That(grid.Width, Is.EqualTo(15));
        Assert.That(grid.Height, Is.EqualTo(9));
    }

    [Test]
    public void Indexer_DefaultsToEmpty()
    {
        var grid = new Grid(3, 3);

        Assert.That(grid[1, 1], Is.EqualTo(CellType.Empty));
    }

    [Test]
    public void Indexer_RoundTripsSetValuePerCell()
    {
        var grid = new Grid(3, 3);

        grid[0, 0] = CellType.Road;
        grid[1, 1] = CellType.Structure;
        grid[2, 2] = CellType.SpecialStructure;

        Assert.That(grid[0, 0], Is.EqualTo(CellType.Road));
        Assert.That(grid[1, 1], Is.EqualTo(CellType.Structure));
        Assert.That(grid[2, 2], Is.EqualTo(CellType.SpecialStructure));
        // Celula nao tocada continua Empty, escrever uma celula nao vaza pra vizinha.
        Assert.That(grid[0, 1], Is.EqualTo(CellType.Empty));
    }

    [Test]
    public void IsInBounds_InsideGrid_ReturnsTrue()
    {
        var grid = new Grid(3, 3);

        Assert.That(grid.IsInBounds(0, 0), Is.True);
        Assert.That(grid.IsInBounds(2, 2), Is.True);
    }

    [Test]
    public void IsInBounds_NegativeCoordinate_ReturnsFalse()
    {
        var grid = new Grid(3, 3);

        Assert.That(grid.IsInBounds(-1, 0), Is.False);
        Assert.That(grid.IsInBounds(0, -1), Is.False);
    }

    [Test]
    public void IsInBounds_AtOrPastWidthOrHeight_ReturnsFalse()
    {
        var grid = new Grid(3, 3);

        Assert.That(grid.IsInBounds(3, 0), Is.False);
        Assert.That(grid.IsInBounds(0, 3), Is.False);
    }

    [Test]
    public void TryGetRandomFreePosition_NoFreeCells_ReturnsFalse()
    {
        var grid = new Grid(2, 2);
        for (var x = 0; x < 2; x++)
            for (var z = 0; z < 2; z++)
                grid[x, z] = CellType.Structure;

        var found = grid.TryGetRandomFreePosition(new Random(0), out var x2, out var z2);

        Assert.That(found, Is.False);
        Assert.That((x2, z2), Is.EqualTo((0, 0)));
    }

    [Test]
    public void TryGetRandomFreePosition_SingleFreeCell_ReturnsThatCell()
    {
        var grid = new Grid(2, 2);
        for (var i = 0; i < 2; i++)
            for (var j = 0; j < 2; j++)
                grid[i, j] = CellType.Structure;
        grid[1, 0] = CellType.Empty;

        var found = grid.TryGetRandomFreePosition(new Random(0), out var x, out var z);

        Assert.That(found, Is.True);
        Assert.That((x, z), Is.EqualTo((1, 0)));
    }

    [Test]
    public void TryGetRandomFreePosition_MultipleFreeCells_NeverReturnsAnOccupiedCell()
    {
        var grid = new Grid(3, 3);
        grid[1, 1] = CellType.Structure;

        for (var seed = 0; seed < 50; seed++)
        {
            var found = grid.TryGetRandomFreePosition(new Random(seed), out var x, out var z);

            Assert.That(found, Is.True);
            Assert.That((x, z), Is.Not.EqualTo((1, 1)));
        }
    }
}
