using UnityEngine;

// Carros nas ruas: a Populacao da quantos, a Mobilidade da o ritmo.
public class CarTrafficController : RoadTrafficController
{
    [SerializeField] private GameObject[] _carPrefabs;
    [SerializeField] private int _minCars = 4;
    [SerializeField] private int _maxCars = 14;

    [Tooltip("Distância do centro da célula (cellSize=1) até o centro de cada faixa, 2 faixas por rua (uma pra cada sentido). Precisa deixar espaço até a borda da célula pra nunca se sobrepor com a calçada do pedestre.")]
    [SerializeField] private float _laneOffset = 0.22f;

    protected override string ContainerName => "Cars";
    protected override bool HasPrefabs => _carPrefabs != null && _carPrefabs.Length > 0;

    protected override RoadActorDriver CreateActor(Transform parent)
    {
        var instance = Instantiate(_carPrefabs[Random.Range(0, _carPrefabs.Length)], parent);
        return instance.AddComponent<CarDriver>();
    }

    protected override void LaunchActor(RoadActorDriver actor, (int X, int Z) startCell, float speed, int seed, bool growIn)
    {
        ((CarDriver)actor).Initialize(Placement, Network, startCell, speed, _laneOffset, ForwardAxisCorrection, seed, growIn);
    }

    protected override int CountFor(float population, float mobility) => TrafficDensity.ActorCount(population, _minCars, _maxCars);

    protected override float SpeedFactorFor(float mobility) => TrafficDensity.SpeedFactor(mobility);
}
