/// <summary>Tipo de ocupacao de uma celula do grid</summary>
public enum CellType
{
    Empty,
    Road,
    Structure,
    SpecialStructure,
    None
}

/// <summary>
/// Grid 2D: guarda o CellType de cada célula com um indexador (grid[i, j]) 
/// para leitura/escrita direta. PlacementManager é o wrapper que traduz 
/// posicoes para este grid.
/// </summary>
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

    // Adding index operator to our Grid class so that we can use grid[][] to access specific cell from our grid.
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
