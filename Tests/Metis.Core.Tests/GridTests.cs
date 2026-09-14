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
}
