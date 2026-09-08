using UnityEngine;

// Spawna e supervisiona os carros que andam pelas ruas da cidade.
public class CarTrafficController : MonoBehaviour
{
    [SerializeField] private PlacementManager _placementManager;
    [SerializeField] private GameObject[] _carPrefabs;
    [SerializeField] private int _carCount = 6;
    [SerializeField] private float _minSpeed = 1.2f;
    [SerializeField] private float _maxSpeed = 2.2f;

    [Tooltip("Distância do centro da célula (cellSize=1) até o centro de cada faixa — 2 faixas por rua, uma em cada sentido, a essa distância pra cada lado. Precisa deixar espaço até a borda da célula pra PedestrianDriver._edgeOffset (calçada) nunca se sobrepor com a faixa de carro.")]
    [SerializeField] private float _laneOffset = 0.22f;

    [Tooltip("Graus somados por cima da rotação calculada (Quaternion.LookRotation na direção de movimento) — compensa o eixo \"de frente\" de verdade do mesh do carro, igual à FrontAxisCorrection dos prédios da cidade inicial (ver ARCHITECTURE.md).")]
    [SerializeField] private float _forwardAxisCorrectionDegrees;

    private RoadNetwork _roadNetwork;

    private void Start()
    {
        if (_placementManager == null || _placementManager.Grid == null)
        {
            Debug.LogWarning("[CarTrafficController] Sem PlacementManager/Grid configurado — nenhum carro spawnado.");
            return;
        }
        if (_carPrefabs == null || _carPrefabs.Length == 0)
        {
            Debug.LogWarning("[CarTrafficController] Nenhum prefab de carro configurado — nenhum carro spawnado.");
            return;
        }

        _roadNetwork = new RoadNetwork(_placementManager.Grid);
        if (_roadNetwork.RoadCells.Count == 0)
        {
            Debug.LogWarning("[CarTrafficController] Grid sem nenhuma célula CellType.Road — nenhum carro spawnado.");
            return;
        }

        var carsContainer = new GameObject("Cars").transform;
        carsContainer.SetParent(_placementManager.transform, false);

        for (var i = 0; i < _carCount; i++)
            SpawnCar(carsContainer);
    }

    private void SpawnCar(Transform parent)
    {
        var prefab = _carPrefabs[Random.Range(0, _carPrefabs.Length)];
        var startCell = _roadNetwork.RoadCells[Random.Range(0, _roadNetwork.RoadCells.Count)];
        var speed = Random.Range(_minSpeed, _maxSpeed);
        var seed = Random.Range(int.MinValue, int.MaxValue);

        var instance = Instantiate(prefab, parent);
        var driver = instance.AddComponent<CarDriver>();
        driver.Initialize(_placementManager, _roadNetwork, startCell, speed, _laneOffset, _forwardAxisCorrectionDegrees, seed);
    }
}
