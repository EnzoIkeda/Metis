using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Bridge entre o espaco de mundo e o grid.
public class PlacementManager : MonoBehaviour
{
    [FormerlySerializedAs("width")] [SerializeField] private int _width;
    [FormerlySerializedAs("height")] [SerializeField] private int _height;
    [FormerlySerializedAs("cellSize")] [SerializeField] private float _cellSize = 1f;

    // Usados so nas fases 2+ de uma rodada, pra sortear o conteudo dos quarteiroes por cima da malha de ruas fixa.
    [SerializeField] private StructureData[] _proceduralBuildingOptions;
    [SerializeField] private StructureData[] _proceduralGreenLotOptions;

    private Grid _placementGrid;

    // Instancia pura do grid, exposta pra outros sistemas consultarem o layout.
    public Grid Grid => _placementGrid;

    private readonly Dictionary<Vector3Int, StructureData> _placedStructureData = new Dictionary<Vector3Int, StructureData>();

    private void Awake()
    {
        _placementGrid = new Grid(_width, _height);
        RegisterInitialOccupants();
    }

    private void RegisterInitialOccupants()
    {
        var isNewPhase = MetaProgressionManager.IsNewPhase;

        foreach (Transform child in transform)
        {
            var occupant = child.GetComponent<InitialGridOccupant>();
            if (occupant == null)
                continue;

            var position = LocalPositionToCell(child.localPosition);
            if (CheckIfPositionInBound(position) == false)
            {
                Debug.LogWarning($"[PlacementManager] '{child.name}' tem InitialGridOccupant fora dos limites do grid ({position}), ignorado.");
                continue;
            }

            _placementGrid[position.x, position.z] = occupant.CellType;

            var isBlockCell = occupant.CellType == CellType.Structure || occupant.CellType == CellType.SpecialStructure;
            if (isBlockCell && isNewPhase)
            {
                // Fase nova: o quarteirao desenhado a mao vira o sorteado em RegenerateBlocks, entao some daqui.
                child.gameObject.SetActive(false);
                continue;
            }

            if (occupant.StructureData != null)
                _placedStructureData[position] = occupant.StructureData;
        }

        if (isNewPhase)
            RegenerateBlocks();
    }

    // Sorteia de novo o conteudo dos quarteiroes (nunca a malha de ruas) pras fases 2+ de uma rodada.
    private void RegenerateBlocks()
    {
        var generator = new CityLayoutGenerator(_proceduralBuildingOptions, _proceduralGreenLotOptions);
        foreach (var cell in generator.Generate(_placementGrid))
        {
            var position = new Vector3Int(cell.X, 0, cell.Z);
            PlaceTemporaryStructure(position, cell.Structure.Prefab, cell.Structure.FootprintCellType, cell.FacingDegrees);
            _placedStructureData[position] = cell.Structure;
        }
    }

    internal bool CheckIfPositionInBound(Vector3Int position)
    {
        if(position.x >= 0 && position.x < _width && position.z >=0 && position.z < _height)
        {
            return true;
        }
        return false;
    }

    internal bool CheckIfPositionIsFree(Vector3Int position)
    {
        return CheckIfPositionIsOfType(position, CellType.Empty);
    }

    private bool CheckIfPositionIsOfType(Vector3Int position, CellType type)
    {
        return _placementGrid[position.x, position.z] == type;
    }

    internal void PlaceTemporaryStructure(Vector3Int position, GameObject structurePrefab, CellType type, float yRotationDegrees = 0f)
    {
        _placementGrid[position.x, position.z] = type;
        CreateANewStructureModel(position, structurePrefab, type, yRotationDegrees);
    }

    private void CreateANewStructureModel(Vector3Int position, GameObject structurePrefab, CellType type, float yRotationDegrees = 0f)
    {
        GameObject structure = new GameObject(type.ToString());
        structure.transform.SetParent(transform);
        structure.transform.localPosition = CellToLocalPosition(position);
        structure.transform.localRotation = Quaternion.Euler(0f, yRotationDegrees, 0f);
        var structureModel = structure.AddComponent<StructureModel>();
        structureModel.CreateModel(structurePrefab);
    }

    // Converte uma celula pra posicao local, reaproveitavel por qualquer GameObject filho do grid.
    public Vector3 CellToLocalPosition(Vector3Int cell)
    {
        return new Vector3(cell.x * _cellSize, cell.y, cell.z * _cellSize);
    }

    private Vector3Int LocalPositionToCell(Vector3 localPosition)
    {
        return new Vector3Int(Mathf.RoundToInt(localPosition.x / _cellSize), 0, Mathf.RoundToInt(localPosition.z / _cellSize));
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

    internal StructureData GetStructureDataAt(Vector3Int position)
    {
        _placedStructureData.TryGetValue(position, out var structureData);
        return structureData;
    }

    public bool TryGetRandomFreePosition(out Vector3Int position)
    {
        var freePositions = new List<Vector3Int>();
        for (var x = 0; x < _width; x++)
        {
            for (var z = 0; z < _height; z++)
            {
                var candidate = new Vector3Int(x, 0, z);
                if (CheckIfPositionIsFree(candidate))
                    freePositions.Add(candidate);
            }
        }

        if (freePositions.Count == 0)
        {
            position = default;
            return false;
        }

        position = freePositions[UnityEngine.Random.Range(0, freePositions.Count)];
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
