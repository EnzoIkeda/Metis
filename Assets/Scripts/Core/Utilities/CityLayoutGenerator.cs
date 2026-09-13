using System;
using System.Collections.Generic;

// Estrutura sorteada pra uma celula de quarteirao, com a rotacao (graus, eixo Y) pra encarar a rua mais proxima.
public struct CityLayoutCell
{
    public int X;
    public int Z;
    public StructureData Structure;
    public float FacingDegrees;
}

// Gera o conteudo dos quarteiroes (celulas Structure/SpecialStructure) de uma fase nova.
// A malha de ruas em si nunca muda, so o que e sorteado em cima dela.
public class CityLayoutGenerator
{
    // Mesma proporcao de hoje: cerca de 1 em cada 9 celulas de quarteirao vira lote verde.
    private const int GreenLotChance = 9;

    private readonly IReadOnlyList<StructureData> _buildingOptions;
    private readonly IReadOnlyList<StructureData> _greenLotOptions;
    private readonly Random _random;

    public CityLayoutGenerator(IReadOnlyList<StructureData> buildingOptions, IReadOnlyList<StructureData> greenLotOptions, Random random = null)
    {
        _buildingOptions = buildingOptions;
        _greenLotOptions = greenLotOptions;
        _random = random ?? new Random();
    }

    public List<CityLayoutCell> Generate(Grid grid)
    {
        var cells = new List<CityLayoutCell>();
        for (int x = 0; x < grid.Width; x++)
        {
            for (int z = 0; z < grid.Height; z++)
            {
                var cellType = grid[x, z];
                if (cellType != CellType.Structure && cellType != CellType.SpecialStructure)
                    continue;

                var useGreenLot = _greenLotOptions.Count > 0 && _random.Next(GreenLotChance) == 0;
                var options = useGreenLot ? _greenLotOptions : _buildingOptions;
                if (options.Count == 0)
                    continue;

                cells.Add(new CityLayoutCell
                {
                    X = x,
                    Z = z,
                    Structure = options[_random.Next(options.Count)],
                    FacingDegrees = NearestStreetFacingDegrees(grid, x, z)
                });
            }
        }

        return cells;
    }

    // Vira a estrutura pro lado da rua Road mais proxima, em multiplos de 90 graus.
    private static float NearestStreetFacingDegrees(Grid grid, int x, int z)
    {
        var bestDistance = int.MaxValue;
        var facing = 0f;

        CheckDirection(grid, x, z, 0, -1, 180f, ref bestDistance, ref facing);
        CheckDirection(grid, x, z, 0, 1, 0f, ref bestDistance, ref facing);
        CheckDirection(grid, x, z, -1, 0, 270f, ref bestDistance, ref facing);
        CheckDirection(grid, x, z, 1, 0, 90f, ref bestDistance, ref facing);

        return facing;
    }

    private static void CheckDirection(Grid grid, int x, int z, int dx, int dz, float degrees, ref int bestDistance, ref float facing)
    {
        var limit = Math.Max(grid.Width, grid.Height);
        for (int distance = 1; distance < limit; distance++)
        {
            var nx = x + dx * distance;
            var nz = z + dz * distance;
            if (nx < 0 || nx >= grid.Width || nz < 0 || nz >= grid.Height)
                return;

            if (grid[nx, nz] == CellType.Road)
            {
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    facing = degrees;
                }
                return;
            }
        }
    }
}
