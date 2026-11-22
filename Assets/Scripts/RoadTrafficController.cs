using System.Collections.Generic;
using UnityEngine;

// Base dos controladores de quem anda pelas ruas: quantos andam e em que ritmo seguem a cidade.
// So reavalia em momentos estaveis (ao carregar a fase e no fim de cada turno) e reaproveita os atores que saem.
public abstract class RoadTrafficController : MonoBehaviour
{
    [SerializeField] private PlacementManager _placementManager;
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private float _minSpeed = 1f;
    [SerializeField] private float _maxSpeed = 2f;

    [Tooltip("Graus somados por cima da rotação calculada na direção de movimento, pra compensar o eixo \"de frente\" de verdade do mesh.")]
    [SerializeField] private float _forwardAxisCorrectionDegrees;

    [Tooltip("Segundos entre a entrada ou a saída de um ator quando a quantidade muda depois de um turno.")]
    [SerializeField] private float _stepInterval = 0.3f;

    private readonly List<RoadActorDriver> _active = new List<RoadActorDriver>();
    private readonly Stack<RoadActorDriver> _idle = new Stack<RoadActorDriver>();
    private readonly List<(int X, int Z)> _entryCells = new List<(int X, int Z)>();
    private RoadNetwork _roadNetwork;
    private Transform _container;
    private float _nextStepTime;

    public int TargetCount { get; private set; }
    public int ActiveCount => _active.Count;
    public float SpeedFactor { get; private set; } = 1f;
    public bool IsRunning => _roadNetwork != null;

    protected PlacementManager Placement => _placementManager;
    protected RoadNetwork Network => _roadNetwork;
    protected float ForwardAxisCorrection => _forwardAxisCorrectionDegrees;

    protected abstract string ContainerName { get; }
    protected abstract bool HasPrefabs { get; }

    // Cria um ator novo (instancia e componente), ainda sem posicao.
    protected abstract RoadActorDriver CreateActor(Transform parent);

    protected abstract void LaunchActor(RoadActorDriver actor, (int X, int Z) startCell, float speed, int seed, bool growIn);
    protected abstract int CountFor(float population, float mobility);
    protected abstract float SpeedFactorFor(float mobility);

    // Roda depois do gerenciador de turno, que aplica o comeco da fase no proprio Start.
    private void Start()
    {
        var label = GetType().Name;
        if (_placementManager == null || _placementManager.Grid == null)
        {
            Debug.LogWarning($"[{label}] Sem PlacementManager/Grid configurado, ninguém nas ruas.");
            return;
        }
        if (HasPrefabs == false)
        {
            Debug.LogWarning($"[{label}] Nenhum prefab configurado, ninguém nas ruas.");
            return;
        }

        var network = new RoadNetwork(_placementManager.Grid);
        if (network.RoadCells.Count == 0)
        {
            Debug.LogWarning($"[{label}] Grid sem nenhuma célula CellType.Road, ninguém nas ruas.");
            return;
        }
        _roadNetwork = network;
        CollectEntryCells();

        _container = new GameObject(ContainerName).transform;
        _container.SetParent(_placementManager.transform, false);

        if (_turnManager != null)
            _turnManager.OnTurnFinished += HandleTurnFinished;

        Evaluate();
        // Ao carregar, a cidade ja aparece cheia; so as mudancas de turno entram aos poucos.
        while (_active.Count < TargetCount)
            Spawn(false);
    }

    private void OnDestroy()
    {
        if (_turnManager != null)
            _turnManager.OnTurnFinished -= HandleTurnFinished;
    }

    private void Update()
    {
        if (_roadNetwork == null || _active.Count == TargetCount || Time.time < _nextStepTime)
            return;
        _nextStepTime = Time.time + _stepInterval;
        if (_active.Count < TargetCount)
            Spawn(true);
        else
            Retire();
    }

    private void HandleTurnFinished(PhaseTurnRecord record) => Evaluate();

    // Le a cidade e define a quantidade-alvo e o ritmo; os atores ja nas ruas mudam de ritmo aos poucos.
    public void Evaluate()
    {
        var stats = _cityStatsManager != null ? _cityStatsManager.Stats : null;
        var population = stats != null ? stats.GetValue(CityParameterType.Populacao) : 50f;
        var mobility = stats != null ? stats.GetValue(CityParameterType.Mobilidade) : TrafficDensity.FluidMobility;
        TargetCount = CountFor(population, mobility);
        SpeedFactor = SpeedFactorFor(mobility);
        foreach (var actor in _active)
            actor.SetSpeedFactor(SpeedFactor, false);
        _nextStepTime = Time.time;
    }

    private void Spawn(bool growIn)
    {
        var actor = _idle.Count > 0 ? _idle.Pop() : CreateActor(_container);
        actor.gameObject.SetActive(true);
        // Quem entra no meio da fase surge pela borda da cidade, longe do centro da tela.
        var cells = growIn && _entryCells.Count > 0 ? _entryCells : _roadNetwork.RoadCells;
        var startCell = cells[Random.Range(0, cells.Count)];
        LaunchActor(actor, startCell, Random.Range(_minSpeed, _maxSpeed), Random.Range(int.MinValue, int.MaxValue), growIn);
        actor.SetSpeedFactor(SpeedFactor, true);
        _active.Add(actor);
    }

    private void Retire()
    {
        var actor = _active[_active.Count - 1];
        _active.RemoveAt(_active.Count - 1);
        actor.Retire(done => _idle.Push(done));
    }

    private void CollectEntryCells()
    {
        _entryCells.Clear();
        var grid = _placementManager.Grid;
        foreach (var cell in _roadNetwork.RoadCells)
            if (cell.X == 0 || cell.Z == 0 || cell.X == grid.Width - 1 || cell.Z == grid.Height - 1)
                _entryCells.Add(cell);
    }
}
