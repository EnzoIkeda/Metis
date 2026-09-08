using UnityEngine;

// Spawna e supervisiona os pedestres que andam pelas ruas da cidade.
public class PedestrianTrafficController : MonoBehaviour
{
    [SerializeField] private PlacementManager _placementManager;
    [SerializeField] private GameObject[] _pedestrianPrefabs;
    [SerializeField] private int _pedestrianCount = 12;
    [SerializeField] private float _minSpeed = 0.5f;
    [SerializeField] private float _maxSpeed = 0.9f;

    [Tooltip("Distância do centro da célula até a beira da calçada — precisa ficar claramente fora de CarTrafficController._laneOffset (mais a metade da largura de um carro) pra nunca se sobrepor com uma faixa de carro.")]
    [SerializeField] private float _edgeOffset = 0.4f;

    [Tooltip("Chance (0-1) de trocar de lado da rua toda vez que um pedestre SAI de uma célula de interseção (4 vizinhos) — a \"faixa de pedestre\" do design, já que interseção é o único lugar onde ruas se cruzam.")]
    [Range(0f, 1f)]
    [SerializeField] private float _crossingChance = 0.35f;

    [Tooltip("Graus somados por cima da rotação calculada na direção de movimento — mesmo propósito do campo equivalente em CarTrafficController, mas pros meshes de pessoa do pacote CityPeople.")]
    [SerializeField] private float _forwardAxisCorrectionDegrees;

    private RoadNetwork _roadNetwork;

    private void Start()
    {
        if (_placementManager == null || _placementManager.Grid == null)
        {
            Debug.LogWarning("[PedestrianTrafficController] Sem PlacementManager/Grid configurado — nenhum pedestre spawnado.");
            return;
        }
        if (_pedestrianPrefabs == null || _pedestrianPrefabs.Length == 0)
        {
            Debug.LogWarning("[PedestrianTrafficController] Nenhum prefab de pedestre configurado — nenhum pedestre spawnado.");
            return;
        }

        _roadNetwork = new RoadNetwork(_placementManager.Grid);
        if (_roadNetwork.RoadCells.Count == 0)
        {
            Debug.LogWarning("[PedestrianTrafficController] Grid sem nenhuma célula CellType.Road — nenhum pedestre spawnado.");
            return;
        }

        var pedestriansContainer = new GameObject("Pedestrians").transform;
        pedestriansContainer.SetParent(_placementManager.transform, false);

        for (var i = 0; i < _pedestrianCount; i++)
            SpawnPedestrian(pedestriansContainer);
    }

    private void SpawnPedestrian(Transform parent)
    {
        var prefab = _pedestrianPrefabs[Random.Range(0, _pedestrianPrefabs.Length)];
        var startCell = _roadNetwork.RoadCells[Random.Range(0, _roadNetwork.RoadCells.Count)];
        var speed = Random.Range(_minSpeed, _maxSpeed);
        var seed = Random.Range(int.MinValue, int.MaxValue);

        var instance = Instantiate(prefab, parent);
        var driver = instance.AddComponent<PedestrianDriver>();
        driver.Initialize(_placementManager, _roadNetwork, startCell, speed, _edgeOffset, _crossingChance, _forwardAxisCorrectionDegrees, seed);
    }
}
