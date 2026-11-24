using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Deixa na cidade a marca de cada carta jogada: troca lotes ou poe pecas no telhado e na calcada,
// caindo do alto com poeira. Ao continuar a fase, refaz as marcas do historico, sem animacao.
public class CardMarkController : MonoBehaviour
{
    private static readonly Vector3[] SideDirections = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private PlacementManager _placementManager;
    [SerializeField] private CardMarkLibraryData _library;

    [Tooltip("Segundos entre jogar a carta e a primeira peca comecar a cair, para o brilho do impacto aparecer antes.")]
    [SerializeField] private float _startDelay = 0.15f;

    [Tooltip("Deslocamento, em celulas, da peca de calcada ao longo da rua, para nao cair em cima do poste.")]
    [SerializeField] private float _sidewalkAlongOffset = 0.25f;

    [Tooltip("Distância do centro da célula até a borda da pista, em células; peça de calçada não passa dela.")]
    [SerializeField] private float _roadEdge = 0.4f;

    [Tooltip("Escala da poeira na troca de lote (o predio fundido dobra).")]
    [SerializeField] private float _lotDustScale = 1.6f;

    [Tooltip("Escala da poeira nas pecas pequenas (telhado e calcada).")]
    [SerializeField] private float _smallDustScale = 0.8f;

    private CardMarkPlanner _planner;
    private Transform _container;
    private int _running;

    public IReadOnlyList<CardMark> Marks => _planner != null ? _planner.Marks : Array.Empty<CardMark>();
    public bool IsAnimating => _running > 0;

    // Roda depois do gerenciador de turno, que retoma o historico no proprio Start.
    private void Start()
    {
        if (_turnManager == null || _placementManager == null || _placementManager.Plan == null || _library == null)
            return;

        _planner = new CardMarkPlanner(_placementManager.Plan, _placementManager.LayoutSeed);
        _container = new GameObject("Marks").transform;
        _container.SetParent(_placementManager.transform, false);

        RebuildFromHistory();
        _turnManager.OnCardPlayed += HandleCardPlayed;
    }

    private void OnDestroy()
    {
        if (_turnManager != null)
            _turnManager.OnCardPlayed -= HandleCardPlayed;
    }

    private void RebuildFromHistory()
    {
        var history = _turnManager.History;
        if (history == null)
            return;
        var lastTurn = -1;
        foreach (var record in history.Turns)
        {
            Mark(record.CardId, record.TurnIndex, false);
            lastTurn = record.TurnIndex;
        }
        // Salvo com o evento aberto, o turno da carta ainda nao fechou.
        var current = history.Current;
        if (current != null && current.TurnIndex != lastTurn)
            Mark(current.CardId, current.TurnIndex, false);
    }

    private void HandleCardPlayed(CardData card)
    {
        if (card == null || CardRules.IsFreeAction(card) || _turnManager.Machine == null)
            return;
        Mark(card.name, _turnManager.Machine.TurnIndex, true);
    }

    private void Mark(string cardId, int turn, bool animate)
    {
        if (string.IsNullOrEmpty(cardId) || _library.TryGet(cardId, out var entry) == false)
            return;
        if (_planner.TryMark(cardId, entry.Rule, turn, out var mark) == false)
            return;

        var random = new SeededRandom(_placementManager.LayoutSeed ^ ((ulong)(turn + 1) * 0xBF58476D1CE4E5B9UL) ^ StableHash(cardId));
        var arrivals = new List<Arrival>();
        switch (mark.Kind)
        {
            case CardMarkKind.ReplaceLot:
                PlaceLots(mark, entry, random, arrivals);
                break;
            case CardMarkKind.RoofProp:
                PlaceRoofProps(mark, entry, random, arrivals);
                break;
            default:
                PlaceSidewalkProps(mark, entry, random, arrivals);
                break;
        }

        if (animate && isActiveAndEnabled)
        {
            StartCoroutine(Animate(arrivals, mark.Kind == CardMarkKind.ReplaceLot ? _lotDustScale : _smallDustScale));
            return;
        }
        foreach (var arrival in arrivals)
            foreach (var old in arrival.Removed)
                Destroy(old);
    }

    private void PlaceLots(CardMark mark, CardMarkEntry entry, System.Random random, List<Arrival> arrivals)
    {
        var plan = _placementManager.Plan;
        var facing = plan.FacingAt(mark.X, mark.Z);
        if (mark.Merged)
        {
            var cells = new List<Vector3Int>();
            foreach (var spot in mark.Spots)
                cells.Add(new Vector3Int(spot.X, 0, spot.Z));
            var arrival = new Arrival { DustBoost = 2f };
            arrival.Piece = _placementManager.ReplaceLots(cells, Pick(entry, random), facing, true, arrival.Removed);
            Add(arrivals, arrival);
            return;
        }
        foreach (var spot in mark.Spots)
        {
            var arrival = new Arrival();
            var cell = new Vector3Int(spot.X, 0, spot.Z);
            arrival.Piece = _placementManager.ReplaceLots(new[] { cell }, Pick(entry, random), plan.FacingAt(spot.X, spot.Z), false, arrival.Removed);
            Add(arrivals, arrival);
        }
    }

    private void PlaceRoofProps(CardMark mark, CardMarkEntry entry, System.Random random, List<Arrival> arrivals)
    {
        foreach (var spot in mark.Spots)
        {
            var lot = _placementManager.LotObjectAt(new Vector3Int(spot.X, 0, spot.Z));
            if (lot == null)
                continue;
            var piece = Instantiate(Pick(entry, random), lot.transform);
            piece.transform.localPosition = new Vector3(0f, RoofHeight(lot), 0f);
            piece.SetActive(true);
            Add(arrivals, new Arrival { Piece = piece });
        }
    }

    private void PlaceSidewalkProps(CardMark mark, CardMarkEntry entry, System.Random random, List<Arrival> arrivals)
    {
        var cellSize = _placementManager.CellSize;
        foreach (var spot in mark.Spots)
        {
            if (spot.Side < 0 || spot.Side > 3)
                continue;
            var toSide = SideDirections[spot.Side];
            var along = new Vector3(Mathf.Abs(toSide.z), 0f, Mathf.Abs(toSide.x));
            var position = _placementManager.CellToLocalPosition(new Vector3Int(spot.X, 0, spot.Z))
                + toSide * (CityDecor.SidewalkOffset * cellSize)
                + along * (_sidewalkAlongOffset * cellSize);
            var piece = Instantiate(Pick(entry, random), _container);
            piece.transform.localPosition = position;
            // De frente para a rua.
            piece.transform.localRotation = Quaternion.Euler(0f, spot.Side * 90f + 180f, 0f) * piece.transform.localRotation;
            piece.SetActive(true);
            KeepOffTheRoad(piece, spot, toSide);
            Add(arrivals, new Arrival { Piece = piece });
        }
    }

    // A calcada do pavimento e estreita: empurra a peca para o lado do lote ate a borda dela sair da pista.
    private void KeepOffTheRoad(GameObject piece, CardMarkSpot spot, Vector3 toSide)
    {
        var renderers = piece.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;
        var parent = _container;
        var center = _placementManager.CellToLocalPosition(new Vector3Int(spot.X, 0, spot.Z));
        var inner = float.MaxValue;
        foreach (var renderer in renderers)
        {
            var bounds = renderer.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var world = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                inner = Mathf.Min(inner, Vector3.Dot(parent.InverseTransformPoint(world) - center, toSide));
            }
        }
        var edge = _roadEdge * _placementManager.CellSize;
        if (inner < edge)
            piece.transform.localPosition += toSide * (edge - inner);
    }

    private static void Add(List<Arrival> arrivals, Arrival arrival)
    {
        if (arrival.Piece == null)
            return;
        arrival.RestPosition = arrival.Piece.transform.localPosition;
        arrival.RestScale = arrival.Piece.transform.localScale;
        arrivals.Add(arrival);
    }

    private static GameObject Pick(CardMarkEntry entry, System.Random random)
    {
        return entry.Pieces[random.Next(entry.Pieces.Length)];
    }

    // Altura do topo do lote, no espaco dele, pelo maior alcance dos renderizadores.
    private static float RoofHeight(GameObject lot)
    {
        var top = float.MinValue;
        foreach (var renderer in lot.GetComponentsInChildren<Renderer>())
            top = Mathf.Max(top, renderer.bounds.max.y);
        if (top == float.MinValue)
            return 0f;
        return lot.transform.InverseTransformPoint(new Vector3(lot.transform.position.x, top, lot.transform.position.z)).y;
    }

    private IEnumerator Animate(List<Arrival> arrivals, float dustScale)
    {
        _running++;
        foreach (var arrival in arrivals)
            Hide(arrival);

        var duration = CardMarkMotion.Duration(arrivals.Count);
        var elapsed = -_startDelay;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            for (var i = 0; i < arrivals.Count; i++)
                Step(arrivals[i], elapsed, CardMarkMotion.StartOf(i), dustScale);
            yield return null;
        }
        foreach (var arrival in arrivals)
            Finish(arrival);
        _running--;
    }

    private static void Hide(Arrival arrival)
    {
        if (arrival.Piece != null)
            arrival.Piece.transform.localScale = Vector3.zero;
        foreach (var old in arrival.Removed)
            if (old != null)
                arrival.RemovedScales.Add(old.transform.localScale);
    }

    private void Step(Arrival arrival, float elapsed, float start, float dustScale)
    {
        for (var i = 0; i < arrival.Removed.Count; i++)
        {
            var old = arrival.Removed[i];
            if (old == null)
                continue;
            var sink = CardMarkMotion.SinkScale(elapsed);
            if (sink <= 0f)
            {
                Destroy(old);
                arrival.Removed[i] = null;
                continue;
            }
            old.transform.localScale = arrival.RemovedScales[i] * sink;
        }

        if (arrival.Piece == null || elapsed < start)
            return;
        var time = elapsed - start;
        var transform = arrival.Piece.transform;
        var scale = CardMarkMotion.Scale(time);
        transform.localScale = new Vector3(arrival.RestScale.x * scale.Horizontal, arrival.RestScale.y * scale.Vertical, arrival.RestScale.z * scale.Horizontal);
        transform.localPosition = arrival.RestPosition + Vector3.up * CardMarkMotion.Height(time);
        if (arrival.Landed == false && CardMarkMotion.HasLanded(time))
        {
            arrival.Landed = true;
            SpawnDust(transform.position, dustScale * arrival.DustBoost);
        }
    }

    private static void Finish(Arrival arrival)
    {
        foreach (var old in arrival.Removed)
            if (old != null)
                Destroy(old);
        arrival.Removed.Clear();
        if (arrival.Piece == null)
            return;
        arrival.Piece.transform.localPosition = arrival.RestPosition;
        arrival.Piece.transform.localScale = arrival.RestScale;
    }

    private void SpawnDust(Vector3 position, float scale)
    {
        if (_library.LandingDust == null)
            return;
        var dust = Instantiate(_library.LandingDust, position, Quaternion.identity);
        dust.transform.localScale *= scale;
        dust.SetActive(true);
        Destroy(dust, 3f);
    }

    private static ulong StableHash(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private class Arrival
    {
        public GameObject Piece;
        public Vector3 RestPosition;
        public Vector3 RestScale;
        public bool Landed;

        // Predio fundido levanta poeira do tamanho dele.
        public float DustBoost = 1f;
        public readonly List<GameObject> Removed = new List<GameObject>();
        public readonly List<Vector3> RemovedScales = new List<Vector3>();
    }
}
