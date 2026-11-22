using System;
using System.Collections.Generic;

public enum CardMarkKind
{
    None,
    ReplaceLot,
    RoofProp,
    StreetProp
}

// Onde a marca prefere ficar; sem lugar preferido livre, vale qualquer um.
public enum CardMarkPlacement
{
    Any,
    Edge,
    NearCrossing
}

// Regra de apresentacao de uma carta: que marca deixa, onde prefere e quantas vezes na fase.
public struct CardMarkRule
{
    public CardMarkKind Kind;
    public CardMarkPlacement Placement;

    // Troca de lote que vira verde (parque, horta) deixa de ter telhado.
    public bool ResultIsGreen;

    // Troca de lote que vira predio de telhado plano: depois pode receber peca no telhado.
    public bool ResultHasFlatRoof;
    public int Limit;

    // Quantos lugares vizinhos a marca ocupa de uma vez (1 a MaxSize), para a mudanca ser notada.
    public int Size;

    // Troca de lote: um so predio grande sobre um quadrado de 2x2 lotes, quando houver um livre.
    public bool Merge;
}

public struct CardMarkSpot
{
    public int X;
    public int Z;

    // Na calcada, o lado da rua (0 norte, 1 leste, 2 sul, 3 oeste) em que a peca fica; -1 nas outras marcas.
    public int Side;
}

public struct CardMark
{
    public string CardId;
    public CardMarkKind Kind;

    // Primeiro lugar da marca (o canto de menor X e Z quando funde 2x2).
    public int X;
    public int Z;
    public int Side;
    public int Turn;

    // Todos os lugares ocupados, o primeiro igual a X, Z e Side.
    public CardMarkSpot[] Spots;

    // Os quatro lotes de Spots viraram um predio so.
    public bool Merged;
}

// Escolhe onde cada carta jogada deixa a sua marca na cidade planejada. O sorteio sai da semente
// do tracado, do turno e da carta, entao refazer as jogadas na mesma ordem refaz as mesmas marcas.
public class CardMarkPlanner
{
    public const int DefaultLimit = 3;
    public const int MaxSize = 4;

    // Distancia maxima, em celulas, entre o primeiro lugar e os outros de uma mesma marca.
    private const int Reach = 3;

    private static readonly int[] Dx = { 0, 1, 0, -1 };
    private static readonly int[] Dz = { 1, 0, -1, 0 };

    private readonly CityPlan _plan;
    private readonly ulong _seed;
    private readonly bool[,] _nearCrossing;
    private readonly Dictionary<(int X, int Z), bool> _replaced = new Dictionary<(int X, int Z), bool>();
    private readonly HashSet<(int X, int Z)> _roofs = new HashSet<(int X, int Z)>();
    private readonly HashSet<(int X, int Z)> _merged = new HashSet<(int X, int Z)>();

    // Lotes com telhado plano (predio e torre); casa tem telhado inclinado e loja tem letreiro.
    private readonly HashSet<(int X, int Z)> _flatRoofs = new HashSet<(int X, int Z)>();
    private readonly HashSet<(int X, int Z, int Side)> _sidewalks = new HashSet<(int X, int Z, int Side)>();
    private readonly Dictionary<string, int> _countByCard = new Dictionary<string, int>();
    private readonly List<CardMark> _marks = new List<CardMark>();

    public IReadOnlyList<CardMark> Marks => _marks;

    public CardMarkPlanner(CityPlan plan, ulong layoutSeed)
    {
        _plan = plan;
        _seed = layoutSeed;
        _nearCrossing = new bool[plan.Width, plan.Height];
        for (var x = 0; x < plan.Width; x++)
            for (var z = 0; z < plan.Height; z++)
                if (plan.IsRoad(x, z) == false && (plan.LotUseAt(x, z) == CityLotUse.Apartment || plan.LotUseAt(x, z) == CityLotUse.Tower))
                    _flatRoofs.Add((x, z));
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var shape = CityPlan.ShapeOf(plan.RoadAt(x, z), out _);
                if (shape != RoadShape.TJunction && shape != RoadShape.Cross)
                    continue;
                for (var ax = -2; ax <= 2; ax++)
                    for (var az = -2; az <= 2; az++)
                        if (Math.Abs(ax) + Math.Abs(az) <= 2 && plan.IsInBounds(x + ax, z + az))
                            _nearCrossing[x + ax, z + az] = true;
            }
        }
    }

    // Lote trocado e se virou verde; a cena usa para saber o que esta em cada lugar.
    public bool IsReplaced(int x, int z, out bool isGreen) => _replaced.TryGetValue((x, z), out isGreen);

    public bool TryMark(string cardId, CardMarkRule rule, int turn, out CardMark mark)
    {
        mark = default;
        if (rule.Kind == CardMarkKind.None || string.IsNullOrEmpty(cardId))
            return false;

        var limit = rule.Limit > 0 ? rule.Limit : DefaultLimit;
        _countByCard.TryGetValue(cardId, out var count);
        if (count >= limit)
            return false;

        var random = new SeededRandom(Mix(_seed, (ulong)turn, Fnv(cardId)));
        var size = Math.Max(1, Math.Min(MaxSize, rule.Size));
        var spots = new List<CardMarkSpot>();
        var merged = false;
        switch (rule.Kind)
        {
            case CardMarkKind.ReplaceLot:
                if (rule.Merge && TryPickSquare(rule.Placement, random, out var corner))
                {
                    merged = true;
                    for (var dx = 0; dx < 2; dx++)
                        for (var dz = 0; dz < 2; dz++)
                            spots.Add(Spot(corner.X + dx, corner.Z + dz, -1));
                }
                else
                {
                    PickCluster(LotCandidates(rule.Placement, roof: false), size, random, IsFreeLot, spots);
                }
                foreach (var spot in spots)
                {
                    _replaced[(spot.X, spot.Z)] = rule.ResultIsGreen;
                    _flatRoofs.Remove((spot.X, spot.Z));
                    if (merged)
                        _merged.Add((spot.X, spot.Z));
                    else if (rule.ResultHasFlatRoof && rule.ResultIsGreen == false)
                        _flatRoofs.Add((spot.X, spot.Z));
                }
                break;
            case CardMarkKind.RoofProp:
                var roofs = new List<CardMarkSpot>();
                foreach (var cell in LotCandidates(rule.Placement, roof: true))
                    roofs.Add(Spot(cell.X, cell.Z, -1));
                PickNear(roofs, size, random, spots);
                foreach (var spot in spots)
                    _roofs.Add((spot.X, spot.Z));
                break;
            default:
                var sidewalks = new List<CardMarkSpot>();
                foreach (var place in SidewalkCandidates(rule.Placement))
                    sidewalks.Add(Spot(place.X, place.Z, place.Side));
                PickNear(sidewalks, size, random, spots);
                foreach (var spot in spots)
                    _sidewalks.Add((spot.X, spot.Z, spot.Side));
                break;
        }

        if (spots.Count == 0)
            return false;
        mark = new CardMark
        {
            CardId = cardId,
            Kind = rule.Kind,
            X = spots[0].X,
            Z = spots[0].Z,
            Side = spots[0].Side,
            Turn = turn,
            Spots = spots.ToArray(),
            Merged = merged,
        };
        _countByCard[cardId] = count + 1;
        _marks.Add(mark);
        return true;
    }

    private static CardMarkSpot Spot(int x, int z, int side) => new CardMarkSpot { X = x, Z = z, Side = side };

    private bool IsFreeLot(int x, int z)
    {
        return _plan.IsInBounds(x, z) && _plan.IsRoad(x, z) == false && _replaced.ContainsKey((x, z)) == false && _roofs.Contains((x, z)) == false;
    }

    // Quadrado de 2x2 lotes livres, de preferencia no lugar pedido.
    private bool TryPickSquare(CardMarkPlacement placement, Random random, out (int X, int Z) corner)
    {
        var all = new List<(int X, int Z)>();
        var preferred = new List<(int X, int Z)>();
        for (var x = 0; x < _plan.Width - 1; x++)
        {
            for (var z = 0; z < _plan.Height - 1; z++)
            {
                if (IsFreeLot(x, z) == false || IsFreeLot(x + 1, z) == false || IsFreeLot(x, z + 1) == false || IsFreeLot(x + 1, z + 1) == false)
                    continue;
                all.Add((x, z));
                if (Prefers(placement, x, z) || Prefers(placement, x + 1, z + 1))
                    preferred.Add((x, z));
            }
        }
        return TryPick(preferred.Count > 0 ? preferred : all, random, out corner);
    }

    // Lotes vizinhos a partir de um sorteado, crescendo pela borda livre; tenta outros comecos
    // ate achar o tamanho pedido e fica com o maior encontrado.
    private static void PickCluster(List<(int X, int Z)> candidates, int size, Random random, Func<int, int, bool> isFree, List<CardMarkSpot> result)
    {
        const int tries = 8;
        for (var attempt = 0; attempt < tries && candidates.Count > 0 && result.Count < size; attempt++)
        {
            var start = candidates[random.Next(candidates.Count)];
            var cluster = new List<(int X, int Z)> { start };
            var taken = new HashSet<(int X, int Z)> { start };
            while (cluster.Count < size)
            {
                var frontier = new List<(int X, int Z)>();
                foreach (var cell in cluster)
                {
                    for (var d = 0; d < 4; d++)
                    {
                        var next = (cell.X + Dx[d], cell.Z + Dz[d]);
                        if (taken.Contains(next) == false && frontier.Contains(next) == false && isFree(next.Item1, next.Item2))
                            frontier.Add(next);
                    }
                }
                if (frontier.Count == 0)
                    break;
                var pick = frontier[random.Next(frontier.Count)];
                cluster.Add(pick);
                taken.Add(pick);
            }
            if (cluster.Count > result.Count)
            {
                result.Clear();
                foreach (var cell in cluster)
                    result.Add(Spot(cell.X, cell.Z, -1));
            }
        }
    }

    // Primeiro lugar sorteado e os outros entre os mais perto dele, ate Reach celulas.
    private static void PickNear(List<CardMarkSpot> spots, int size, Random random, List<CardMarkSpot> result)
    {
        if (spots.Count == 0)
            return;
        var first = spots[random.Next(spots.Count)];
        result.Add(first);
        var near = new List<(CardMarkSpot Spot, int Distance, int Tie)>();
        foreach (var spot in spots)
        {
            var distance = Math.Abs(spot.X - first.X) + Math.Abs(spot.Z - first.Z);
            if (distance > Reach || (spot.X == first.X && spot.Z == first.Z && spot.Side == first.Side))
                continue;
            near.Add((spot, distance, random.Next(1000)));
        }
        near.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Tie.CompareTo(b.Tie));
        for (var i = 0; i < near.Count && result.Count < size; i++)
            result.Add(near[i].Spot);
    }

    // Lote livre para trocar (sem troca nem peca no telhado) ou telhado livre (predio, sem peca ainda).
    private List<(int X, int Z)> LotCandidates(CardMarkPlacement placement, bool roof)
    {
        var all = new List<(int X, int Z)>();
        var preferred = new List<(int X, int Z)>();
        for (var x = 0; x < _plan.Width; x++)
        {
            for (var z = 0; z < _plan.Height; z++)
            {
                if (_plan.IsRoad(x, z))
                    continue;

                if (roof)
                {
                    if (_roofs.Contains((x, z)) || _flatRoofs.Contains((x, z)) == false)
                        continue;
                }
                else if (_replaced.ContainsKey((x, z)) || _roofs.Contains((x, z)))
                {
                    continue;
                }

                all.Add((x, z));
                if (Prefers(placement, x, z))
                    preferred.Add((x, z));
            }
        }
        return preferred.Count > 0 ? preferred : all;
    }

    // Calcada de rua reta virada para um lote, ainda sem peca.
    private List<(int X, int Z, int Side)> SidewalkCandidates(CardMarkPlacement placement)
    {
        var all = new List<(int X, int Z, int Side)>();
        var preferred = new List<(int X, int Z, int Side)>();
        for (var x = 0; x < _plan.Width; x++)
        {
            for (var z = 0; z < _plan.Height; z++)
            {
                if (CityPlan.ShapeOf(_plan.RoadAt(x, z), out _) != RoadShape.Straight)
                    continue;
                for (var side = 0; side < 4; side++)
                {
                    if ((_plan.RoadAt(x, z) & (RoadSides)(1 << side)) != 0)
                        continue;
                    var nx = x + Dx[side];
                    var nz = z + Dz[side];
                    if (_plan.IsInBounds(nx, nz) == false || _plan.IsRoad(nx, nz) || _sidewalks.Contains((x, z, side)))
                        continue;
                    all.Add((x, z, side));
                    if (Prefers(placement, x, z))
                        preferred.Add((x, z, side));
                }
            }
        }
        return preferred.Count > 0 ? preferred : all;
    }

    private bool Prefers(CardMarkPlacement placement, int x, int z)
    {
        switch (placement)
        {
            case CardMarkPlacement.Edge:
                return x <= 1 || z <= 1 || x >= _plan.Width - 2 || z >= _plan.Height - 2;
            case CardMarkPlacement.NearCrossing:
                return _nearCrossing[x, z];
            default:
                return false;
        }
    }

    private static bool TryPick(List<(int X, int Z)> candidates, Random random, out (int X, int Z) pick)
    {
        pick = default;
        if (candidates.Count == 0)
            return false;
        pick = candidates[random.Next(candidates.Count)];
        return true;
    }

    private static ulong Mix(ulong seed, ulong turn, ulong card)
    {
        var mixer = new SeededRandom(seed ^ (turn * 0x9E3779B97F4A7C15UL) ^ (card * 0xC2B2AE3D27D4EB4FUL));
        return mixer.NextUInt64();
    }

    // Hash estavel entre execucoes (o da linguagem muda a cada processo).
    private static ulong Fnv(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
