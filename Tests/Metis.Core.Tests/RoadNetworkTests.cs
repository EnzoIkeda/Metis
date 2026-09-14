using System;
using System.Collections.Generic;

namespace Metis.Core.Tests;

public class RoadNetworkTests
{
    // Grid 3x3 em formato de cruz: so a linha e a coluna do meio sao Road, o resto e Structure.
    //   S R S
    //   R R R
    //   S R S
    private static Grid BuildCrossGrid()
    {
        var grid = new Grid(3, 3);
        for (var x = 0; x < 3; x++)
            for (var z = 0; z < 3; z++)
                grid[x, z] = CellType.Structure;

        grid[1, 0] = CellType.Road;
        grid[0, 1] = CellType.Road;
        grid[1, 1] = CellType.Road;
        grid[2, 1] = CellType.Road;
        grid[1, 2] = CellType.Road;
        return grid;
    }

    [Test]
    public void Constructor_CollectsOnlyRoadCells()
    {
        var network = new RoadNetwork(BuildCrossGrid());

        Assert.That(network.RoadCells, Is.EquivalentTo(new (int, int)[] { (1, 0), (0, 1), (1, 1), (2, 1), (1, 2) }));
    }

    [Test]
    public void GetNeighbors_CenterCell_ReturnsAllFourRoadNeighbors()
    {
        var network = new RoadNetwork(BuildCrossGrid());

        var neighbors = network.GetNeighbors((1, 1));

        Assert.That(neighbors, Is.EquivalentTo(new (int, int)[] { (2, 1), (0, 1), (1, 2), (1, 0) }));
    }

    [Test]
    public void GetNeighbors_ArmTip_ExcludesDiagonalAndNonRoadAndOutOfBounds()
    {
        var network = new RoadNetwork(BuildCrossGrid());

        var neighbors = network.GetNeighbors((1, 0));

        Assert.That(neighbors, Is.EquivalentTo(new (int, int)[] { (1, 1) }));
    }

    [Test]
    public void GetNeighbors_NonRoadCell_ReturnsEmpty()
    {
        var network = new RoadNetwork(BuildCrossGrid());

        Assert.That(network.GetNeighbors((0, 0)), Is.Empty);
    }

    [Test]
    public void PickNextCell_WithMultipleCandidates_NeverImmediatelyBacktracks()
    {
        var network = new RoadNetwork(BuildCrossGrid());

        for (var seed = 0; seed < 50; seed++)
        {
            var next = network.PickNextCell((1, 1), (1, 0), new Random(seed));
            Assert.That(next, Is.Not.EqualTo((1, 0)));
        }
    }

    [Test]
    public void PickNextCell_AtDeadEnd_AllowsBacktrackingToOnlyNeighbor()
    {
        var network = new RoadNetwork(BuildCrossGrid());

        // A ponta do braco (1,0) so enxerga (1,1) como vizinho: unica opcao e voltar por onde veio.
        var next = network.PickNextCell((1, 0), (1, 1), new Random(0));

        Assert.That(next, Is.EqualTo((1, 1)));
    }

    [Test]
    public void PickNextCell_IsolatedCellWithNoNeighbors_StaysInPlace()
    {
        var grid = new Grid(3, 3);
        grid[1, 1] = CellType.Road; // unica celula de rua, sem vizinhos

        var network = new RoadNetwork(grid);

        var next = network.PickNextCell((1, 1), null, new Random(0));

        Assert.That(next, Is.EqualTo((1, 1)));
    }
}
