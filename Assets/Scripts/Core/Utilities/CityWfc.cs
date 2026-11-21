using System;
using System.Collections.Generic;

// Lados de uma celula como bits; uma rua liga a celula ao vizinho de cada bit ligado.
[Flags]
public enum RoadSides
{
    None = 0,
    North = 1,
    East = 2,
    South = 4,
    West = 8,
    All = 15
}

public enum RoadShape
{
    None,
    Straight,
    Corner,
    TJunction,
    Cross,
    DeadEnd
}

// Uso de um lote; a cena liga cada uso a uma lista de estruturas.
public enum CityLotUse
{
    House,
    Apartment,
    Shop,
    Tower,
    Green
}

// Cidade planejada: rua (com os lados ligados) ou lote (com uso e frente) em cada celula.
public class CityPlan
{
    private readonly RoadSides[,] _roads;
    private readonly CityLotUse[,] _uses;
    private readonly float[,] _facing;

    public int Width { get; }
    public int Height { get; }

    // Quantas tentativas o gerador gastou; a reserva entra quando nenhuma deu certo.
    public int Attempts { get; internal set; }
    public bool UsedFallback { get; internal set; }

    public CityPlan(int width, int height)
    {
        Width = width;
        Height = height;
        _roads = new RoadSides[width, height];
        _uses = new CityLotUse[width, height];
        _facing = new float[width, height];
    }

    public bool IsInBounds(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Height;

    public bool IsRoad(int x, int z) => _roads[x, z] != RoadSides.None;

    public RoadSides RoadAt(int x, int z) => _roads[x, z];

    public CityLotUse LotUseAt(int x, int z) => _uses[x, z];

    // Graus no eixo Y, em multiplos de 90, para o lote encarar a rua.
    public float FacingAt(int x, int z) => _facing[x, z];

    internal void SetRoad(int x, int z, RoadSides sides) => _roads[x, z] = sides;

    internal void SetLot(int x, int z, CityLotUse use, float facing)
    {
        _uses[x, z] = use;
        _facing[x, z] = facing;
    }

    public int RoadCount
    {
        get
        {
            var count = 0;
            for (var x = 0; x < Width; x++)
                for (var z = 0; z < Height; z++)
                    if (IsRoad(x, z))
                        count++;
            return count;
        }
    }

    // Forma e rotacao (graus no eixo Y, sentido horario visto de cima) da peca de rua.
    // Pecas na posicao 0: reta ligando norte e sul, curva ligando norte e leste, T sem o lado oeste, beco aberto ao norte.
    public static RoadShape ShapeOf(RoadSides sides, out float rotation)
    {
        rotation = 0f;
        var mask = (int)sides;
        var count = 0;
        for (var bit = 0; bit < 4; bit++)
            if ((mask & (1 << bit)) != 0)
                count++;

        switch (count)
        {
            case 0:
                return RoadShape.None;
            case 4:
                return RoadShape.Cross;
            case 1:
                rotation = FirstBit(mask) * 90f;
                return RoadShape.DeadEnd;
            case 3:
                // O lado que falta, girado tres quartos, da a rotacao do T.
                rotation = ((FirstBit(~mask & 15) + 1) % 4) * 90f;
                return RoadShape.TJunction;
            default:
                if (mask == 5 || mask == 10)
                {
                    rotation = mask == 5 ? 0f : 90f;
                    return RoadShape.Straight;
                }
                // Curva: o primeiro lado no sentido horario cujo seguinte tambem esta ligado.
                for (var bit = 0; bit < 4; bit++)
                {
                    var next = (bit + 1) % 4;
                    if ((mask & (1 << bit)) != 0 && (mask & (1 << next)) != 0)
                    {
                        rotation = bit * 90f;
                        break;
                    }
                }
                return RoadShape.Corner;
        }
    }

    private static int FirstBit(int mask)
    {
        for (var bit = 0; bit < 4; bit++)
            if ((mask & (1 << bit)) != 0)
                return bit;
        return 0;
    }
}

// Parametros do gerador; os padroes dao uma cidade parecida em densidade com a desenhada a mao.
public class CityWfcOptions
{
    public int Width = 15;
    public int Height = 15;

    // Peso de cada forma de rua no sorteio da celula.
    public double EmptyWeight = 8.0;
    public double StraightWeight = 12.0;
    public double CornerWeight = 0.6;
    public double TJunctionWeight = 2.5;
    public double CrossWeight = 1.0;

    public double MinRoadShare = 0.28;
    public double MaxRoadShare = 0.48;

    // Lote mais longe que isso da rua vira verde (miolo de quadra); passou do teto, tenta de novo.
    public int MaxDistanceToRoad = 2;
    public int MaxFarCells = 12;
    public int MinBorderExits = 2;
    public int MaxAttempts = 400;

    // Peso base de cada uso de lote, na ordem do enum.
    public double[] LotWeights = { 3.0, 2.0, 2.0, 1.0, 1.2 };

    // Multiplicadores: comercio e torre perto dos cruzamentos, casa longe do centro, verde perto de verde.
    public double NearCrossingBoost = 3.0;
    public double OuterHouseBoost = 2.0;
    public double GreenClusterBoost = 3.0;
}

// Gera a cidade em duas camadas de Wave Function Collapse: primeiro as ruas, depois o uso dos lotes.
// Mesma semente, mesma cidade. Se nenhuma tentativa passar nas regras globais, usa o tracado em grade.
public static class CityWfc
{
    private static readonly RoadSides[] Sides = { RoadSides.North, RoadSides.East, RoadSides.South, RoadSides.West };
    private static readonly int[] Dx = { 0, 1, 0, -1 };
    private static readonly int[] Dz = { 1, 0, -1, 0 };

    // Mascaras das pecas de rua possiveis (sem beco sem saida); a primeira e a celula sem rua.
    private static readonly int[] RoadTiles = { 0, 5, 10, 3, 6, 12, 9, 7, 14, 13, 11, 15 };

    public static CityPlan Generate(ulong seed, CityWfcOptions options = null)
    {
        options ??= new CityWfcOptions();
        for (var attempt = 0; attempt < options.MaxAttempts; attempt++)
        {
            var random = new SeededRandom(seed + (ulong)attempt * 0x9E3779B97F4A7C15UL);
            var roads = CollapseRoads(options, random);
            if (roads == null)
                continue;
            KeepLargestNetwork(roads);
            if (PassesGlobalRules(roads, options) == false)
                continue;

            var plan = BuildPlan(roads, options, random);
            plan.Attempts = attempt + 1;
            return plan;
        }

        var fallback = BuildPlan(FallbackRoads(options.Width, options.Height), options, new SeededRandom(seed));
        fallback.Attempts = options.MaxAttempts;
        fallback.UsedFallback = true;
        return fallback;
    }

    // Tracado em grade da cidade original: ruas nas colunas e linhas 3, 7 e 11, saindo pelas bordas.
    public static RoadSides[,] FallbackRoads(int width, int height)
    {
        var isRoad = new bool[width, height];
        for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                isRoad[x, z] = x % 4 == 3 || z % 4 == 3;

        var roads = new RoadSides[width, height];
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                if (isRoad[x, z] == false)
                    continue;
                var mask = 0;
                for (var d = 0; d < 4; d++)
                {
                    var nx = x + Dx[d];
                    var nz = z + Dz[d];
                    var inside = nx >= 0 && nx < width && nz >= 0 && nz < height;
                    // Na borda, a rua continua para fora quando o lado oposto tambem e rua.
                    var opposite = (d + 2) % 4;
                    var ox = x + Dx[opposite];
                    var oz = z + Dz[opposite];
                    var oppositeRoad = ox >= 0 && ox < width && oz >= 0 && oz < height && isRoad[ox, oz];
                    if ((inside && isRoad[nx, nz]) || (inside == false && oppositeRoad))
                        mask |= 1 << d;
                }
                roads[x, z] = (RoadSides)mask;
            }
        }
        return roads;
    }

    private static RoadSides[,] CollapseRoads(CityWfcOptions options, Random random)
    {
        var width = options.Width;
        var height = options.Height;
        var weights = new double[RoadTiles.Length];
        for (var i = 0; i < RoadTiles.Length; i++)
            weights[i] = TileWeight(RoadTiles[i], options);

        var all = (1 << RoadTiles.Length) - 1;
        var domain = new int[width, height];
        for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                domain[x, z] = all;

        var queue = new Queue<(int X, int Z)>();
        while (true)
        {
            // Celula de menor entropia ainda aberta; o ruido desempata sem vies de posicao.
            var best = (X: -1, Z: -1);
            var bestEntropy = double.MaxValue;
            for (var x = 0; x < width; x++)
            {
                for (var z = 0; z < height; z++)
                {
                    var options2 = domain[x, z];
                    if (options2 == 0)
                        return null;
                    if ((options2 & (options2 - 1)) == 0)
                        continue;
                    var entropy = Entropy(options2, weights) + random.NextDouble() * 1e-6;
                    if (entropy < bestEntropy)
                    {
                        bestEntropy = entropy;
                        best = (x, z);
                    }
                }
            }

            if (best.X < 0)
                break;

            var chosen = PickWeighted(domain[best.X, best.Z], weights, random);
            domain[best.X, best.Z] = 1 << chosen;
            queue.Enqueue(best);
            if (Propagate(domain, queue, width, height) == false)
                return null;
        }

        var roads = new RoadSides[width, height];
        for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                roads[x, z] = (RoadSides)RoadTiles[SingleIndex(domain[x, z])];
        return roads;
    }

    private static double TileWeight(int mask, CityWfcOptions options)
    {
        var shape = CityPlan.ShapeOf((RoadSides)mask, out _);
        switch (shape)
        {
            case RoadShape.None: return options.EmptyWeight;
            case RoadShape.Straight: return options.StraightWeight / 2.0;
            case RoadShape.Corner: return options.CornerWeight / 4.0;
            case RoadShape.TJunction: return options.TJunctionWeight / 4.0;
            default: return options.CrossWeight;
        }
    }

    // Compatibilidade entre pecas vizinhas: lado ligado so encosta em lado ligado, e duas ruas
    // so ficam lado a lado se estiverem ligadas (sem ruas paralelas coladas).
    private static int[,] BuildCompatibility()
    {
        var compat = new int[RoadTiles.Length, 4];
        for (var i = 0; i < RoadTiles.Length; i++)
        {
            for (var d = 0; d < 4; d++)
            {
                var opposite = (d + 2) % 4;
                var open = (RoadTiles[i] & (1 << d)) != 0;
                for (var j = 0; j < RoadTiles.Length; j++)
                {
                    var otherOpen = (RoadTiles[j] & (1 << opposite)) != 0;
                    bool ok;
                    if (open)
                        ok = otherOpen && TurnsTogether(RoadTiles[i], RoadTiles[j], d) == false;
                    else if (RoadTiles[i] != 0)
                        ok = RoadTiles[j] == 0;
                    else
                        ok = otherOpen == false;
                    if (ok)
                        compat[i, d] |= 1 << j;
                }
            }
        }
        return compat;
    }

    // Duas ruas ligadas lado a lado que abrem para o mesmo lado formariam uma quadra de 2x2 so de rua.
    private static bool TurnsTogether(int a, int b, int direction)
    {
        var horizontal = direction == 1 || direction == 3;
        var sideA = horizontal ? 1 : 2;
        var sideB = horizontal ? 4 : 8;
        return ((a & sideA) != 0 && (b & sideA) != 0) || ((a & sideB) != 0 && (b & sideB) != 0);
    }

    private static readonly int[,] Compatibility = BuildCompatibility();

    private static bool Propagate(int[,] domain, Queue<(int X, int Z)> queue, int width, int height)
    {
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            for (var d = 0; d < 4; d++)
            {
                var nx = x + Dx[d];
                var nz = z + Dz[d];
                if (nx < 0 || nx >= width || nz < 0 || nz >= height)
                    continue;

                var allowed = 0;
                for (var i = 0; i < RoadTiles.Length; i++)
                    if ((domain[x, z] & (1 << i)) != 0)
                        allowed |= Compatibility[i, d];

                var before = domain[nx, nz];
                var after = before & allowed;
                if (after == before)
                    continue;
                if (after == 0)
                    return false;
                domain[nx, nz] = after;
                queue.Enqueue((nx, nz));
            }

            if (EnforceBlockDepth(domain, queue, x, z, width, height) == false)
                return false;
        }
        return true;
    }

    // Entre duas ruas paralelas cabem pelo menos 2 lotes: um lote nunca tem rua dos dois lados opostos.
    // A regra olha tres celulas em linha, entao roda a parte da compatibilidade entre pares.
    private static bool EnforceBlockDepth(int[,] domain, Queue<(int X, int Z)> queue, int x, int z, int width, int height)
    {
        const int noRoad = 1;
        bool IsLot(int cx, int cz) => domain[cx, cz] == noRoad;
        bool IsRoad(int cx, int cz) => (domain[cx, cz] & noRoad) == 0;
        bool Inside(int cx, int cz) => cx >= 0 && cx < width && cz >= 0 && cz < height;

        bool ForbidRoad(int cx, int cz)
        {
            var before = domain[cx, cz];
            var after = before & noRoad;
            if (after == before)
                return true;
            if (after == 0)
                return false;
            domain[cx, cz] = after;
            queue.Enqueue((cx, cz));
            return true;
        }

        for (var d = 0; d < 4; d++)
        {
            var ax = x + Dx[d];
            var az = z + Dz[d];
            if (Inside(ax, az) == false)
                continue;

            // Esta celula e lote no meio: rua de um lado proibe rua do outro.
            var bx = x - Dx[d];
            var bz = z - Dz[d];
            if (IsLot(x, z) && Inside(bx, bz) && IsRoad(ax, az) && ForbidRoad(bx, bz) == false)
                return false;

            // Esta celula e rua na ponta: lote ao lado proibe rua logo depois dele.
            var fx = x + 2 * Dx[d];
            var fz = z + 2 * Dz[d];
            if (IsRoad(x, z) && IsLot(ax, az) && Inside(fx, fz) && ForbidRoad(fx, fz) == false)
                return false;
        }
        return true;
    }

    // Fica so o maior pedaco da rede; os outros viram lote. Como pedacos separados nao se tocam
    // por lado ligado, apagar um inteiro nao deixa rua apontando para o vazio.
    public static void KeepLargestNetwork(RoadSides[,] roads)
    {
        var width = roads.GetLength(0);
        var height = roads.GetLength(1);
        var label = new int[width, height];
        var sizes = new List<int> { 0 };
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                if (roads[x, z] == RoadSides.None || label[x, z] != 0)
                    continue;
                var id = sizes.Count;
                var size = 0;
                var stack = new Stack<(int X, int Z)>();
                stack.Push((x, z));
                label[x, z] = id;
                while (stack.Count > 0)
                {
                    var (cx, cz) = stack.Pop();
                    size++;
                    for (var d = 0; d < 4; d++)
                    {
                        if ((roads[cx, cz] & Sides[d]) == 0)
                            continue;
                        var nx = cx + Dx[d];
                        var nz = cz + Dz[d];
                        if (nx < 0 || nx >= width || nz < 0 || nz >= height || label[nx, nz] != 0)
                            continue;
                        label[nx, nz] = id;
                        stack.Push((nx, nz));
                    }
                }
                sizes.Add(size);
            }
        }

        var biggest = 0;
        for (var i = 1; i < sizes.Count; i++)
            if (biggest == 0 || sizes[i] > sizes[biggest])
                biggest = i;
        for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                if (label[x, z] != biggest)
                    roads[x, z] = RoadSides.None;
    }

    private static double Entropy(int options, double[] weights)
    {
        var sum = 0.0;
        var sumLog = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            if ((options & (1 << i)) == 0)
                continue;
            sum += weights[i];
            sumLog += weights[i] * Math.Log(weights[i]);
        }
        return Math.Log(sum) - sumLog / sum;
    }

    private static int PickWeighted(int options, double[] weights, Random random)
    {
        var sum = 0.0;
        for (var i = 0; i < weights.Length; i++)
            if ((options & (1 << i)) != 0)
                sum += weights[i];

        var roll = random.NextDouble() * sum;
        var last = -1;
        for (var i = 0; i < weights.Length; i++)
        {
            if ((options & (1 << i)) == 0)
                continue;
            last = i;
            roll -= weights[i];
            if (roll < 0)
                return i;
        }
        return last;
    }

    private static int SingleIndex(int options)
    {
        for (var i = 0; i < 32; i++)
            if ((options & (1 << i)) != 0)
                return i;
        return 0;
    }

    // Regras que o colapso local nao garante: rede conexa, densidade, miolo longe da rua, saidas e sem quadras de 2x2 so de rua.
    public static bool PassesGlobalRules(RoadSides[,] roads, CityWfcOptions options)
    {
        var width = roads.GetLength(0);
        var height = roads.GetLength(1);
        var roadCount = 0;
        var start = (X: -1, Z: -1);
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                if (roads[x, z] == RoadSides.None)
                    continue;
                roadCount++;
                if (start.X < 0)
                    start = (x, z);
            }
        }

        var share = roadCount / (double)(width * height);
        if (roadCount == 0 || share < options.MinRoadShare || share > options.MaxRoadShare)
            return false;

        if (CountReachable(roads, start) != roadCount)
            return false;

        if (CountBorderExits(roads) < options.MinBorderExits)
            return false;

        for (var x = 0; x + 1 < width; x++)
            for (var z = 0; z + 1 < height; z++)
                if (roads[x, z] != 0 && roads[x + 1, z] != 0 && roads[x, z + 1] != 0 && roads[x + 1, z + 1] != 0)
                    return false;

        var distance = DistanceToRoad(roads);
        var far = 0;
        for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                if (distance[x, z] > options.MaxDistanceToRoad)
                    far++;

        return far <= options.MaxFarCells;
    }

    // Celulas de rua alcancaveis a partir de uma, andando so por lados ligados.
    public static int CountReachable(RoadSides[,] roads, (int X, int Z) start)
    {
        var width = roads.GetLength(0);
        var height = roads.GetLength(1);
        var seen = new bool[width, height];
        var stack = new Stack<(int X, int Z)>();
        stack.Push(start);
        seen[start.X, start.Z] = true;
        var count = 0;
        while (stack.Count > 0)
        {
            var (x, z) = stack.Pop();
            count++;
            for (var d = 0; d < 4; d++)
            {
                if ((roads[x, z] & Sides[d]) == 0)
                    continue;
                var nx = x + Dx[d];
                var nz = z + Dz[d];
                if (nx < 0 || nx >= width || nz < 0 || nz >= height || seen[nx, nz])
                    continue;
                seen[nx, nz] = true;
                stack.Push((nx, nz));
            }
        }
        return count;
    }

    // Ruas que saem da cidade por um lado ligado na borda.
    public static int CountBorderExits(RoadSides[,] roads)
    {
        var width = roads.GetLength(0);
        var height = roads.GetLength(1);
        var exits = 0;
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                for (var d = 0; d < 4; d++)
                {
                    if ((roads[x, z] & Sides[d]) == 0)
                        continue;
                    var nx = x + Dx[d];
                    var nz = z + Dz[d];
                    if (nx < 0 || nx >= width || nz < 0 || nz >= height)
                        exits++;
                }
            }
        }
        return exits;
    }

    // Distancia em passos ortogonais ate a rua mais proxima (0 na propria rua).
    public static int[,] DistanceToRoad(RoadSides[,] roads)
    {
        var width = roads.GetLength(0);
        var height = roads.GetLength(1);
        var distance = new int[width, height];
        var queue = new Queue<(int X, int Z)>();
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                distance[x, z] = roads[x, z] != RoadSides.None ? 0 : int.MaxValue;
                if (distance[x, z] == 0)
                    queue.Enqueue((x, z));
            }
        }
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            for (var d = 0; d < 4; d++)
            {
                var nx = x + Dx[d];
                var nz = z + Dz[d];
                if (nx < 0 || nx >= width || nz < 0 || nz >= height || distance[nx, nz] <= distance[x, z] + 1)
                    continue;
                distance[nx, nz] = distance[x, z] + 1;
                queue.Enqueue((nx, nz));
            }
        }
        return distance;
    }

    private static CityPlan BuildPlan(RoadSides[,] roads, CityWfcOptions options, Random random)
    {
        var width = roads.GetLength(0);
        var height = roads.GetLength(1);
        var plan = new CityPlan(width, height);
        for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                plan.SetRoad(x, z, roads[x, z]);

        CollapseLots(plan, options, random);
        return plan;
    }

    // Segunda camada: uso de cada lote. Torre nunca encosta em casa e so fica de frente para a rua;
    // o miolo longe da rua e verde; os outros usos combinam com tudo, entao essa camada nao trava.
    private static void CollapseLots(CityPlan plan, CityWfcOptions options, Random random)
    {
        var width = plan.Width;
        var height = plan.Height;
        var useCount = Enum.GetValues(typeof(CityLotUse)).Length;
        var distance = DistanceToRoad(Copy(plan));
        var nearCrossing = NearCrossing(plan, 2);

        var all = (1 << useCount) - 1;
        var domain = new int[width, height];
        var open = new List<(int X, int Z)>();
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                if (plan.IsRoad(x, z))
                    continue;
                if (distance[x, z] > options.MaxDistanceToRoad)
                    domain[x, z] = 1 << (int)CityLotUse.Green;
                else
                    domain[x, z] = distance[x, z] == 1 ? all : all & ~(1 << (int)CityLotUse.Tower);
                open.Add((x, z));
            }
        }

        var centerX = (width - 1) / 2.0;
        var centerZ = (height - 1) / 2.0;
        var outerRadius = Math.Min(width, height) * 0.35;
        var weights = new double[useCount];

        while (open.Count > 0)
        {
            var bestIndex = -1;
            var bestEntropy = double.MaxValue;
            for (var i = 0; i < open.Count; i++)
            {
                var (x, z) = open[i];
                FillLotWeights(weights, domain, plan, x, z, options, nearCrossing, centerX, centerZ, outerRadius);
                var entropy = Entropy(domain[x, z], weights) + random.NextDouble() * 1e-6;
                if (entropy < bestEntropy)
                {
                    bestEntropy = entropy;
                    bestIndex = i;
                }
            }

            var cell = open[bestIndex];
            open.RemoveAt(bestIndex);
            FillLotWeights(weights, domain, plan, cell.X, cell.Z, options, nearCrossing, centerX, centerZ, outerRadius);
            var use = (CityLotUse)PickWeighted(domain[cell.X, cell.Z], weights, random);
            domain[cell.X, cell.Z] = 1 << (int)use;
            plan.SetLot(cell.X, cell.Z, use, FacingDegrees(plan, cell.X, cell.Z));

            // Torre e casa nao ficam lado a lado.
            var banned = use == CityLotUse.Tower ? CityLotUse.House : use == CityLotUse.House ? CityLotUse.Tower : (CityLotUse?)null;
            if (banned == null)
                continue;
            for (var d = 0; d < 4; d++)
            {
                var nx = cell.X + Dx[d];
                var nz = cell.Z + Dz[d];
                if (plan.IsInBounds(nx, nz) && plan.IsRoad(nx, nz) == false && (domain[nx, nz] & (domain[nx, nz] - 1)) != 0)
                    domain[nx, nz] &= ~(1 << (int)banned.Value);
            }
        }
    }

    private static void FillLotWeights(double[] weights, int[,] domain, CityPlan plan, int x, int z, CityWfcOptions options,
        bool[,] nearCrossing, double centerX, double centerZ, double outerRadius)
    {
        var greenNeighbors = 0;
        for (var d = 0; d < 4; d++)
        {
            var nx = x + Dx[d];
            var nz = z + Dz[d];
            if (plan.IsInBounds(nx, nz) && plan.IsRoad(nx, nz) == false && domain[nx, nz] == 1 << (int)CityLotUse.Green)
                greenNeighbors++;
        }

        var outer = Math.Sqrt((x - centerX) * (x - centerX) + (z - centerZ) * (z - centerZ)) > outerRadius;
        for (var i = 0; i < weights.Length; i++)
        {
            var weight = i < options.LotWeights.Length ? options.LotWeights[i] : 1.0;
            var use = (CityLotUse)i;
            if (nearCrossing[x, z] && (use == CityLotUse.Shop || use == CityLotUse.Tower))
                weight *= options.NearCrossingBoost;
            if (outer && use == CityLotUse.House)
                weight *= options.OuterHouseBoost;
            if (use == CityLotUse.Green && greenNeighbors > 0)
                weight *= Math.Pow(options.GreenClusterBoost, greenNeighbors);
            weights[i] = Math.Max(weight, 1e-9);
        }
    }

    // Celulas a ate "radius" passos de um T ou cruzamento.
    private static bool[,] NearCrossing(CityPlan plan, int radius)
    {
        var near = new bool[plan.Width, plan.Height];
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                var shape = CityPlan.ShapeOf(plan.RoadAt(x, z), out _);
                if (shape != RoadShape.TJunction && shape != RoadShape.Cross)
                    continue;
                for (var ax = -radius; ax <= radius; ax++)
                    for (var az = -radius; az <= radius; az++)
                        if (Math.Abs(ax) + Math.Abs(az) <= radius && plan.IsInBounds(x + ax, z + az))
                            near[x + ax, z + az] = true;
            }
        }
        return near;
    }

    private static RoadSides[,] Copy(CityPlan plan)
    {
        var roads = new RoadSides[plan.Width, plan.Height];
        for (var x = 0; x < plan.Width; x++)
            for (var z = 0; z < plan.Height; z++)
                roads[x, z] = plan.RoadAt(x, z);
        return roads;
    }

    // Vira o lote para a rua mais proxima em linha reta; sem rua em linha, para a mais proxima no geral.
    public static float FacingDegrees(CityPlan plan, int x, int z)
    {
        float[] degrees = { 0f, 90f, 180f, 270f };
        var bestDistance = int.MaxValue;
        var facing = 0f;
        for (var d = 0; d < 4; d++)
        {
            for (var step = 1; ; step++)
            {
                var nx = x + Dx[d] * step;
                var nz = z + Dz[d] * step;
                if (plan.IsInBounds(nx, nz) == false)
                    break;
                if (plan.IsRoad(nx, nz))
                {
                    if (step < bestDistance)
                    {
                        bestDistance = step;
                        facing = degrees[d];
                    }
                    break;
                }
            }
        }
        if (bestDistance != int.MaxValue)
            return facing;

        var best = int.MaxValue;
        for (var rx = 0; rx < plan.Width; rx++)
        {
            for (var rz = 0; rz < plan.Height; rz++)
            {
                if (plan.IsRoad(rx, rz) == false)
                    continue;
                var dist = Math.Abs(rx - x) + Math.Abs(rz - z);
                if (dist >= best)
                    continue;
                best = dist;
                facing = Math.Abs(rx - x) > Math.Abs(rz - z) ? (rx > x ? 90f : 270f) : (rz > z ? 0f : 180f);
            }
        }
        return facing;
    }
}
