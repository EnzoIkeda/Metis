using System.Collections.Generic;
using UnityEngine;

// Categoria de gameplay de uma estrutura.
public enum StructureCategory
{
    Road,
    Residential,
    Industrial,
    Park,
    SmartInfrastructure,
    SpecialStructure
}

// Definicao orientada a dados de uma estrutura colocavel.
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

    // Tipo de celula ocupado ao ser colocada.
    public CellType FootprintCellType => _footprintCellType;

    public IReadOnlyList<StatModifier> StatEffects => _statEffects;
}
