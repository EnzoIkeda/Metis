using System;
using System.Collections.Generic;

// Ponto e direcao de quem anda; X e Z em unidades do grid (centro da celula em numero inteiro).
// Yaw em graus no eixo Y (0 olha para o norte, 90 para o leste); Pitch positivo olha para baixo.
public struct WalkPose
{
    public float X;
    public float Z;
    public float Yaw;
    public float Pitch;

    public WalkPose(float x, float z, float yaw, float pitch)
    {
        X = x;
        Z = z;
        Yaw = yaw;
        Pitch = pitch;
    }
}

// Passeio a pe pela cidade gerada: o corpo e um circulo que so anda sobre celulas de rua,
// desliza encostado nos lotes e nunca sai do grid.
public class StreetWalk
{
    public const float DefaultRadius = 0.15f;
    public const float MinPitch = -35f;
    public const float MaxPitch = 40f;

    private static readonly int[] Dx = { 0, 1, 0, -1 };
    private static readonly int[] Dz = { 1, 0, -1, 0 };

    private readonly CityPlan _plan;

    public float Radius { get; }

    public StreetWalk(CityPlan plan, float radius = DefaultRadius)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        // O corpo precisa caber no meio de uma rua estreita, com lote dos dois lados.
        Radius = Math.Max(0.01f, Math.Min(radius, 0.45f));
    }

    public static int CellOf(float coordinate) => (int)Math.Floor(coordinate + 0.5f);

    public bool IsWalkableCell(int x, int z) => _plan.IsInBounds(x, z) && _plan.IsRoad(x, z);

    // Livre se nenhum lote nem a borda do grid encosta no circulo.
    public bool IsFree(float x, float z)
    {
        var minX = CellOf(x - Radius);
        var maxX = CellOf(x + Radius);
        var minZ = CellOf(z - Radius);
        var maxZ = CellOf(z + Radius);
        for (var cx = minX; cx <= maxX; cx++)
        {
            for (var cz = minZ; cz <= maxZ; cz++)
            {
                if (IsWalkableCell(cx, cz))
                    continue;
                if (DistanceToCell(x, z, cx, cz) < Radius)
                    return false;
            }
        }
        return true;
    }

    // Anda o que der: cada eixo em separado, em passos menores que o raio, para deslizar na parede e nao atravessar quina.
    public WalkPose Move(WalkPose pose, float deltaX, float deltaZ)
    {
        var length = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        if (length <= 0.0 || float.IsNaN(deltaX) || float.IsNaN(deltaZ))
            return pose;

        var steps = (int)Math.Ceiling(length / (Radius * 0.5));
        var stepX = deltaX / steps;
        var stepZ = deltaZ / steps;
        for (var i = 0; i < steps; i++)
        {
            if (IsFree(pose.X + stepX, pose.Z))
                pose.X += stepX;
            if (IsFree(pose.X, pose.Z + stepZ))
                pose.Z += stepZ;
        }
        return pose;
    }

    // Anda na direcao do olhar: forward para a frente, strafe para a direita.
    public WalkPose Walk(WalkPose pose, float forward, float strafe, float distance)
    {
        var radians = pose.Yaw * Math.PI / 180.0;
        var sin = (float)Math.Sin(radians);
        var cos = (float)Math.Cos(radians);
        var dx = (sin * forward + cos * strafe) * distance;
        var dz = (cos * forward - sin * strafe) * distance;
        return Move(pose, dx, dz);
    }

    public static WalkPose Look(WalkPose pose, float deltaYaw, float deltaPitch)
    {
        var yaw = (pose.Yaw + deltaYaw) % 360f;
        if (yaw < 0f)
            yaw += 360f;
        pose.Yaw = yaw;
        pose.Pitch = Math.Max(MinPitch, Math.Min(MaxPitch, pose.Pitch + deltaPitch));
        return pose;
    }

    // Afasta o corpo de obstaculos redondos (os carros), so ate onde a rua deixa.
    public WalkPose PushOut(WalkPose pose, IReadOnlyList<(float X, float Z, float Radius)> obstacles)
    {
        if (obstacles == null)
            return pose;
        foreach (var obstacle in obstacles)
        {
            var dx = pose.X - obstacle.X;
            var dz = pose.Z - obstacle.Z;
            var distance = (float)Math.Sqrt(dx * dx + dz * dz);
            var minimum = obstacle.Radius + Radius;
            if (distance >= minimum)
                continue;
            if (distance < 1e-4f)
            {
                dx = 1f;
                dz = 0f;
                distance = 1f;
            }
            var push = minimum - distance;
            pose = Move(pose, dx / distance * push, dz / distance * push);
        }
        return pose;
    }

    // Ponto de partida: o cruzamento (ou T) mais perto do centro, olhando para a saida com mais rua pela frente.
    public static WalkPose StartPose(CityPlan plan)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        var centerX = (plan.Width - 1) / 2f;
        var centerZ = (plan.Height - 1) / 2f;
        var bestX = -1;
        var bestZ = -1;
        var bestScore = double.MaxValue;
        for (var x = 0; x < plan.Width; x++)
        {
            for (var z = 0; z < plan.Height; z++)
            {
                if (plan.IsRoad(x, z) == false)
                    continue;
                var shape = CityPlan.ShapeOf(plan.RoadAt(x, z), out _);
                var isJunction = shape == RoadShape.Cross || shape == RoadShape.TJunction;
                var distance = Math.Sqrt((x - centerX) * (x - centerX) + (z - centerZ) * (z - centerZ));
                // Cruzamento ganha de qualquer outra rua; entre iguais, o mais central.
                var score = distance + (isJunction ? 0.0 : 1000.0);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestX = x;
                    bestZ = z;
                }
            }
        }
        if (bestX < 0)
            return new WalkPose(centerX, centerZ, 0f, 0f);

        var sides = plan.RoadAt(bestX, bestZ);
        var bestSide = 0;
        var bestRun = -1;
        for (var side = 0; side < 4; side++)
        {
            if (((int)sides & (1 << side)) == 0)
                continue;
            var run = 0;
            var cx = bestX + Dx[side];
            var cz = bestZ + Dz[side];
            while (plan.IsInBounds(cx, cz) && plan.IsRoad(cx, cz))
            {
                run++;
                cx += Dx[side];
                cz += Dz[side];
            }
            if (run > bestRun)
            {
                bestRun = run;
                bestSide = side;
            }
        }
        return new WalkPose(bestX, bestZ, bestSide * 90f, 0f);
    }

    private static float DistanceToCell(float x, float z, int cellX, int cellZ)
    {
        var nearestX = Math.Max(cellX - 0.5f, Math.Min(x, cellX + 0.5f));
        var nearestZ = Math.Max(cellZ - 0.5f, Math.Min(z, cellZ + 0.5f));
        var dx = x - nearestX;
        var dz = z - nearestZ;
        return (float)Math.Sqrt(dx * dx + dz * dz);
    }
}

// Joystick virtual: o arraste a partir do centro vira um vetor de comprimento ate 1, com zona morta.
public static class StickInput
{
    public const float DefaultDeadZone = 0.12f;

    public static (float X, float Y) FromDrag(float offsetX, float offsetY, float radius, float deadZone = DefaultDeadZone)
    {
        if (radius <= 0f)
            return (0f, 0f);
        var x = offsetX / radius;
        var y = offsetY / radius;
        var length = (float)Math.Sqrt(x * x + y * y);
        if (length <= deadZone || float.IsNaN(length))
            return (0f, 0f);
        // Fora da zona morta, recomeca do zero para nao dar tranco na saida dela.
        var clamped = Math.Min(length, 1f);
        var scaled = (clamped - deadZone) / (1f - deadZone);
        return (x / length * scaled, y / length * scaled);
    }
}
