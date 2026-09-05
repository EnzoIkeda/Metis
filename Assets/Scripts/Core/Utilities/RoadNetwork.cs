using System;
using System.Collections.Generic;

// Grafo de adjacencia das celulas de rua de um grid, guiando quem anda pelas ruas.
public class RoadNetwork
{
    private readonly List<(int X, int Z)> _roadCells = new List<(int X, int Z)>();
    private readonly Dictionary<(int X, int Z), List<(int X, int Z)>> _neighbors = new Dictionary<(int X, int Z), List<(int X, int Z)>>();

    public IReadOnlyList<(int X, int Z)> RoadCells => _roadCells;

    public RoadNetwork(Grid grid)
    {
        for (var x = 0; x < grid.Width; x++)
        {
            for (var z = 0; z < grid.Height; z++)
            {
                if (grid[x, z] == CellType.Road)
                    _roadCells.Add((x, z));
            }
        }

        foreach (var cell in _roadCells)
        {
            var neighbors = new List<(int X, int Z)>();
            TryAddNeighbor(grid, (cell.X + 1, cell.Z), neighbors);
            TryAddNeighbor(grid, (cell.X - 1, cell.Z), neighbors);
            TryAddNeighbor(grid, (cell.X, cell.Z + 1), neighbors);
            TryAddNeighbor(grid, (cell.X, cell.Z - 1), neighbors);
            _neighbors[cell] = neighbors;
        }
    }

    private static void TryAddNeighbor(Grid grid, (int X, int Z) candidate, List<(int X, int Z)> into)
    {
        if (candidate.X < 0 || candidate.X >= grid.Width || candidate.Z < 0 || candidate.Z >= grid.Height)
            return;
        if (grid[candidate.X, candidate.Z] != CellType.Road)
            return;

        into.Add(candidate);
    }

    public IReadOnlyList<(int X, int Z)> GetNeighbors((int X, int Z) cell)
    {
        return _neighbors.TryGetValue(cell, out var neighbors) ? neighbors : Array.Empty<(int X, int Z)>();
    }

    // Escolhe a proxima celula do passeio, evitando meia-volta a menos que seja um beco sem saida.
    public (int X, int Z) PickNextCell((int X, int Z) current, (int X, int Z)? previous, Random random)
    {
        var neighbors = GetNeighbors(current);
        if (neighbors.Count == 0)
            return current;

        var candidates = new List<(int X, int Z)>(neighbors.Count);
        foreach (var neighbor in neighbors)
        {
            if (previous.HasValue && neighbor.Equals(previous.Value) && neighbors.Count > 1)
                continue;
            candidates.Add(neighbor);
        }
        if (candidates.Count == 0)
            candidates.AddRange(neighbors);

        return candidates[random.Next(candidates.Count)];
    }
}
