using UnityEngine;

// Marca um GameObject como ja ocupando uma celula do grid ao carregar a cena.
public class InitialGridOccupant : MonoBehaviour
{
    [SerializeField] private CellType _cellType = CellType.Structure;

    [SerializeField] private StructureData _structureData;

    public CellType CellType => _cellType;
    public StructureData StructureData => _structureData;
}
