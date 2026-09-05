using UnityEngine;

// Carro andando pela malha de ruas celula a celula, instanciado e configurado por CarTrafficController.
public class CarDriver : MonoBehaviour
{
    private PlacementManager _placementManager;
    private RoadNetwork _roadNetwork;
    private System.Random _random;
    private float _speed;
    private float _forwardAxisCorrectionDegrees;

    private (int X, int Z) _currentCell;
    private (int X, int Z) _previousCell;
    private bool _hasPreviousCell;

    private Vector3 _fromLocalPosition;
    private Vector3 _toLocalPosition;
    private float _segmentProgress;

    // Seed propria pra cada carro decidir seu caminho de forma independente.
    public void Initialize(
        PlacementManager placementManager,
        RoadNetwork roadNetwork,
        (int X, int Z) startCell,
        float speed,
        float forwardAxisCorrectionDegrees,
        int randomSeed)
    {
        _placementManager = placementManager;
        _roadNetwork = roadNetwork;
        _speed = speed;
        _forwardAxisCorrectionDegrees = forwardAxisCorrectionDegrees;
        _random = new System.Random(randomSeed);

        _currentCell = startCell;
        _hasPreviousCell = false;

        transform.localPosition = _placementManager.CellToLocalPosition(new Vector3Int(startCell.X, 0, startCell.Z));
        PickNewSegment();
    }

    private void Update()
    {
        if (_placementManager == null || _roadNetwork == null)
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
        var next = _roadNetwork.PickNextCell(_currentCell, _hasPreviousCell ? _previousCell : ((int, int)?)null, _random);

        _fromLocalPosition = _placementManager.CellToLocalPosition(new Vector3Int(_currentCell.X, 0, _currentCell.Z));
        _toLocalPosition = _placementManager.CellToLocalPosition(new Vector3Int(next.X, 0, next.Z));
        _segmentProgress = 0f;

        var direction = _toLocalPosition - _fromLocalPosition;
        if (direction.sqrMagnitude > 0.0001f)
            transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, _forwardAxisCorrectionDegrees, 0f);

        _previousCell = _currentCell;
        _hasPreviousCell = true;
        _currentCell = next;
    }
}
