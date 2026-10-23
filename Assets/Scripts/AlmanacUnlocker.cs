using System.Collections.Generic;
using UnityEngine;

// Desbloqueia fichas do almanaque durante a fase: a da carta ao jogar, a do evento ao acontecer e as de
// conceito nos momentos marcados em cada ficha. So observa: nao muda nenhum numero do jogo.
public class AlmanacUnlocker : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private KnowledgeLibraryData _library;
    [SerializeField] private RelationMapView _relationMap;
    [SerializeField] private ChangeBreakdownPopupView _breakdownPopup;

    private CityStats _stats;
    private TurnMachine _machine;
    private Dictionary<CardTier, float> _tierGates = new Dictionary<CardTier, float>();

    // Roda depois do gerenciador de turno, que cria a maquina e os parametros no proprio Start.
    private void Start()
    {
        if (_turnManager != null)
        {
            _tierGates = AlmanacTriggers.TierGates(_turnManager.CardPool);
            _turnManager.OnCardPlayed += HandleCardPlayed;
            _turnManager.OnRandomEventTriggered += HandleEventTriggered;
            _machine = _turnManager.Machine;
            if (_machine != null)
                _machine.OnGameEnded += HandleGameEnded;
        }
        if (_relationMap != null)
            _relationMap.Opened += HandleRelationMapOpened;
        if (_breakdownPopup != null)
            _breakdownPopup.Opened += HandleBreakdownOpened;

        UnlockTrigger(KnowledgeUnlockTrigger.GameStart);

        _stats = _cityStatsManager != null ? _cityStatsManager.Stats : null;
        if (_stats != null)
        {
            _stats.OnParameterChanged += HandleParameterChanged;
            foreach (CityParameterType parameter in System.Enum.GetValues(typeof(CityParameterType)))
                CheckParameter(parameter);
        }
    }

    private void OnDestroy()
    {
        if (_turnManager != null)
        {
            _turnManager.OnCardPlayed -= HandleCardPlayed;
            _turnManager.OnRandomEventTriggered -= HandleEventTriggered;
        }
        if (_machine != null)
            _machine.OnGameEnded -= HandleGameEnded;
        if (_relationMap != null)
            _relationMap.Opened -= HandleRelationMapOpened;
        if (_breakdownPopup != null)
            _breakdownPopup.Opened -= HandleBreakdownOpened;
        if (_stats != null)
            _stats.OnParameterChanged -= HandleParameterChanged;
    }

    private void HandleCardPlayed(CardData card)
    {
        if (card != null)
            AlmanacProgress.Unlock(card.KnowledgeEntry);
    }

    private void HandleEventTriggered(RandomEventData triggeredEvent)
    {
        if (triggeredEvent != null)
            AlmanacProgress.Unlock(triggeredEvent.KnowledgeEntry);
    }

    private void HandleGameEnded(GameOutcome outcome)
    {
        UnlockTrigger(AlmanacTriggers.ForOutcome(outcome));
    }

    private void HandleRelationMapOpened()
    {
        UnlockTrigger(KnowledgeUnlockTrigger.OpenRelationMap);
    }

    private void HandleBreakdownOpened()
    {
        UnlockTrigger(KnowledgeUnlockTrigger.OpenChangeBreakdown);
    }

    private void HandleParameterChanged(CityParameterType parameter, float value)
    {
        CheckParameter(parameter);
    }

    private void CheckParameter(CityParameterType parameter)
    {
        foreach (var trigger in AlmanacTriggers.ForParameter(_stats, parameter, _tierGates))
            UnlockTrigger(trigger);
    }

    private void UnlockTrigger(KnowledgeUnlockTrigger trigger)
    {
        if (_library == null)
            return;
        foreach (var entry in _library.EntriesFor(trigger))
            AlmanacProgress.Unlock(entry);
    }
}
