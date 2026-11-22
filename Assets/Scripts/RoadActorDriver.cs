using System;
using UnityEngine;

// Base comum de quem anda pela malha de ruas celula a celula, so movendo e girando o transform.
public abstract class RoadActorDriver : MonoBehaviour
{
    protected PlacementManager PlacementManager { get; private set; }
    protected RoadNetwork RoadNetwork { get; private set; }

    // Nomeado Rng pra nao colidir com a referencia nao qualificada ao gerador aleatorio do motor.
    protected System.Random Rng { get; private set; }

    // Tempo de crescer ao entrar e de encolher ao sair, e quanto o ritmo muda por segundo.
    private const float ScaleDuration = 0.3f;
    private const float SpeedFactorChangePerSecond = 0.5f;

    private float _speed;
    private float _forwardAxisCorrectionDegrees;
    private float _speedFactor = 1f;
    private float _targetSpeedFactor = 1f;

    private Vector3 _baseScale;
    private bool _hasBaseScale;
    private float _scale = 1f;
    private bool _retiring;
    private Action<RoadActorDriver> _onRetired;

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
        int randomSeed,
        bool growIn)
    {
        if (_hasBaseScale == false)
        {
            _baseScale = transform.localScale;
            _hasBaseScale = true;
        }
        _retiring = false;
        _onRetired = null;
        _scale = growIn ? 0f : 1f;
        transform.localScale = _baseScale * _scale;

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

    // Ritmo sobre a velocidade sorteada; sem "immediate", chega aos poucos para nao dar tranco.
    public void SetSpeedFactor(float factor, bool immediate)
    {
        _targetSpeedFactor = factor;
        if (immediate)
            _speedFactor = factor;
    }

    // Encolhe ate sumir, desativa e avisa quem guarda os atores parados.
    public void Retire(Action<RoadActorDriver> onRetired)
    {
        _retiring = true;
        _onRetired = onRetired;
    }

    private void Update()
    {
        if (PlacementManager == null || RoadNetwork == null)
            return;

        if (UpdateScale() == false)
            return;
        _speedFactor = Mathf.MoveTowards(_speedFactor, _targetSpeedFactor, SpeedFactorChangePerSecond * Time.deltaTime);

        var segmentLength = Vector3.Distance(_fromLocalPosition, _toLocalPosition);
        if (segmentLength <= 0.0001f)
        {
            // Celula isolada sem vizinho de rua, entao so fica parado.
            return;
        }

        _segmentProgress += _speed * _speedFactor * Time.deltaTime / segmentLength;
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

    // Falso quando acabou de sumir.
    private bool UpdateScale()
    {
        var target = _retiring ? 0f : 1f;
        if (_scale == target)
        {
            if (_retiring == false)
                return true;
            _retiring = false;
            gameObject.SetActive(false);
            var done = _onRetired;
            _onRetired = null;
            done?.Invoke(this);
            return false;
        }
        _scale = Mathf.MoveTowards(_scale, target, Time.deltaTime / ScaleDuration);
        transform.localScale = _baseScale * _scale;
        return true;
    }

    // Deslocamento lateral aplicado ao ponto de chegada do proximo segmento.
    protected abstract Vector3 ComputeLateralOffset((int X, int Z) from, (int X, int Z) to);
}
