using System;

// As 6 fases do turno, na ordem em que acontecem.
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

// Maquina de estados explicita do loop de turno, com duas paradas que esperam chamada externa.
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
        // A mao e comprada em reacao a esta fase.

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
        // O evento e sorteado em reacao a esta fase, e o turno so continua quando o popup fecha.
    }

    // Continua o turno depois da fase de evento.
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
        // A mao e descartada em reacao a esta fase.
        TurnIndex++;
        OnTurnAdvanced?.Invoke(TurnIndex);

        BeginTurn();
    }

    // Debug: pula direto pra um turno especifico.
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
