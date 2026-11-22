using UnityEngine;

// Pedestres nas calcadas: a Populacao da quantos, e a Mobilidade alta puxa mais gente a pe.
public class PedestrianTrafficController : RoadTrafficController
{
    [SerializeField] private GameObject[] _pedestrianPrefabs;
    [SerializeField] private int _minPedestrians = 4;
    [SerializeField] private int _maxPedestrians = 18;

    [Tooltip("Distância do centro da célula até a beira da calçada, precisa ficar claramente fora da faixa de carro (mais a metade da largura de um carro) pra nunca se sobrepor.")]
    [SerializeField] private float _edgeOffset = 0.4f;

    [Tooltip("Chance (0-1) de trocar de lado da rua toda vez que um pedestre sai de uma célula de interseção (4 vizinhos), a \"faixa de pedestre\" do design.")]
    [Range(0f, 1f)]
    [SerializeField] private float _crossingChance = 0.35f;

    protected override string ContainerName => "Pedestrians";
    protected override bool HasPrefabs => _pedestrianPrefabs != null && _pedestrianPrefabs.Length > 0;

    protected override RoadActorDriver CreateActor(Transform parent)
    {
        var instance = Instantiate(_pedestrianPrefabs[Random.Range(0, _pedestrianPrefabs.Length)], parent);
        return instance.AddComponent<PedestrianDriver>();
    }

    protected override void LaunchActor(RoadActorDriver actor, (int X, int Z) startCell, float speed, int seed, bool growIn)
    {
        ((PedestrianDriver)actor).Initialize(Placement, Network, startCell, speed, _edgeOffset, _crossingChance, ForwardAxisCorrection, seed, growIn);
    }

    protected override int CountFor(float population, float mobility) => TrafficDensity.PedestrianCount(population, mobility, _minPedestrians, _maxPedestrians);

    // A pe o ritmo nao depende do transito.
    protected override float SpeedFactorFor(float mobility) => 1f;
}
