using UnityEngine;

// Base comum de quem anda pela malha de ruas celula a celula, so movendo e girando o transform.
public abstract class RoadActorDriver : MonoBehaviour
{
    protected PlacementManager PlacementManager { get; private set; }
    protected RoadNetwork RoadNetwork { get; private set; }

    // Nomeado Rng pra nao colidir com a referencia nao qualificada ao gerador aleatorio do motor.
    protected System.Random Rng { get; private set; }

    private float _speed;
    private float _forwardAxisCorrectionDegrees;

    protected (int X, int Z) CurrentCell { get; private set; }
    protected (int X, int Z) PreviousCell { get; private set; }
    protected bool HasPreviousCell { get; private set; }

    private Vector3 _fromLocalPosition;
    private Vector3 _toLocalPosition;
    private float _segmentProgress;

    protected void InitializeMovement(
        PlacementManager placementManager,
        RoadNetwork roadNetwork,
        (int X, int Z) startCell,
        float speed,
        float forwardAxisCorrectionDegrees,
        int randomSeed)
    {
        PlacementManager = placementManager;
        RoadNetwork = roadNetwork;
        _speed = speed;
        _forwardAxisCorrectionDegrees = forwardAxisCorrectionDegrees;
        Rng = new System.Random(randomSeed);

        CurrentCell = startCell;
        HasPreviousCell = false;

        transform.localPosition = PlacementManager.CellToLocalPosition(new Vector3Int(startCell.X, 0, startCell.Z));
        PickNewSegment();
    }

    private void Update()
    {
        if (PlacementManager == null || RoadNetwork == null)
            return;

        var segmentLength = Vector3.Distance(_fromLocalPosition, _toLocalPosition);
        if (segmentLength <= 0.0001f)
        {
            // Celula isolada sem vizinho de rua, entao so fica parado.
            return;
        }

        _segmentProgress += _speed * Time.deltaTime / segmentLength;
        if (_segmentProgress >= 1f)
        {
            transform.localPosition = _toLocalPosition;
            PickNewSegment();
            return;
        }

        transform.localPosition = Vector3.Lerp(_fromLocalPosition, _toLocalPosition, _segmentProgress);
    }

    private void PickNewSegment()
    {
        var next = RoadNetwork.PickNextCell(CurrentCell, HasPreviousCell ? PreviousCell : ((int, int)?)null, Rng);
        var lateralOffset = ComputeLateralOffset(CurrentCell, next);

        // Continua de onde o ator fisicamente esta, pra mudanca de faixa acontecer suave ao longo do segmento.
        _fromLocalPosition = transform.localPosition;
        _toLocalPosition = PlacementManager.CellToLocalPosition(new Vector3Int(next.X, 0, next.Z)) + lateralOffset;
        _segmentProgress = 0f;

        var direction = _toLocalPosition - _fromLocalPosition;
        if (direction.sqrMagnitude > 0.0001f)
            transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, _forwardAxisCorrectionDegrees, 0f);

        PreviousCell = CurrentCell;
        HasPreviousCell = true;
        CurrentCell = next;
    }

    // Deslocamento lateral aplicado ao ponto de chegada do proximo segmento.
    protected abstract Vector3 ComputeLateralOffset((int X, int Z) from, (int X, int Z) to);
}
