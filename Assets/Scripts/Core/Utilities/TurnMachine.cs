using System;

/// <summary>
/// As 6 fases do turno, na ordem em que acontecem.
/// </summary>
public enum TurnPhase
{
    StartOfTurn,
    Action,
    Resolution,
    Event,
    EndCheck,
    Advance
}

public enum GameOutcome
{
    None,
    Victory,
    GameOver
}

/// <summary>
/// Loop de turno do design desejado, implementado como uma State machine explicita, 
/// TurnManager e o wrapper que a conecta na cena.
///
/// TurnMachine em si nao ve as cartas ou eventos e notifica via
/// Observer, deixando TurnManager controlar a compra de mao, disparar eventos e descartar. 
/// Duas paradas que esperam chamada externa: a fase de acao e a fase de evento
/// </summary>
public class TurnMachine
{
    public const int VictoryTurnCount = 20;

    private readonly CityStats _cityStats;

    public int TurnIndex { get; private set; }
    public TurnPhase CurrentPhase { get; private set; }
    public GameOutcome Outcome { get; private set; }

    public event Action<TurnPhase> OnPhaseChanged;
    public event Action<int> OnTurnAdvanced;
    public event Action<GameOutcome> OnGameEnded;

    public TurnMachine(CityStats cityStats)
    {
        _cityStats = cityStats;
    }

    public void StartGame()
    {
        TurnIndex = 1;
        Outcome = GameOutcome.None;
        BeginTurn();
    }

    private void BeginTurn()
    {
        SetPhase(TurnPhase.StartOfTurn);
        // TurnManager reage a esta fase para comprar a mao

        SetPhase(TurnPhase.Action);
        // Espera ser chamado externamente.
    }

    public void EndActionPhase()
    {
        if (Outcome != GameOutcome.None || CurrentPhase != TurnPhase.Action)
            return;

        SetPhase(TurnPhase.Resolution);
        _cityStats.RecomputeDerivedParameters();

        SetPhase(TurnPhase.Event);
        // TurnManager reage a este phase pra sortear o evento e
        // decide na hora se chama AcknowledgeEvent() de volta ou espera
        // o jogador fechar o popup
    }

    /// <summary>
    /// Continua o turno depois da fase de Evento
    /// </summary>
    public void AcknowledgeEvent()
    {
        if (Outcome != GameOutcome.None || CurrentPhase != TurnPhase.Event)
            return;

        SetPhase(TurnPhase.EndCheck);
        if (_cityStats.AnyParameterCritical())
        {
            EndGame(GameOutcome.GameOver);
            return;
        }
        if (TurnIndex >= VictoryTurnCount)
        {
            EndGame(GameOutcome.Victory);
            return;
        }

        SetPhase(TurnPhase.Advance);
        // TurnManager reage a este phase pra descartar a mao
        TurnIndex++;
        OnTurnAdvanced?.Invoke(TurnIndex);

        BeginTurn();
    }

    // debug: vai para a proxima fase
    public void DebugJumpToTurn(int turnIndex)
    {
        if (Outcome != GameOutcome.None)
            return;

        TurnIndex = Math.Max(1, turnIndex);
        BeginTurn();
    }

    private void EndGame(GameOutcome outcome)
    {
        Outcome = outcome;
        OnGameEnded?.Invoke(outcome);
    }

    private void SetPhase(TurnPhase phase)
    {
        CurrentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
    }
}
