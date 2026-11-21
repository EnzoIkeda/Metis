using System;
using System.Collections.Generic;
using UnityEngine;

// Peca de rua de uma forma, com o giro que a deixa na posicao de referencia do gerador.
[Serializable]
public struct CityRoadPiece
{
    public RoadShape Shape;
    public GameObject Prefab;

    [Tooltip("Graus somados no eixo Y para o prefab ficar como a peca na posicao 0 do gerador (reta norte-sul, curva norte-leste, T sem o oeste).")]
    public float RotationOffset;
}

[Serializable]
public struct CityLotOptions
{
    public CityLotUse Use;
    public StructureData[] Structures;
}

// Conjunto de pecas da cidade gerada: rua por forma, estruturas por uso de lote e decoracao.
[CreateAssetMenu(fileName = "CityTileSet", menuName = "Metis/City Tile Set")]
public class CityTileSetData : ScriptableObject
{
    [SerializeField] private CityRoadPiece[] _roads;
    [SerializeField] private CityLotOptions[] _lots;
    [SerializeField] private GameObject _streetLight;
    [SerializeField] private GameObject _trafficSignal;

    public GameObject StreetLight => _streetLight;
    public GameObject TrafficSignal => _trafficSignal;
    public IReadOnlyList<CityRoadPiece> Roads => _roads ?? Array.Empty<CityRoadPiece>();
    public IReadOnlyList<CityLotOptions> Lots => _lots ?? Array.Empty<CityLotOptions>();

    public bool TryGetRoad(RoadShape shape, out CityRoadPiece piece)
    {
        foreach (var road in Roads)
        {
            if (road.Shape == shape && road.Prefab != null)
            {
                piece = road;
                return true;
            }
        }
        piece = default;
        return false;
    }

    public IReadOnlyList<StructureData> StructuresFor(CityLotUse use)
    {
        foreach (var lot in Lots)
            if (lot.Use == use && lot.Structures != null)
                return lot.Structures;
        return Array.Empty<StructureData>();
    }
}
