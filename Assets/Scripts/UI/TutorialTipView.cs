using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Balao de dica nao modal, ao lado do elemento citado. So aparece na fase de acao, sem popup aberto,
// e nao muda nenhum numero do jogo.
public class TutorialTipView : MonoBehaviour
{
    [Serializable]
    public struct TipBinding
    {
        public TutorialTip Tip;
        public RectTransform Anchor;

        // Ficha do "Saiba mais"; sem ficha o botao some.
        public KnowledgeEntryData Entry;
    }

    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private KnowledgePopupView _knowledgePopup;

    [SerializeField] private RectTransform _balloon;
    [SerializeField] private RectTransform _arrow;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _okButton;
    [SerializeField] private Button _learnMoreButton;

    [SerializeField] private TipBinding[] _bindings = Array.Empty<TipBinding>();

    [Tooltip("Enquanto um destes estiver aberto nenhuma dica aparece.")]
    [SerializeField] private GameObject[] _blockingPanels = Array.Empty<GameObject>();

    [Tooltip("Altura do balao fora a mensagem: titulo, botoes e margens.")]
    [SerializeField, Min(0f)] private float _chromeHeight = 230f;
    [SerializeField, Min(0f)] private float _anchorGap = 36f;
    [SerializeField, Min(0f)] private float _screenMargin = 24f;

    [Tooltip("Intervalo (real) entre as consultas ao estado da fase.")]
    [SerializeField, Min(0.01f)] private float _pollSeconds = 0.25f;

    private System.Collections.Generic.Dictionary<CardTier, float> _tierGates;
    private TurnMachine _machine;
    private bool _eventHappened;
    private float _nextPoll;
    private TipBinding _currentBinding;

    public TutorialTip Current { get; private set; } = TutorialTip.None;

    // Roda depois do gerenciador de turno, que cria a maquina no proprio Start.
    private void Start()
    {
        if (_okButton != null)
            _okButton.onClick.AddListener(HandleOkClicked);
        if (_learnMoreButton != null)
            _learnMoreButton.onClick.AddListener(HandleLearnMoreClicked);
        if (_turnManager != null)
        {
            _tierGates = AlmanacTriggers.TierGates(_turnManager.CardPool);
            _machine = _turnManager.Machine;
            _turnManager.OnRandomEventTriggered += HandleEventTriggered;
            _turnManager.OnCardPlayed += HandleCardPlayed;
        }
        LocalizationManager.OnLanguageChanged += RefreshTexts;
        Show(TutorialTip.None);
    }

    private void OnDestroy()
    {
        if (_okButton != null)
            _okButton.onClick.RemoveListener(HandleOkClicked);
        if (_learnMoreButton != null)
            _learnMoreButton.onClick.RemoveListener(HandleLearnMoreClicked);
        if (_turnManager != null)
        {
            _turnManager.OnRandomEventTriggered -= HandleEventTriggered;
            _turnManager.OnCardPlayed -= HandleCardPlayed;
        }
        LocalizationManager.OnLanguageChanged -= RefreshTexts;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextPoll)
            return;
        _nextPoll = Time.unscaledTime + _pollSeconds;

        var next = NextTip();
        if (next != Current)
            Show(next);
        else if (Current != TutorialTip.None)
            Place();
    }

    private TutorialTip NextTip()
    {
        if (_machine == null || IsBlocked())
            return TutorialTip.None;

        var situation = new TutorialSituation
        {
            IsActionPhase = _machine.CurrentPhase == TurnPhase.Action,
            Turn = _machine.TurnIndex,
            CanPass = _turnManager.CanPassTurn,
            EventHappened = _eventHappened,
        };
        TutorialRules.ReadStats(ref situation, _cityStatsManager != null ? _cityStatsManager.Stats : null, _tierGates);
        return TutorialRules.Next(situation, TutorialProgress.WasSeen);
    }

    private bool IsBlocked()
    {
        foreach (var panel in _blockingPanels)
        {
            if (panel != null && panel.activeInHierarchy)
                return true;
        }
        return false;
    }

    private void Show(TutorialTip tip)
    {
        Current = tip;
        _currentBinding = default;
        foreach (var binding in _bindings)
        {
            if (binding.Tip == tip)
                _currentBinding = binding;
        }

        if (_balloon != null)
            _balloon.gameObject.SetActive(tip != TutorialTip.None);
        if (tip == TutorialTip.None)
            return;

        if (_learnMoreButton != null)
            _learnMoreButton.gameObject.SetActive(_currentBinding.Entry != null);
        RefreshTexts();
    }

    private void RefreshTexts()
    {
        if (Current == TutorialTip.None)
            return;

        if (_titleText != null)
            _titleText.text = UIStrings.TutorialTitle(Current);
        if (_messageText != null)
            _messageText.text = UIStrings.TutorialMessage(Current);
        SetLabel(_okButton, UIStrings.TutorialOkButton);
        SetLabel(_learnMoreButton, UIStrings.KnowledgeButton);
        Place();
    }

    private static void SetLabel(Button button, string text)
    {
        if (button == null)
            return;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = text;
    }

    // A altura acompanha o texto; o balao fica abaixo de quem esta na metade de cima e acima de quem esta na de baixo.
    private void Place()
    {
        if (_balloon == null || _messageText == null)
            return;

        var messageHeight = _messageText.GetPreferredValues(_messageText.text, _balloon.rect.width - MessageSideMargins(), 0f).y;
        _balloon.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, messageHeight + _chromeHeight);

        var parent = _balloon.parent as RectTransform;
        var anchor = _currentBinding.Anchor;
        if (_arrow != null)
            _arrow.gameObject.SetActive(anchor != null);
        if (parent == null)
            return;
        if (anchor == null)
        {
            _balloon.localPosition = parent.rect.center;
            return;
        }

        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        var bottomLeft = parent.InverseTransformPoint(corners[0]);
        var topRight = parent.InverseTransformPoint(corners[2]);
        var parentRect = parent.rect;
        var halfHeight = _balloon.rect.height * 0.5f;

        var below = (bottomLeft.y + topRight.y) * 0.5f > parentRect.center.y;
        var y = below ? bottomLeft.y - _anchorGap - halfHeight : topRight.y + _anchorGap + halfHeight;
        y = Mathf.Clamp(y, parentRect.yMin + _screenMargin + halfHeight, parentRect.yMax - _screenMargin - halfHeight);
        var x = parentRect.center.x;
        _balloon.localPosition = new Vector3(x, y, 0f);

        if (_arrow != null)
        {
            var halfWidth = _balloon.rect.width * 0.5f - _anchorGap;
            var arrowX = Mathf.Clamp((bottomLeft.x + topRight.x) * 0.5f - x, -halfWidth, halfWidth);
            _arrow.localPosition = new Vector3(arrowX, below ? halfHeight : -halfHeight, 0f);
        }
    }

    private float MessageSideMargins()
    {
        var rect = _messageText.rectTransform;
        return rect.offsetMin.x - rect.offsetMax.x;
    }

    private void HandleEventTriggered(RandomEventData triggeredEvent)
    {
        if (triggeredEvent != null)
            _eventHappened = true;
    }

    // Quem ja jogou uma carta nao precisa mais da dica de como jogar.
    private void HandleCardPlayed(CardData card)
    {
        TutorialProgress.MarkSeen(TutorialTip.Cards);
    }

    private void HandleOkClicked()
    {
        Dismiss();
    }

    private void HandleLearnMoreClicked()
    {
        var entry = _currentBinding.Entry;
        Dismiss();
        if (entry == null || _knowledgePopup == null)
            return;
        AlmanacProgress.Unlock(entry);
        _knowledgePopup.Show(entry);
    }

    private void Dismiss()
    {
        TutorialProgress.MarkSeen(Current);
        Show(TutorialTip.None);
        _nextPoll = 0f;
    }
}
