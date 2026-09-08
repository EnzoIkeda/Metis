using UnityEngine;

// Carro andando pela malha de ruas, sempre numa faixa, nunca no centro nem na beira dos pedestres.
public class CarDriver : RoadActorDriver
{
    private float _laneOffset;

    // Seed propria pra cada carro decidir seu caminho de forma independente.
    public void Initialize(
        PlacementManager placementManager,
        RoadNetwork roadNetwork,
        (int X, int Z) startCell,
        float speed,
        float laneOffset,
        float forwardAxisCorrectionDegrees,
        int randomSeed)
    {
        _laneOffset = laneOffset;
        InitializeMovement(placementManager, roadNetwork, startCell, speed, forwardAxisCorrectionDegrees, randomSeed);
    }

    // Rotaciona a direcao 90 graus em sentido horario pra achar o lado da faixa de mao-direita.
    protected override Vector3 ComputeLateralOffset((int X, int Z) from, (int X, int Z) to)
    {
        var deltaX = to.X - from.X;
        var deltaZ = to.Z - from.Z;
        return new Vector3(deltaZ, 0f, -deltaX) * _laneOffset;
    }
}
