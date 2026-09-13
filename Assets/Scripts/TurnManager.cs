using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Ponte entre a maquina de turno pura e a cena.
[DefaultExecutionOrder(-100)]
public class TurnManager : MonoBehaviour
{
    private const int HandSize = 5;

    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private PlacementManager _placementManager;
    [SerializeField] private CardData[] _cardPool;
    [SerializeField] private CardData _debugCardToPlay;
    [SerializeField] private RandomEventData[] _eventPool;
    [SerializeField] private PassiveAdvantageData[] _advantagePool;
    [SerializeField] private CityEffectsController _effects;

    private RandomEventPool _events;
    private RandomEventData _pendingEvent;

    public TurnMachine Machine { get; private set; }
    public CardHand Hand { get; private set; }

    // Pool completo de 54 cartas, exposto pro popup de recompensa de fim de fase sortear opcoes.
    public IReadOnlyList<CardData> CardPool => _cardPool;

    // Todas as vantagens passivas cadastradas, exposto pro popup de recompensa sortear opcoes.
    public IReadOnlyList<PassiveAdvantageData> AdvantagePool => _advantagePool;

    public event Action<RandomEventData> OnRandomEventTriggered;

    private void Start()
    {
        ApplyLoadedAdvantages();

        Machine = new TurnMachine(_cityStatsManager.Stats);
        Machine.OnPhaseChanged += HandlePhaseChanged;
        Machine.OnTurnAdvanced += HandleTurnAdvanced;
        Machine.OnGameEnded += HandleGameEnded;

        var pool = DeckBuilder.Build(_cardPool, MetaProgressionManager.Archetype, MetaProgressionManager.LoadedCardNames);
        Hand = new CardHand(pool);
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

    // Aplica de uma vez, no inicio da fase, o efeito de cada vantagem passiva carregada de uma fase anterior.
    private void ApplyLoadedAdvantages()
    {
        foreach (var name in MetaProgressionManager.LoadedAdvantageNames)
        {
            var advantage = _advantagePool.FirstOrDefault(candidate => candidate.name == name);
            if (advantage != null)
                _cityStatsManager.Stats.ApplyModifiers(advantage.StatEffects);
        }
    }

    public bool CanPlay(CardData card)
    {
        return card != null && Hand.CanPlay(card, _cityStatsManager.Stats);
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
        {
            // O ambiente some ja nesse instante, antes mesmo do efeito de impacto.
            _effects?.SetAmbientGlowsVisible(false);

            if (_effects != null)
                _effects.PlayImpactGlowing(() => Machine.EndActionPhase());
            else
                Machine.EndActionPhase();
        }

        return played;
    }

    public void AcknowledgeEvent()
    {
        var closedEvent = _pendingEvent;
        _pendingEvent = null;

        if (closedEvent != null && _effects != null && IsPositiveOrMixed(closedEvent))
            _effects.PlayMagicPoof(() => Machine?.AcknowledgeEvent());
        else if (closedEvent != null && _effects != null && IsNegative(closedEvent))
            _effects.PlayExplosion(() => Machine?.AcknowledgeEvent());
        else
            Machine?.AcknowledgeEvent();
    }

    // Positivo ou misto, quando o evento tem pelo menos um efeito de valor positivo.
    private static bool IsPositiveOrMixed(RandomEventData eventData)
    {
        foreach (var effect in eventData.StatEffects)
        {
            if (effect.Amount > 0f)
                return true;
        }
        return false;
    }

    // Negativo, quando o evento tem pelo menos um efeito de valor negativo e nenhum positivo.
    private static bool IsNegative(RandomEventData eventData)
    {
        foreach (var effect in eventData.StatEffects)
        {
            if (effect.Amount < 0f)
                return true;
        }
        return false;
    }

    private void HandlePhaseChanged(TurnPhase phase)
    {
        Debug.Log($"[TurnMachine] Turno {Machine.TurnIndex}, fase {phase}");

        // Liga de novo no comeco do proximo turno, ja que jogar uma carta esconde na hora.
        _effects?.SetAmbientGlowsVisible(phase == TurnPhase.Action);

        if (phase == TurnPhase.StartOfTurn)
            Hand.Draw(HandSize, _cityStatsManager.Stats);
        else if (phase == TurnPhase.Event)
            HandleEventPhase();
        else if (phase == TurnPhase.Advance)
            Hand.DiscardAll();
    }

    private void HandleEventPhase()
    {
        var triggeredEvent = _events.TryTriggerEvent(_cityStatsManager.Stats, Machine.TurnIndex);
        _pendingEvent = triggeredEvent;
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

    // Debug.
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
