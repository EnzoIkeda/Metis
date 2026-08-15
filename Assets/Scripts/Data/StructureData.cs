using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Categoria de gameplay de uma estrutura
/// </summary>
public enum StructureCategory
{
    Road,
    Residential,
    Industrial,
    Park,
    SmartInfrastructure,
    SpecialStructure
}

/// <summary>
/// Definicao orientada a dados de uma estrutura colocavel como um ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "New Structure", menuName = "Metis/Structure Data")]
public class StructureData : ScriptableObject
{
    [SerializeField] private string _structureName;
    [SerializeField] private StructureCategory _category;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private CellType _footprintCellType = CellType.Structure;
    [SerializeField] private StatModifier[] _statEffects;

    public string StructureName => _structureName;
    public StructureCategory Category => _category;
    public GameObject Prefab => _prefab;

    /// <summary>CellType ocupado ao ser colocada </summary>
    public CellType FootprintCellType => _footprintCellType;

    public IReadOnlyList<StatModifier> StatEffects => _statEffects;
}
