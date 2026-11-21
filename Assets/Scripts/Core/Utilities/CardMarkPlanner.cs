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
    public int Limit;
}

public struct CardMark
{
    public string CardId;
    public CardMarkKind Kind;
    public int X;
    public int Z;

    // Na calcada, o lado da rua (0 norte, 1 leste, 2 sul, 3 oeste) em que a peca fica; -1 nas outras marcas.
    public int Side;
    public int Turn;
}

// Escolhe onde cada carta jogada deixa a sua marca na cidade planejada. O sorteio sai da semente
// do tracado, do turno e da carta, entao refazer as jogadas na mesma ordem refaz as mesmas marcas.
public class CardMarkPlanner
{
    public const int DefaultLimit = 3;

    private static readonly int[] Dx = { 0, 1, 0, -1 };
    private static readonly int[] Dz = { 1, 0, -1, 0 };

    private readonly CityPlan _plan;
    private readonly ulong _seed;
    private readonly bool[,] _nearCrossing;
    private readonly Dictionary<(int X, int Z), bool> _replaced = new Dictionary<(int X, int Z), bool>();
    private readonly HashSet<(int X, int Z)> _roofs = new HashSet<(int X, int Z)>();
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
        bool placed;
        switch (rule.Kind)
        {
            case CardMarkKind.ReplaceLot:
                placed = TryPick(LotCandidates(rule.Placement, roof: false), random, out var lot);
                if (placed)
                {
                    _replaced[(lot.X, lot.Z)] = rule.ResultIsGreen;
                    mark = NewMark(cardId, rule.Kind, lot.X, lot.Z, -1, turn);
                }
                break;
            case CardMarkKind.RoofProp:
                placed = TryPick(LotCandidates(rule.Placement, roof: true), random, out var roof);
                if (placed)
                {
                    _roofs.Add((roof.X, roof.Z));
                    mark = NewMark(cardId, rule.Kind, roof.X, roof.Z, -1, turn);
                }
                break;
            default:
                var spots = SidewalkCandidates(rule.Placement);
                placed = spots.Count > 0;
                if (placed)
                {
                    var spot = spots[random.Next(spots.Count)];
                    _sidewalks.Add(spot);
                    mark = NewMark(cardId, rule.Kind, spot.X, spot.Z, spot.Side, turn);
                }
                break;
        }

        if (placed == false)
            return false;
        _countByCard[cardId] = count + 1;
        _marks.Add(mark);
        return true;
    }

    private static CardMark NewMark(string cardId, CardMarkKind kind, int x, int z, int side, int turn)
    {
        return new CardMark { CardId = cardId, Kind = kind, X = x, Z = z, Side = side, Turn = turn };
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
                    if (_roofs.Contains((x, z)))
                        continue;
                    var isGreen = _replaced.TryGetValue((x, z), out var replacedGreen) ? replacedGreen : _plan.LotUseAt(x, z) == CityLotUse.Green;
                    if (isGreen)
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
