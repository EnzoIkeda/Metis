using System;
using System.Collections.Generic;

// Um turno da fase como aconteceu, guardado pro relatorio de fim de fase.
// Valores sao listas na ordem do enum de parametros, pra entrar no save sem conversao.
[Serializable]
public class PhaseTurnRecord
{
    public int TurnIndex;

    // Carta comum jogada; vazio quando o turno passou sem carta.
    public string CardId = string.Empty;
    public List<string> FreeActionIds = new List<string>();

    // Cartas comuns que podiam ser jogadas, e o estado logo antes da jogada, pra comparar com o que nao foi jogado.
    public List<string> PlayableCardIds = new List<string>();
    public bool HasPlayState;
    public float[] ValuesBeforePlay = Array.Empty<float>();
    public float AnchorBonusBeforePlay;

    // Estado no comeco da resolucao, com a carta ja aplicada.
    public bool HasResolution;
    public float[] ValuesAfterPlay = Array.Empty<float>();

    // Parametros em Colapso quando a resolucao comecou, os que cobraram a penalidade do Bem-estar neste turno.
    public List<CityParameterType> CollapsedAtResolution = new List<CityParameterType>();
    public float CollapsePenalty;

    public string EventId = string.Empty;

    // Estado no fim do turno, depois do evento.
    public float[] EndValues = Array.Empty<float>();

    public bool PlayedCard => string.IsNullOrEmpty(CardId) == false;

    public float GetBeforePlay(CityParameterType parameter) => PhaseHistory.Read(ValuesBeforePlay, parameter);

    public float GetAfterPlay(CityParameterType parameter) => PhaseHistory.Read(ValuesAfterPlay, parameter);

    public float GetEnd(CityParameterType parameter) => PhaseHistory.Read(EndValues, parameter);

    // O que a carta comum (custo incluido) mudou no parametro, antes das interacoes do turno.
    public float GetCardEffect(CityParameterType parameter)
    {
        if (HasPlayState == false || HasResolution == false)
            return 0f;
        return GetAfterPlay(parameter) - GetBeforePlay(parameter);
    }
}

// Historico da fase, turno a turno, so lendo o estado: nenhuma conta muda com ou sem ele.
public class PhaseHistory
{
    public static readonly CityParameterType[] Parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));

    private readonly List<PhaseTurnRecord> _turns = new List<PhaseTurnRecord>();

    public float[] StartValues { get; private set; } = Array.Empty<float>();

    public IReadOnlyList<PhaseTurnRecord> Turns => _turns;

    // Turno em andamento, ainda sem o fim registrado.
    public PhaseTurnRecord Current { get; private set; }

    public PhaseHistory()
    {
    }

    // Historico retomado de um save.
    public PhaseHistory(float[] startValues, IEnumerable<PhaseTurnRecord> turns, PhaseTurnRecord current)
    {
        StartValues = startValues ?? Array.Empty<float>();
        if (turns != null)
            _turns.AddRange(turns);
        Current = current;
    }

    public static float[] Snapshot(CityStats stats)
    {
        var values = new float[Parameters.Length];
        for (var i = 0; i < Parameters.Length; i++)
            values[i] = stats.GetValue(Parameters[i]);
        return values;
    }

    public static float Read(float[] values, CityParameterType parameter)
    {
        var index = (int)parameter;
        return values != null && index >= 0 && index < values.Length ? values[index] : 0f;
    }

    public void CaptureStart(CityStats stats)
    {
        StartValues = Snapshot(stats);
    }

    // Abre o registro do turno; continuar a rodada no meio dele mantem o que ja foi registrado.
    public void BeginTurn(int turnIndex)
    {
        if (Current != null && Current.TurnIndex == turnIndex)
            return;
        Current = new PhaseTurnRecord { TurnIndex = turnIndex };
    }

    public void RecordFreeAction(int turnIndex, string cardId)
    {
        BeginTurn(turnIndex);
        Current.FreeActionIds.Add(cardId);
    }

    // Chamado logo antes da carta comum ser aplicada; cardId vazio quando o turno passa sem carta.
    public void RecordPlay(int turnIndex, string cardId, CityStats stats, IEnumerable<string> playableCardIds)
    {
        BeginTurn(turnIndex);
        Current.CardId = cardId ?? string.Empty;
        Current.HasPlayState = true;
        Current.ValuesBeforePlay = Snapshot(stats);
        Current.AnchorBonusBeforePlay = stats.AnchorBonus;
        Current.PlayableCardIds.Clear();
        if (playableCardIds == null)
            return;
        foreach (var id in playableCardIds)
        {
            if (Current.PlayableCardIds.Contains(id) == false)
                Current.PlayableCardIds.Add(id);
        }
    }

    // Chamado antes da resolucao, quando a penalidade de Colapso do turno ja esta decidida.
    public void RecordResolutionStart(int turnIndex, CityStats stats)
    {
        BeginTurn(turnIndex);
        Current.HasResolution = true;
        Current.ValuesAfterPlay = Snapshot(stats);
        Current.CollapsedAtResolution.Clear();
        foreach (var parameter in Parameters)
        {
            if (stats.IsInCollapse(parameter))
                Current.CollapsedAtResolution.Add(parameter);
        }
        Current.CollapsePenalty = stats.CollapsePenaltyPerParameter * Current.CollapsedAtResolution.Count;
    }

    public void RecordEvent(int turnIndex, string eventId)
    {
        BeginTurn(turnIndex);
        Current.EventId = eventId ?? string.Empty;
    }

    public void FinishTurn(int turnIndex, CityStats stats)
    {
        BeginTurn(turnIndex);
        Current.EndValues = Snapshot(stats);
        _turns.Add(Current);
        Current = null;
    }
}
