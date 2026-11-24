using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Bridge entre o espaco de mundo e o grid.
public class PlacementManager : MonoBehaviour
{
    [FormerlySerializedAs("width")] [SerializeField] private int _width;
    [FormerlySerializedAs("height")] [SerializeField] private int _height;
    [FormerlySerializedAs("cellSize")] [SerializeField] private float _cellSize = 1f;

    [Tooltip("Pecas da cidade gerada: rua por forma, estruturas por uso de lote e decoracao.")]
    [SerializeField] private CityTileSetData _tileSet;
    [SerializeField] private ThemedPhaseLibraryData _themes;

    private Grid _placementGrid;

    // Semente do tracado, salva pra reconstruir a mesma cidade ao continuar.
    public ulong LayoutSeed { get; private set; }

    // Cidade planejada desta fase (ruas, usos e frentes dos lotes).
    public CityPlan Plan { get; private set; }

    // Instancia pura do grid, exposta pra outros sistemas consultarem o layout.
    public Grid Grid => _placementGrid;

    private readonly Dictionary<Vector3Int, StructureData> _placedStructureData = new Dictionary<Vector3Int, StructureData>();

    // O que esta de pe em cada lote; um predio fundido aparece nos quatro lotes.
    private readonly Dictionary<Vector3Int, GameObject> _lotObjects = new Dictionary<Vector3Int, GameObject>();

    public float CellSize => _cellSize;

    // Quanto a peca fundida cresce na altura (na largura cresce o lado do quadrado).
    public const float MergedHeightFactor = 1.4f;

    private void Awake()
    {
        _placementGrid = new Grid(_width, _height);
        BuildCity();
    }

    // Toda fase sai do gerador: semente salva ao continuar, a do tema se houver, senao uma nova.
    private void BuildCity()
    {
        var savedPhase = MetaProgressionManager.SavedPhase;
        var theme = _themes != null ? _themes.Find(MetaProgressionManager.CurrentTheme) : null;
        LayoutSeed = savedPhase != null ? savedPhase.LayoutSeed : theme != null && theme.HasLayoutSeed ? theme.LayoutSeed : SeededRandom.NewSeed();
        var coast = theme != null ? theme.Coast : CoastSide.None;
        Plan = CityWfc.Generate(LayoutSeed, new CityWfcOptions { Width = _width, Height = _height, Coast = coast });

        if (_tileSet == null)
        {
            Debug.LogWarning("[PlacementManager] Sem conjunto de pecas da cidade; o grid fica so com os tipos de celula.");
            FillGridTypes();
            return;
        }

        // Gerador proprio para a escolha das estruturas, separado do tracado.
        var random = new SeededRandom(LayoutSeed ^ 0x5DEECE66DUL);
        for (var x = 0; x < _width; x++)
        {
            for (var z = 0; z < _height; z++)
            {
                var position = new Vector3Int(x, 0, z);
                if (Plan.IsRoad(x, z))
                {
                    PlaceRoad(position, Plan.RoadAt(x, z));
                    continue;
                }
                if (Plan.IsCoast(x, z))
                {
                    PlaceCoast(position, Plan.CoastAt(x, z));
                    continue;
                }

                var options = _tileSet.StructuresFor(Plan.LotUseAt(x, z));
                if (options.Count == 0)
                {
                    _placementGrid[x, z] = CellType.Structure;
                    continue;
                }
                var structure = options[random.Next(options.Count)];
                _lotObjects[position] = PlaceTemporaryStructure(position, structure.Prefab, structure.FootprintCellType, Plan.FacingAt(x, z));
                _placedStructureData[position] = structure;
            }
        }

        PlaceDecor();
        PlaceCoastDecor();
    }

    private void FillGridTypes()
    {
        for (var x = 0; x < _width; x++)
            for (var z = 0; z < _height; z++)
                _placementGrid[x, z] = Plan.IsRoad(x, z) ? CellType.Road : CellType.Structure;
    }

    private void PlaceRoad(Vector3Int position, RoadSides sides)
    {
        var shape = CityPlan.ShapeOf(sides, out var rotation);
        if (_tileSet.TryGetRoad(shape, out var piece) == false)
        {
            _placementGrid[position.x, position.z] = CellType.Road;
            return;
        }
        PlaceTemporaryStructure(position, piece.Prefab, CellType.Road, rotation + piece.RotationOffset);
    }

    // Mar e praia nao recebem estrutura nem carta: no grid ficam bloqueados como lote.
    private void PlaceCoast(Vector3Int position, CoastCell cell)
    {
        _placementGrid[position.x, position.z] = CellType.Structure;
        var prefab = cell == CoastCell.Water ? _tileSet.Water : _tileSet.Beach;
        if (prefab == null)
            return;
        var instance = Instantiate(prefab, CoastContainer());
        instance.transform.localPosition = CellToLocalPosition(position);
        if (cell == CoastCell.Water)
            CoastContainer().GetComponent<CoastAnimator>().AddWater(instance);
    }

    private void PlaceCoastDecor()
    {
        if (Plan.Coast == CoastSide.None)
            return;
        var container = CoastContainer();
        var animator = container.GetComponent<CoastAnimator>();
        // Gerador proprio para escolher as pecas, separado do lugar de cada uma.
        var random = new SeededRandom(LayoutSeed ^ 0xB0A7UL);
        foreach (var item in CoastDecor.Plan(Plan, LayoutSeed))
        {
            var options = _tileSet.CoastPieces(item.Kind);
            if (options.Count == 0)
                continue;
            var prefab = options[random.Next(options.Count)];
            if (prefab == null)
                continue;
            var instance = Instantiate(prefab, container);
            instance.transform.localPosition = new Vector3(item.X * _cellSize, 0f, item.Z * _cellSize);
            instance.transform.localRotation = Quaternion.Euler(0f, item.Rotation, 0f) * instance.transform.localRotation;
            if (item.Kind == CoastDecorKind.Boat || item.Kind == CoastDecorKind.Ship || item.Kind == CoastDecorKind.Buoy)
                animator.AddFloating(instance.transform, random.Next(1000) / 1000f);
        }
    }

    private Transform _coastContainer;

    private Transform CoastContainer()
    {
        if (_coastContainer != null)
            return _coastContainer;
        var container = new GameObject("Coast");
        container.transform.SetParent(transform, false);
        container.AddComponent<CoastAnimator>();
        _coastContainer = container.transform;
        return _coastContainer;
    }

    private void PlaceDecor()
    {
        var container = new GameObject("Decor").transform;
        container.SetParent(transform, false);
        foreach (var item in CityDecor.Plan(Plan))
        {
            var prefab = item.Kind == CityDecorKind.StreetLight ? _tileSet.StreetLight : _tileSet.TrafficSignal;
            if (prefab == null)
                continue;
            var instance = Instantiate(prefab, container);
            instance.transform.localPosition = CellToLocalPosition(new Vector3Int(item.X, 0, item.Z)) + new Vector3(item.OffsetX * _cellSize, 0f, item.OffsetZ * _cellSize);
            instance.transform.localRotation = Quaternion.Euler(0f, item.Rotation, 0f) * instance.transform.localRotation;
        }
    }

    internal bool CheckIfPositionInBound(Vector3Int position)
    {
        return _placementGrid.IsInBounds(position.x, position.z);
    }

    internal bool CheckIfPositionIsFree(Vector3Int position)
    {
        return CheckIfPositionIsOfType(position, CellType.Empty);
    }

    private bool CheckIfPositionIsOfType(Vector3Int position, CellType type)
    {
        return _placementGrid[position.x, position.z] == type;
    }

    internal GameObject PlaceTemporaryStructure(Vector3Int position, GameObject structurePrefab, CellType type, float yRotationDegrees = 0f)
    {
        _placementGrid[position.x, position.z] = type;
        return CreateANewStructureModel(CellToLocalPosition(position), structurePrefab, type, yRotationDegrees);
    }

    private GameObject CreateANewStructureModel(Vector3 localPosition, GameObject structurePrefab, CellType type, float yRotationDegrees = 0f)
    {
        GameObject structure = new GameObject(type.ToString());
        structure.transform.SetParent(transform);
        structure.transform.localPosition = localPosition;
        structure.transform.localRotation = Quaternion.Euler(0f, yRotationDegrees, 0f);
        var structureModel = structure.AddComponent<StructureModel>();
        structureModel.CreateModel(structurePrefab);
        return structure;
    }

    public GameObject LotObjectAt(Vector3Int cell)
    {
        _lotObjects.TryGetValue(cell, out var lot);
        return lot;
    }

    // Poe uma peca nova nos lotes dados e devolve as que estavam ali, sem destruir, para quem anima a saida.
    // Fundida, a peca fica no centro dos lotes em escala multiplicada pelo lado do quadrado.
    public GameObject ReplaceLots(IReadOnlyList<Vector3Int> cells, GameObject prefab, float yRotationDegrees, bool merged, List<GameObject> removed)
    {
        if (cells == null || cells.Count == 0 || prefab == null)
            return null;
        foreach (var cell in cells)
        {
            var old = LotObjectAt(cell);
            if (old != null && removed != null && removed.Contains(old) == false)
                removed.Add(old);
            _lotObjects.Remove(cell);
            _placedStructureData.Remove(cell);
        }

        var center = Vector3.zero;
        foreach (var cell in cells)
            center += CellToLocalPosition(cell);
        center /= cells.Count;
        var piece = CreateANewStructureModel(merged ? center : CellToLocalPosition(cells[0]), prefab, CellType.Structure, yRotationDegrees);
        if (merged)
        {
            // Mais largo do que alto: torre dobrada em tudo viraria uma caixa enorme.
            var side = Mathf.Sqrt(cells.Count);
            piece.transform.localScale = Vector3.Scale(piece.transform.localScale, new Vector3(side, MergedHeightFactor, side));
        }
        foreach (var cell in merged ? cells : new[] { cells[0] })
            _lotObjects[cell] = piece;
        return piece;
    }

    // Converte uma celula pra posicao local, reaproveitavel por qualquer GameObject filho do grid.
    public Vector3 CellToLocalPosition(Vector3Int cell)
    {
        return new Vector3(cell.x * _cellSize, cell.y, cell.z * _cellSize);
    }

    public bool PlaceStructure(Vector3Int position, StructureData structureData)
    {
        if (CheckIfPositionInBound(position) == false)
            return false;
        if (CheckIfPositionIsFree(position) == false)
            return false;

        PlaceTemporaryStructure(position, structureData.Prefab, structureData.FootprintCellType);
        _placedStructureData[position] = structureData;
        return true;
    }

    public StructureData GetStructureDataAt(Vector3Int position)
    {
        _placedStructureData.TryGetValue(position, out var structureData);
        return structureData;
    }

    public bool TryGetRandomFreePosition(out Vector3Int position)
    {
        var random = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        if (_placementGrid.TryGetRandomFreePosition(random, out var x, out var z) == false)
        {
            position = default;
            return false;
        }

        position = new Vector3Int(x, 0, z);
        return true;
    }

    // Debug visual
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        for (var x = 0; x <= _width; x++)
            Gizmos.DrawLine(transform.position + new Vector3(x * _cellSize, 0, 0), transform.position + new Vector3(x * _cellSize, 0, _height * _cellSize));
        for (var z = 0; z <= _height; z++)
            Gizmos.DrawLine(transform.position + new Vector3(0, 0, z * _cellSize), transform.position + new Vector3(_width * _cellSize, 0, z * _cellSize));
    }
}
