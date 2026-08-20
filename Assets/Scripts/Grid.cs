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

}
