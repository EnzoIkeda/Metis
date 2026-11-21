using System.Collections.Generic;
using UnityEngine;

// Fim de rodada depois do relatorio de gestao: derrota mostra as telas da cidade em ruinas e vitoria
// contra o chefe as telas dos sonhos realizados, cada uma com musica propria; depois vem o resumo.
// Vitoria de fase e cenario seguem com o popup de desfecho de sempre.
public class RunEndFlow : MonoBehaviour
{
    private const float SlideDuration = 4f;
    private const float LastSlideDuration = 5.5f;

    // Telas reaproveitadas da abertura, contadas a partir de 0.
    private static readonly int[] DefeatSlides = { 0, 1 };
    private static readonly int[] VictorySlides = { 5, 6, 7, 8, 9 };

    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private PhaseEndFlow _endFlow;
    [SerializeField] private GameObject _sequenceRoot;
    [SerializeField] private SlideSequenceView _sequence;
    [SerializeField] private RunSummaryPopupView _summary;

    private RunSummary _pending;

    public bool IsPlayingSequence => _sequenceRoot != null && _sequenceRoot.activeSelf;

    public static List<SlideStep> Steps(RunEnding ending)
    {
        var slides = ending == RunEnding.Defeat ? DefeatSlides : VictorySlides;
        var steps = new List<SlideStep>();
        for (var i = 0; i < slides.Length; i++)
            steps.Add(new SlideStep(slides[i], UIStrings.RunEndCaptionKey(ending, slides[i]), i == slides.Length - 1 ? LastSlideDuration : SlideDuration));
        return steps;
    }

    // O popup de desfecho consulta aqui para nao abrir por cima do fim de rodada.
    public bool Handles(GameOutcome outcome)
    {
        return _turnManager != null && _turnManager.EndsRun(outcome);
    }

    private void Start()
    {
        if (_sequenceRoot != null)
            _sequenceRoot.SetActive(false);
        if (_endFlow != null)
            _endFlow.OnContinue += HandleContinue;
    }

    private void OnDestroy()
    {
        if (_endFlow != null)
            _endFlow.OnContinue -= HandleContinue;
        if (_sequence != null)
            _sequence.Finished -= ShowSummary;
    }

    private void HandleContinue(GameOutcome outcome)
    {
        if (Handles(outcome) == false || _turnManager.EndedRun == null)
            return;

        _pending = _turnManager.EndedRun;
        var ending = _pending.Ending;
        var player = MusicPlayer.Instance;
        if (player != null && player.Themes != null)
            player.PlaySequence(ending == RunEnding.Defeat ? player.Themes.Defeat : player.Themes.Victory, false);

        if (_sequence == null || _sequenceRoot == null)
        {
            ShowSummary();
            return;
        }
        _sequenceRoot.SetActive(true);
        _sequence.Finished += ShowSummary;
        var archetype = _pending.Archetype;
        _sequence.Play(Steps(ending), key => UIStrings.SlideCaption(key, archetype));
    }

    private void ShowSummary()
    {
        if (_sequence != null)
            _sequence.Finished -= ShowSummary;
        if (_sequenceRoot != null)
            _sequenceRoot.SetActive(false);
        if (_summary != null && _pending != null)
            _summary.Show(_pending);
    }
}
