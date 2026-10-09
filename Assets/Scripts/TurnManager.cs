using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Ponte entre a maquina de turno pura e a cena.
[DefaultExecutionOrder(-100)]
public class TurnManager : MonoBehaviour
{
    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private PlacementManager _placementManager;
    [SerializeField] private CardData[] _cardPool;
    [SerializeField] private CardData _debugCardToPlay;
    [SerializeField] private RandomEventData[] _eventPool;
    [SerializeField] private PassiveAdvantageData[] _advantagePool;
    [SerializeField] private CityEffectsController _effects;
    [SerializeField] private PhaseBackgroundRandomizer _background;
    [SerializeField, Min(1)] private int _handSize = 5;
    [SerializeField, Min(1)] private int _victoryTurnCount = TurnMachine.DefaultVictoryTurnCount;

    private RandomEventPool<RandomEventData> _events;

    // Geradores com estado salvo, pra compra e evento seguirem a mesma sequencia ao continuar a rodada.
    private SeededRandom _handRandom;
    private SeededRandom _eventRandom;

    // Evento salvo com o popup aberto, mostrado de novo ao retomar em vez de sortear outro.
    private RandomEventData _restoredEvent;

    // A fase veio de um save, e nao comecou do zero.
    public bool ResumedFromSave { get; private set; }
    private RandomEventData _pendingEvent;

    // Guardas de reentrancia contra jogar carta ou confirmar evento duas vezes durante a animacao atrasada.
    private bool _actionEffectPending;
    private bool _eventEffectPending;

    // Carta de busca ja paga, esperando o jogador escolher o que trazer do baralho.
    private bool _searchPending;

    public TurnMachine Machine { get; private set; }
    public CardHand<CardData> Hand { get; private set; }

    // Pool completo de cartas, exposto pro popup de recompensa de fim de fase sortear opcoes.
    public IReadOnlyList<CardData> CardPool => _cardPool;

    // Todas as vantagens passivas cadastradas, exposto pro popup de recompensa sortear opcoes.
    public IReadOnlyList<PassiveAdvantageData> AdvantagePool => _advantagePool;

    public event Action<RandomEventData> OnRandomEventTriggered;

    // Carta de busca jogada: lista o que pode vir do baralho, a escolha volta por CompleteSearch.
    public event Action<IReadOnlyList<CardData>> OnSearchRequested;

    // Roda antes do Awake dos outros componentes da cena (ordem de execucao -100), entao uma carta salva que
    // nao existe mais descarta a fase antes de parametros, cidade e fundo lerem o save.
    private void Awake()
    {
        var savedPhase = MetaProgressionManager.SavedPhase;
        if (savedPhase == null)
            return;

        if (TryRestoreHand(savedPhase) == false || TryRestoreEvent(savedPhase) == false)
        {
            Debug.LogWarning("[TurnManager] Save com carta ou evento desconhecido, a fase recomeça do zero.");
            Hand = null;
            _restoredEvent = null;
            MetaProgressionManager.DiscardSavedPhase();
        }
    }

    private void Start()
    {
        // O save ainda pode ter sido descartado depois do Awake daqui, por um preset que nao existe mais.
        var savedPhase = MetaProgressionManager.SavedPhase;
        if (savedPhase != null && Hand != null)
        {
            ResumedFromSave = true;
            _eventRandom = new SeededRandom(savedPhase.EventRandomState);
        }
        else
        {
            savedPhase = null;
            _restoredEvent = null;
            ApplyLoadedAdvantages();

            var pool = DeckBuilder.Build(_cardPool, MetaProgressionManager.Archetype, MetaProgressionManager.LoadedCardNames);
            _handRandom = new SeededRandom(SeededRandom.NewSeed());
            // Baralho sempre finito: compra sem reposicao e reembaralha o baralho inteiro quando acaba.
            Hand = new CardHand<CardData>(pool, _handRandom, finiteDeck: true);
            _eventRandom = new SeededRandom(SeededRandom.NewSeed());
        }

        Hand.OnHandChanged += HandleHandChanged;
        _events = new RandomEventPool<RandomEventData>(_eventPool, _eventRandom);

        Machine = new TurnMachine(_cityStatsManager.Stats, _victoryTurnCount);
        Machine.OnPhaseChanged += HandlePhaseChanged;
        Machine.OnTurnAdvanced += HandleTurnAdvanced;
        Machine.OnGameEnded += HandleGameEnded;

        if (savedPhase == null)
            Machine.StartGame();
        else if (_restoredEvent != null)
            Machine.ResumeAtEvent(Math.Min(savedPhase.TurnIndex, _victoryTurnCount));
        else
            Machine.ResumeAtAction(Math.Min(savedPhase.TurnIndex, _victoryTurnCount));
    }

    private bool TryRestoreEvent(PhaseSaveData savedPhase)
    {
        _restoredEvent = null;
        if (savedPhase.AtEvent == false)
            return true;

        _restoredEvent = _eventPool.FirstOrDefault(candidate => candidate != null && candidate.name == savedPhase.PendingEventId);
        return _restoredEvent != null;
    }

    private bool TryRestoreHand(PhaseSaveData savedPhase)
    {
        var cardsById = new Dictionary<string, CardData>();
        foreach (var card in _cardPool)
        {
            if (card != null)
                cardsById[card.name] = card;
        }

        var random = new SeededRandom(savedPhase.HandRandomState);
        CardData FindCard(string id) => cardsById.TryGetValue(id, out var card) ? card : null;
        if (RunSaveRules.TryRestoreHand<CardData>(savedPhase, FindCard, random, out var hand) == false)
            return false;

        _handRandom = random;
        Hand = hand;
        return true;
    }

    // Ponto de retomada num momento estavel do turno; com evento, o popup dele abre de novo ao continuar.
    private void SaveCheckpoint(RandomEventData pendingEvent = null)
    {
        var phase = RunSaveRules.CapturePhase(Machine.TurnIndex, _cityStatsManager.Stats, Hand, _handRandom, _eventRandom);
        phase.AtEvent = pendingEvent != null;
        phase.PendingEventId = pendingEvent != null ? pendingEvent.name : string.Empty;
        phase.PresetName = _cityStatsManager.PresetName;
        phase.LayoutSeed = _placementManager != null ? _placementManager.LayoutSeed : 0UL;
        phase.BackgroundIndex = _background != null ? _background.BackgroundIndex : -1;
        MetaProgressionManager.SavePhase(phase);
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
        if (card == null || Hand.CanPlay(card, _cityStatsManager.Stats) == false)
            return false;

        // Busca sem nada pra trazer so gastaria Renda.
        if (card.Ability == CardAbility.SearchDeck && Hand.SearchCandidates(_cityStatsManager.Stats).Count == 0)
            return false;

        return true;
    }

    public bool PlayCard(CardData card)
    {
        if (_actionEffectPending || _searchPending)
            return false;
        if (Machine == null || Machine.CurrentPhase != TurnPhase.Action)
            return false;
        if (CanPlay(card) == false)
            return false;

        if (CardRules.IsFreeAction(card))
            return PlayFreeAction(card);

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
            {
                _actionEffectPending = true;
                _effects.PlayImpactGlowing(() =>
                {
                    _actionEffectPending = false;
                    Machine.EndActionPhase();
                });
            }
            else
            {
                Machine.EndActionPhase();
            }
        }

        return played;
    }

    // So da pra passar a vez quando nenhuma carta comum e jogavel, senao o turno ficaria preso na fase de acao.
    public bool CanPassTurn
    {
        get
        {
            if (Machine == null || Machine.CurrentPhase != TurnPhase.Action || _actionEffectPending || _searchPending)
                return false;

            foreach (var card in Hand.Cards)
            {
                if (CardRules.IsFreeAction(card) == false && CanPlay(card))
                    return false;
            }
            return true;
        }
    }

    // Encerra a fase de acao sem jogar carta comum: o turno segue pra resolucao e evento normalmente.
    public bool PassTurn()
    {
        if (CanPassTurn == false)
            return false;

        _effects?.SetAmbientGlowsVisible(false);
        Machine.EndActionPhase();
        return true;
    }

    // Acao livre: paga o custo e resolve a habilidade, sem encerrar a fase de acao nem tocar o efeito de impacto.
    private bool PlayFreeAction(CardData card)
    {
        var stats = _cityStatsManager.Stats;
        if (Hand.TryPlay(card, stats) == false)
            return false;

        if (card.Ability == CardAbility.RevealHand)
        {
            Hand.Reveal(untilPhaseEnd: true);
            SaveCheckpoint();
        }
        else if (card.Ability == CardAbility.SearchDeck)
        {
            _searchPending = true;
            OnSearchRequested?.Invoke(Hand.SearchCandidates(stats));
        }
        return true;
    }

    // Fecha a busca trazendo a carta escolhida pra mao.
    public bool CompleteSearch(CardData card)
    {
        if (_searchPending == false)
            return false;
        if (Hand.TakeFromDeck(card, _cityStatsManager.Stats) == false)
            return false;

        _searchPending = false;
        SaveCheckpoint();
        return true;
    }

    public void AcknowledgeEvent()
    {
        if (_eventEffectPending)
            return;

        var closedEvent = _pendingEvent;
        _pendingEvent = null;

        var tone = closedEvent != null && _effects != null
            ? CityEffectsController.ClassifyEventTone(closedEvent.StatEffects)
            : EventTone.Neutral;

        if (tone == EventTone.PositiveOrMixed)
        {
            _eventEffectPending = true;
            _effects.PlayMagicPoof(() =>
            {
                _eventEffectPending = false;
                Machine?.AcknowledgeEvent();
            });
        }
        else if (tone == EventTone.Negative)
        {
            _eventEffectPending = true;
            _effects.PlayExplosion(() =>
            {
                _eventEffectPending = false;
                Machine?.AcknowledgeEvent();
            });
        }
        else
        {
            Machine?.AcknowledgeEvent();
        }
    }

    private void HandlePhaseChanged(TurnPhase phase)
    {
        Debug.Log($"[TurnMachine] Turno {Machine.TurnIndex}, fase {phase}");

        // Liga de novo no comeco do proximo turno, ja que jogar uma carta esconde na hora.
        _effects?.SetAmbientGlowsVisible(phase == TurnPhase.Action);

        if (phase == TurnPhase.StartOfTurn)
            Hand.Draw(_handSize, _cityStatsManager.Stats);
        else if (phase == TurnPhase.Action)
            SaveCheckpoint();
        else if (phase == TurnPhase.Event && _restoredEvent != null)
            StartCoroutine(AnnounceRestoredEvent());
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
        SaveCheckpoint(triggeredEvent);
        OnRandomEventTriggered?.Invoke(triggeredEvent);
    }

    // Espera um frame pro popup de evento, que se inscreve no proprio Start, ja estar ouvindo.
    private IEnumerator AnnounceRestoredEvent()
    {
        _pendingEvent = _restoredEvent;
        _restoredEvent = null;
        yield return null;

        Debug.Log($"[RandomEvent] Retomado: '{_pendingEvent.Title}'");
        OnRandomEventTriggered?.Invoke(_pendingEvent);
    }

    private void HandleTurnAdvanced(int turnIndex)
    {
        Debug.Log($"[TurnMachine] Avançou para o turno {turnIndex}");
    }

    private void HandleGameEnded(GameOutcome outcome)
    {
        Debug.LogWarning($"[TurnMachine] Fim de jogo: {outcome} (turno {Machine.TurnIndex})");

        // Derrota apaga o save na hora, senao fechar o app no popup e continuar repetiria o turno perdido.
        if (outcome == GameOutcome.GameOver)
            MetaProgressionManager.ResetRun();
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
        Machine.DebugJumpToTurn(Machine.VictoryTurnCount);
    }

    [ContextMenu("Debug: Jogar carta selecionada")]
    private void DebugPlaySelectedCard()
    {
        var success = PlayCard(_debugCardToPlay);
        Debug.Log(success
            ? $"[CardHand] Jogou '{_debugCardToPlay.CardName}'"
            : $"[CardHand] Não foi possível jogar '{_debugCardToPlay?.CardName}' (fase errada, fora da mão, Pesquisa/Renda insuficientes, ou grid cheio?)");
    }

    [ContextMenu("Debug: Abrir Busca no Baralho (sem custo)")]
    private void DebugOpenSearch()
    {
        _searchPending = true;
        OnSearchRequested?.Invoke(Hand.SearchCandidates(_cityStatsManager.Stats));
    }

    [ContextMenu("Debug: Revelar Mao")]
    private void DebugRevealHand()
    {
        Hand.Reveal(untilPhaseEnd: true);
    }

    [ContextMenu("Debug: Finalizar Jogo com Vitoria")]
    private void DebugForceVictory()
    {
        Machine.DebugForceVictory();
    }

    [ContextMenu("Debug: Finalizar Jogo com Derrota")]
    private void DebugForceGameOver()
    {
        Machine.DebugForceGameOver();
    }

    [ContextMenu("Debug: Subir Pesquisa - Tier Cidade Digital")]
    private void DebugUnlockCidadeDigital()
    {
        DebugSetPesquisaForTier(CardTier.CidadeDigital);
    }

    [ContextMenu("Debug: Subir Pesquisa - Tier Cidade Conectada")]
    private void DebugUnlockCidadeConectada()
    {
        DebugSetPesquisaForTier(CardTier.CidadeConectada);
    }

    [ContextMenu("Debug: Subir Pesquisa - Tier Smart City")]
    private void DebugUnlockSmartCity()
    {
        DebugSetPesquisaForTier(CardTier.SmartCity);
    }

    // Sobe Pesquisa ate o RequiredPesquisa mais baixo do tier, o suficiente pra liberar as cartas dele.
    private void DebugSetPesquisaForTier(CardTier tier)
    {
        var required = _cardPool
            .Where(card => card.Tier == tier)
            .Select(card => card.RequiredPesquisa)
            .DefaultIfEmpty(0f)
            .Min();

        var stats = _cityStatsManager.Stats;
        var delta = required - stats.GetValue(CityParameterType.Pesquisa);
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Pesquisa, Amount = delta });
    }
}
