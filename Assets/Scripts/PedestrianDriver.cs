using UnityEngine;

// Pedestre andando pela mesma malha dos carros, sempre na beira da celula, fora de qualquer faixa.
public class PedestrianDriver : RoadActorDriver
{
    // Nomes dos estados de caminhada do animator, tocados manualmente por nao haver transicao automatica.
    private static readonly int WalkFemaleHash = Animator.StringToHash("locom_f_basicWalk_30f");
    private static readonly int WalkMaleHash = Animator.StringToHash("locom_m_basicWalk_30f");

    private float _edgeOffset;
    private float _crossingChance;
    private int _lateralSign;
    private Animator _animator;

    public void Initialize(
        PlacementManager placementManager,
        RoadNetwork roadNetwork,
        (int X, int Z) startCell,
        float speed,
        float edgeOffset,
        float crossingChance,
        float forwardAxisCorrectionDegrees,
        int randomSeed)
    {
        _edgeOffset = edgeOffset;
        _crossingChance = crossingChance;
        // Paridade da seed decide o lado inicial, dando variedade sem precisar de mais estado.
        _lateralSign = (randomSeed & 1) == 0 ? 1 : -1;

        PlayWalkAnimation();
        InitializeMovement(placementManager, roadNetwork, startCell, speed, forwardAxisCorrectionDegrees, randomSeed);
    }

    private void PlayWalkAnimation()
    {
        _animator = GetComponentInChildren<Animator>(true);
        if (_animator == null)
            return;

        int walkHash;
        if (_animator.HasState(0, WalkFemaleHash))
            walkHash = WalkFemaleHash;
        else if (_animator.HasState(0, WalkMaleHash))
            walkHash = WalkMaleHash;
        else
            return;

        _animator.Play(walkHash, 0, 0f);
        // Forca avaliar o estado na hora, senao a inicializacao preguicosa do animator pode sobrescrever.
        _animator.Update(0f);
    }

    // So troca de lado ao sair de uma interseção, com uma chance por vez, nunca no meio de um trecho reto.
    protected override Vector3 ComputeLateralOffset((int X, int Z) from, (int X, int Z) to)
    {
        var isIntersection = RoadNetwork.GetNeighbors(from).Count == 4;
        if (isIntersection && Rng.NextDouble() < _crossingChance)
            _lateralSign = -_lateralSign;

        var deltaX = to.X - from.X;
        var deltaZ = to.Z - from.Z;
        return new Vector3(deltaZ, 0f, -deltaX) * (_lateralSign * _edgeOffset);
    }
}
