// Tipo de ocupacao de uma celula do grid.
public enum CellType
{
    Empty,
    Road,
    Structure,
    SpecialStructure,
    None
}

// Grid 2D que guarda o tipo de cada celula, acessivel por indexador.
public class Grid
{
    private CellType[,] _grid;
    private int _width;
    public int Width { get { return _width; } }
    private int _height;
    public int Height { get { return _height; } }

    public Grid(int width, int height)
    {
        _width = width;
        _height = height;
        _grid = new CellType[width, height];
    }

    // Indexador pra ler ou escrever o tipo de uma celula direto.
    public CellType this[int i, int j]
    {
        get
        {
            return _grid[i, j];
        }
        set
        {
            _grid[i, j] = value;
        }
    }

    // Verifica se a celula esta dentro dos limites do grid.
    public bool IsInBounds(int x, int z)
    {
        return x >= 0 && x < _width && z >= 0 && z < _height;
    }

    // Sorteia uma celula livre (CellType.Empty); false se nao houver nenhuma.
    public bool TryGetRandomFreePosition(System.Random random, out int x, out int z)
    {
        var freePositions = new System.Collections.Generic.List<(int X, int Z)>();
        for (var i = 0; i < _width; i++)
        {
            for (var j = 0; j < _height; j++)
            {
                if (_grid[i, j] == CellType.Empty)
                    freePositions.Add((i, j));
            }
        }

        if (freePositions.Count == 0)
        {
            x = 0;
            z = 0;
            return false;
        }

        var chosen = freePositions[random.Next(freePositions.Count)];
        x = chosen.X;
        z = chosen.Z;
        return true;
    }
}
