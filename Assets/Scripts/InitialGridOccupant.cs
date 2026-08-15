using UnityEngine;

/// <summary>
/// Marker component que identifica um GameObject como ja ocupando uma celula do grid ao carregar a cena 
/// Note que a posicao precisa ser um filho direto de PlacementManager com coordenadas inteiras de grid 
/// </summary>
public class InitialGridOccupant : MonoBehaviour
{
    [SerializeField] private CellType _cellType = CellType.Structure;

    [SerializeField] private StructureData _structureData;

    public CellType CellType => _cellType;
    public StructureData StructureData => _structureData;
}
