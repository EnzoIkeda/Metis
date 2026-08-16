using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridge entre o espaço de mundo e o Grid
/// </summary>
public class PlacementManager : MonoBehaviour
{
    public int width, height;

    [SerializeField] private float cellSize = 1f;

    Grid placementGrid;

    private readonly Dictionary<Vector3Int, StructureData> _placedStructureData = new Dictionary<Vector3Int, StructureData>();

    private void Awake()
    {
        placementGrid = new Grid(width, height);
        RegisterInitialOccupants();
    }

    private void RegisterInitialOccupants()
    {
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

            placementGrid[position.x, position.z] = occupant.CellType;
            if (occupant.StructureData != null)
                _placedStructureData[position] = occupant.StructureData;
        }
    }

    internal bool CheckIfPositionInBound(Vector3Int position)
    {
        if(position.x >= 0 && position.x < width && position.z >=0 && position.z < height)
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
        return placementGrid[position.x, position.z] == type;
    }

    internal void PlaceTemporaryStructure(Vector3Int position, GameObject structurePrefab, CellType type)
    {
        placementGrid[position.x, position.z] = type;
        CreateANewStructureModel(position, structurePrefab, type);
    }

    private void CreateANewStructureModel(Vector3Int position, GameObject structurePrefab, CellType type)
    {
        GameObject structure = new GameObject(type.ToString());
        structure.transform.SetParent(transform);
        structure.transform.localPosition = CellToLocalPosition(position);
        var structureModel = structure.AddComponent<StructureModel>();
        structureModel.CreateModel(structurePrefab);
    }

    private Vector3 CellToLocalPosition(Vector3Int cell)
    {
        return new Vector3(cell.x * cellSize, cell.y, cell.z * cellSize);
    }

    private Vector3Int LocalPositionToCell(Vector3 localPosition)
    {
        return new Vector3Int(Mathf.RoundToInt(localPosition.x / cellSize), 0, Mathf.RoundToInt(localPosition.z / cellSize));
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
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
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
        for (var x = 0; x <= width; x++)
            Gizmos.DrawLine(transform.position + new Vector3(x * cellSize, 0, 0), transform.position + new Vector3(x * cellSize, 0, height * cellSize));
        for (var z = 0; z <= height; z++)
            Gizmos.DrawLine(transform.position + new Vector3(0, 0, z * cellSize), transform.position + new Vector3(width * cellSize, 0, z * cellSize));
    }
}
