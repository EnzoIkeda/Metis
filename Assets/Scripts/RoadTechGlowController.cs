using System.Collections.Generic;
using UnityEngine;

// Luzes tecnologicas das ruas, que acendem conforme a Pesquisa da cidade: meio-fio em neon,
// pulsos de dados correndo pelas ruas e, na Smart City, faixa central e cruzamentos.
// Reavalia ao carregar a fase e no fim de cada turno; o brilho muda aos poucos.
public class RoadTechGlowController : MonoBehaviour
{
    [SerializeField] private PlacementManager _placementManager;
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CityStatsManager _cityStatsManager;

    [Tooltip("Material aditivo sem luz; cada camada usa uma copia com a propria cor.")]
    [SerializeField] private Material _glowMaterial;

    [SerializeField, ColorUsage(false, true)] private Color _curbColor = new Color(0f, 0.85f, 1f);
    [SerializeField, ColorUsage(false, true)] private Color _centerColor = new Color(1f, 0.15f, 0.85f);
    [SerializeField, ColorUsage(false, true)] private Color _crossingColor = new Color(0.55f, 1f, 1f);
    [SerializeField, ColorUsage(false, true)] private Color _pulseColor = new Color(0.6f, 1f, 1f);

    [SerializeField] private float _lineWidth = 0.06f;
    [SerializeField] private float _height = 0.014f;
    [SerializeField] private int _maxPulses = 12;

    [Tooltip("Celulas por segundo.")]
    [SerializeField] private float _pulseSpeed = 2.6f;

    [SerializeField] private float _pulseSize = 0.2f;

    [Tooltip("Segundos para o brilho ir de um nivel ao outro.")]
    [SerializeField] private float _fadeSeconds = 1.2f;

    [Tooltip("Segundos entre a entrada ou a saida de um pulso.")]
    [SerializeField] private float _pulseStepInterval = 0.25f;

    private readonly List<Pulse> _pulses = new List<Pulse>();
    private readonly Stack<Pulse> _idlePulses = new Stack<Pulse>();
    private readonly List<Material> _materials = new List<Material>();
    private readonly List<Mesh> _meshes = new List<Mesh>();
    private RoadNetwork _network;
    private Transform _container;
    private Layer _curbs;
    private Layer _center;
    private Layer _crossings;
    private Material _pulseMaterial;
    private Mesh _pulseMesh;
    private System.Random _random;
    private float _nextPulseStep;

    public float CurbTarget { get; private set; }
    public float SmartTarget { get; private set; }
    public float CurbLevel { get; private set; }
    public float SmartLevel { get; private set; }
    public int PulseTarget { get; private set; }
    public int ActivePulses => _pulses.Count;
    public bool IsRunning => _network != null;

    private class Layer
    {
        public MeshRenderer Renderer;
        public Material Material;
        public Color Color;
    }

    private class Pulse
    {
        public GameObject Root;
        public TrailRenderer Trail;
        public (int X, int Z) From;
        public (int X, int Z) To;
        public float Progress;
        public bool Retiring;
    }

    private void Start()
    {
        if (_placementManager == null || _placementManager.Plan == null || _placementManager.Grid == null)
        {
            Debug.LogWarning("[RoadTechGlowController] Sem PlacementManager com cidade planejada, ruas sem luzes.");
            return;
        }
        if (_glowMaterial == null)
        {
            Debug.LogWarning("[RoadTechGlowController] Sem material de brilho, ruas sem luzes.");
            return;
        }

        var network = new RoadNetwork(_placementManager.Grid);
        if (network.RoadCells.Count == 0)
            return;
        _network = network;
        _random = new System.Random((int)(_placementManager.LayoutSeed & 0x7FFFFFFF));

        _container = new GameObject("RoadTechGlow").transform;
        _container.SetParent(_placementManager.transform, false);

        var plan = _placementManager.Plan;
        _curbs = BuildLayer("Curbs", RoadTechGlow.CurbSegments(plan), _curbColor, _lineWidth, 0f);
        _center = BuildLayer("Center", RoadTechGlow.CenterSegments(plan), _centerColor, _lineWidth * 0.75f, 0.001f);
        _crossings = BuildLayer("Crossings", RoadTechGlow.CrossingSegments(plan), _crossingColor, _lineWidth * 1.2f, 0.002f);
        _pulseMaterial = NewMaterial(_pulseColor);
        _pulseMesh = Track(QuadMesh(_pulseSize));

        if (_turnManager != null)
            _turnManager.OnTurnFinished += HandleTurnFinished;

        Evaluate();
        // Ao carregar, a cidade ja aparece como esta; so as mudancas de turno vem aos poucos.
        CurbLevel = CurbTarget;
        SmartLevel = SmartTarget;
        while (_pulses.Count < PulseTarget)
            SpawnPulse(true);
        ApplyLevels();
    }

    private void OnDestroy()
    {
        if (_turnManager != null)
            _turnManager.OnTurnFinished -= HandleTurnFinished;
        foreach (var material in _materials)
            if (material != null)
                Destroy(material);
        foreach (var mesh in _meshes)
            if (mesh != null)
                Destroy(mesh);
    }

    private void HandleTurnFinished(PhaseTurnRecord record) => Evaluate();

    // Le a Pesquisa e define o brilho e a quantidade de pulsos que a cidade vai alcancar.
    public void Evaluate()
    {
        var stats = _cityStatsManager != null ? _cityStatsManager.Stats : null;
        var pesquisa = stats != null ? stats.GetValue(CityParameterType.Pesquisa) : 0f;
        CurbTarget = RoadTechGlow.CurbIntensity(pesquisa);
        SmartTarget = RoadTechGlow.SmartIntensity(pesquisa);
        PulseTarget = RoadTechGlow.PulseCount(pesquisa, _maxPulses);
        _nextPulseStep = Time.time;
    }

    private void Update()
    {
        if (_network == null)
            return;

        var step = _fadeSeconds > 0f ? Time.deltaTime / _fadeSeconds : 1f;
        CurbLevel = Mathf.MoveTowards(CurbLevel, CurbTarget, step);
        SmartLevel = Mathf.MoveTowards(SmartLevel, SmartTarget, step);
        ApplyLevels();

        var running = _pulses.Count - CountRetiring();
        if (running != PulseTarget && Time.time >= _nextPulseStep)
        {
            _nextPulseStep = Time.time + _pulseStepInterval;
            if (running < PulseTarget)
                SpawnPulse(false);
            else
                RetireOne();
        }
        MovePulses(Time.deltaTime);
    }

    private void ApplyLevels()
    {
        SetLayer(_curbs, CurbLevel);
        SetLayer(_center, SmartLevel);
        // Os cruzamentos respiram devagar, como sinal de pedestre inteligente.
        SetLayer(_crossings, SmartLevel * (0.7f + 0.3f * Mathf.Sin(Time.time * 2.4f)));
    }

    private static void SetLayer(Layer layer, float level)
    {
        if (layer == null)
            return;
        var visible = level > 0.001f;
        if (layer.Renderer.enabled != visible)
            layer.Renderer.enabled = visible;
        if (visible)
            layer.Material.color = layer.Color * level;
    }

    private Layer BuildLayer(string name, List<RoadGlowSegment> segments, Color color, float width, float lift)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_container, false);
        go.AddComponent<MeshFilter>().sharedMesh = Track(LineMesh(segments, width, _height + lift, _placementManager.CellSize));
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var material = NewMaterial(color);
        renderer.sharedMaterial = material;
        renderer.enabled = false;
        return new Layer { Renderer = renderer, Material = material, Color = color };
    }

    private Material NewMaterial(Color color)
    {
        var material = new Material(_glowMaterial) { color = color };
        _materials.Add(material);
        return material;
    }

    private Mesh Track(Mesh mesh)
    {
        _meshes.Add(mesh);
        return mesh;
    }

    // Uma faixa plana por trecho, esticada meia largura nas pontas para as emendas fecharem.
    private static Mesh LineMesh(List<RoadGlowSegment> segments, float width, float height, float cellSize)
    {
        var vertices = new List<Vector3>(segments.Count * 4);
        var triangles = new List<int>(segments.Count * 6);
        var half = width * 0.5f;
        foreach (var segment in segments)
        {
            var a = new Vector3(segment.X1 * cellSize, height, segment.Z1 * cellSize);
            var b = new Vector3(segment.X2 * cellSize, height, segment.Z2 * cellSize);
            var along = b - a;
            if (along.sqrMagnitude < 1e-8f)
                continue;
            along.Normalize();
            var side = new Vector3(-along.z, 0f, along.x) * half;
            a -= along * half;
            b += along * half;
            var start = vertices.Count;
            vertices.Add(a - side);
            vertices.Add(a + side);
            vertices.Add(b + side);
            vertices.Add(b - side);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
        var mesh = new Mesh { name = "RoadGlowLines" };
        if (vertices.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(EnsureUpFacing(triangles, vertices), 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Vira os triangulos que ficaram de costas para a camera, que olha de cima.
    private static List<int> EnsureUpFacing(List<int> triangles, List<Vector3> vertices)
    {
        for (var i = 0; i < triangles.Count; i += 3)
        {
            var normal = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
            if (normal.y < 0f)
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        }
        return triangles;
    }

    private static Mesh QuadMesh(float size)
    {
        var half = size * 0.5f;
        var mesh = new Mesh { name = "RoadGlowPulse" };
        // No plano XY: o pulso e girado para deitar no chao.
        mesh.SetVertices(new List<Vector3> { new Vector3(-half, -half, 0f), new Vector3(-half, half, 0f), new Vector3(half, half, 0f), new Vector3(half, -half, 0f) });
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void SpawnPulse(bool anywhere)
    {
        var pulse = _idlePulses.Count > 0 ? _idlePulses.Pop() : CreatePulse();
        var cells = _network.RoadCells;
        pulse.From = cells[_random.Next(cells.Count)];
        pulse.To = _network.PickNextCell(pulse.From, null, _random);
        pulse.Progress = anywhere ? (float)_random.NextDouble() : 0f;
        pulse.Retiring = false;
        pulse.Root.SetActive(true);
        pulse.Root.transform.localPosition = PulsePosition(pulse);
        pulse.Trail.Clear();
        pulse.Trail.emitting = true;
        _pulses.Add(pulse);
    }

    private Pulse CreatePulse()
    {
        var root = new GameObject("Pulse");
        root.transform.SetParent(_container, false);
        root.AddComponent<MeshFilter>().sharedMesh = _pulseMesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _pulseMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var trail = root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = _pulseMaterial;
        trail.time = 0.5f;
        trail.minVertexDistance = 0.05f;
        trail.widthMultiplier = _pulseSize * 0.6f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.alignment = LineAlignment.TransformZ;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.startColor = Color.white;
        trail.endColor = new Color(1f, 1f, 1f, 0f);
        // O rastro deita no chao: o eixo Z do pulso aponta para cima.
        root.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        return new Pulse { Root = root, Trail = trail };
    }

    // Quem sai termina o trecho atual e some, sem cortar o rastro no meio da rua.
    private void RetireOne()
    {
        foreach (var pulse in _pulses)
        {
            if (pulse.Retiring)
                continue;
            pulse.Retiring = true;
            return;
        }
    }

    private int CountRetiring()
    {
        var count = 0;
        foreach (var pulse in _pulses)
            if (pulse.Retiring)
                count++;
        return count;
    }

    private void MovePulses(float deltaTime)
    {
        for (var i = _pulses.Count - 1; i >= 0; i--)
        {
            var pulse = _pulses[i];
            pulse.Progress += deltaTime * _pulseSpeed;
            while (pulse.Progress >= 1f)
            {
                if (pulse.Retiring)
                    break;
                pulse.Progress -= 1f;
                var next = _network.PickNextCell(pulse.To, pulse.From, _random);
                pulse.From = pulse.To;
                pulse.To = next;
            }
            if (pulse.Retiring && pulse.Progress >= 1f)
            {
                pulse.Trail.emitting = false;
                pulse.Root.SetActive(false);
                _pulses.RemoveAt(i);
                _idlePulses.Push(pulse);
                continue;
            }
            pulse.Root.transform.localPosition = PulsePosition(pulse);
        }
    }

    private Vector3 PulsePosition(Pulse pulse)
    {
        var size = _placementManager.CellSize;
        var x = Mathf.Lerp(pulse.From.X, pulse.To.X, pulse.Progress) * size;
        var z = Mathf.Lerp(pulse.From.Z, pulse.To.Z, pulse.Progress) * size;
        return new Vector3(x, _height + 0.004f, z);
    }
}
