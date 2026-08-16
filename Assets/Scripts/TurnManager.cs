using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Wrapper de TurnMachine: um Bridge entre a logica de turno pura e a cena
/// </summary>
[DefaultExecutionOrder(-100)]
public class TurnManager : MonoBehaviour
{
    private const int HandSize = 5;

    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private PlacementManager _placementManager;
    [SerializeField] private CardData[] _cardPool;
    [SerializeField] private CardData _debugCardToPlay;
    [SerializeField] private RandomEventData[] _eventPool;

    private RandomEventPool _events;

    public TurnMachine Machine { get; private set; }
    public CardHand Hand { get; private set; }

    public event Action<RandomEventData> OnRandomEventTriggered;

    private void Start()
    {
        Machine = new TurnMachine(_cityStatsManager.Stats);
        Machine.OnPhaseChanged += HandlePhaseChanged;
        Machine.OnTurnAdvanced += HandleTurnAdvanced;
        Machine.OnGameEnded += HandleGameEnded;

        Hand = new CardHand(_cardPool);
        Hand.OnHandChanged += HandleHandChanged;

        _events = new RandomEventPool(_eventPool);

        Machine.StartGame();
    }

    private void OnDisable()
    {
        if (Machine != null)
        {
            Machine.OnPhaseChanged -= HandlePhaseChanged;
            Machine.OnTurnAdvanced -= HandleTurnAdvanced;
            Machine.OnGameEnded -= HandleGameEnded;
        }

        if (Hand != null)
            Hand.OnHandChanged -= HandleHandChanged;
    }

    public bool PlayCard(CardData card)
    {
        if (Machine == null || Machine.CurrentPhase != TurnPhase.Action)
            return false;
        if (card == null || Hand.CanPlay(card, _cityStatsManager.Stats) == false)
            return false;

        bool played;
        if (card.StructureToPlace == null)
        {
            played = Hand.TryPlay(card, _cityStatsManager.Stats);
        }
        else
        {
            if (_placementManager.TryGetRandomFreePosition(out var position) == false)
                return false;
            if (_placementManager.PlaceStructure(position, card.StructureToPlace) == false)
                return false;

            played = Hand.TryPlay(card, _cityStatsManager.Stats);
        }

        if (played)
            Machine.EndActionPhase();

        return played;
    }

    public void AcknowledgeEvent()
    {
        Machine?.AcknowledgeEvent();
    }

    private void HandlePhaseChanged(TurnPhase phase)
    {
        Debug.Log($"[TurnMachine] Turno {Machine.TurnIndex}, fase {phase}");

        if (phase == TurnPhase.StartOfTurn)
            Hand.Draw(HandSize);
        else if (phase == TurnPhase.Event)
            HandleEventPhase();
        else if (phase == TurnPhase.Advance)
            Hand.DiscardAll();
    }

    private void HandleEventPhase()
    {
        var triggeredEvent = _events.TryTriggerEvent(_cityStatsManager.Stats);
        if (triggeredEvent == null)
        {
            Machine.AcknowledgeEvent();
            return;
        }

        Debug.Log($"[RandomEvent] '{triggeredEvent.Title}': {triggeredEvent.Description}");
        OnRandomEventTriggered?.Invoke(triggeredEvent);
    }

    private void HandleTurnAdvanced(int turnIndex)
    {
        Debug.Log($"[TurnMachine] Avançou para o turno {turnIndex}");
    }

    private void HandleGameEnded(GameOutcome outcome)
    {
        Debug.LogWarning($"[TurnMachine] Fim de jogo: {outcome} (turno {Machine.TurnIndex})");
    }

    private void HandleHandChanged()
    {
        var cardNames = string.Join(", ", Hand.Cards.Select(card => card.CardName));
        Debug.Log($"[CardHand] Mão atual: [{cardNames}]");
    }

    // DEBUG
    [ContextMenu("Debug: Reiniciar Jogo")]
    private void DebugStartGame()
    {
        Machine.StartGame();
    }

    
    [ContextMenu("Debug: Passar Turno (sem jogar carta)")]
    private void DebugEndActionPhase()
    {
        Machine.EndActionPhase();
    }

    [ContextMenu("Debug: Confirmar Evento (fechar popup)")]
    private void DebugAcknowledgeEvent()
    {
        AcknowledgeEvent();
    }

    [ContextMenu("Debug: Pular Para Turno 20")]
    private void DebugJumpToTurnTwenty()
    {
        Hand.DiscardAll();
        Machine.DebugJumpToTurn(TurnMachine.VictoryTurnCount);
    }

    [ContextMenu("Debug: Jogar carta selecionada")]
    private void DebugPlaySelectedCard()
    {
        var success = PlayCard(_debugCardToPlay);
        Debug.Log(success
            ? $"[CardHand] Jogou '{_debugCardToPlay.CardName}'"
            : $"[CardHand] Não foi possível jogar '{_debugCardToPlay?.CardName}' (fase errada, fora da mão, Pesquisa/Renda insuficientes, ou grid cheio?)");
    }
}
