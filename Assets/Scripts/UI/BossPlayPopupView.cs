using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup da carta do chefe: na jogada dele segura o turno ate a confirmacao; pelo HUD so mostra a proxima carta.
public class BossPlayPopupView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private KnowledgePopupView _knowledgePopup;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _cardNameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _effectsText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private TMP_Text _confirmLabel;
    [SerializeField] private Button _learnMoreButton;
    [SerializeField] private TMP_Text _learnMoreLabel;

    // A jogada aberta precisa de confirmacao; a previa so fecha.
    private bool _waitingConfirmation;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    public string EffectsText => _effectsText != null ? _effectsText.text : string.Empty;

    private void Start()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(Confirm);
        if (_learnMoreButton != null)
            _learnMoreButton.onClick.AddListener(OpenKnowledge);
        if (_turnManager != null)
            _turnManager.OnBossCardPlayed += ShowPlayed;

        // A jogada retomada de um save pode ter aberto antes daqui.
        if (_waitingConfirmation == false)
            Hide();
    }

    private void OnDestroy()
    {
        if (_turnManager != null)
            _turnManager.OnBossCardPlayed -= ShowPlayed;
    }

    public void ShowPlayed(BossCardEntry card)
    {
        _waitingConfirmation = true;
        var boss = _turnManager.Boss;

        // O dano ja foi aplicado: o fator e o de antes desta carta.
        var factor = boss != null ? BossRules.DamageFactor(boss.Definition, boss.CardsPlayed - 1) : 1f;
        Fill(card, factor, UIStrings.BossOkButton, PlayedNote(factor));
    }

    public void ShowPreview(BossCardEntry card)
    {
        _waitingConfirmation = false;
        var boss = _turnManager.Boss;
        var factor = boss != null ? boss.DamageFactor : 1f;
        Fill(card, factor, UIStrings.NewsCloseButton, PreviewNote(factor));
    }

    // Depois da jogada: o que a regra especial acabou de fazer.
    private string PlayedNote(float factor)
    {
        var boss = _turnManager.Boss;
        if (boss == null)
            return string.Empty;
        switch (boss.Definition.Mechanic)
        {
            case BossMechanic.GrowingDamage:
                return factor > 1.0001f ? UIStrings.BossGrowingDamage(factor) : UIStrings.BossMechanicRule(BossMechanic.GrowingDamage, 0, string.Empty);
            case BossMechanic.ExtraCards:
                var extra = _turnManager.BossExtraCard;
                return extra != null ? UIStrings.BossExtraCardsAdded(BossRules.ExtraCardCount(boss.Definition), extra.CardName) : string.Empty;
            case BossMechanic.CostIncrease:
                return boss.CardCostOffset > 0 ? UIStrings.BossCostIncreased(boss.CardCostOffset) : UIStrings.BossMechanicRule(BossMechanic.CostIncrease, 0, string.Empty);
            default:
                return UIStrings.BossMechanicRule(boss.Definition.Mechanic, 0, string.Empty);
        }
    }

    // Na previa: a regra especial, com o fator atual no dano crescente.
    private string PreviewNote(float factor)
    {
        var boss = _turnManager.Boss;
        if (boss == null)
            return string.Empty;
        var mechanic = boss.Definition.Mechanic;
        if (mechanic == BossMechanic.GrowingDamage && factor > 1.0001f)
            return UIStrings.BossGrowingDamage(factor);
        var extra = _turnManager.BossExtraCard;
        return UIStrings.BossMechanicRule(mechanic, BossRules.ExtraCardCount(boss.Definition), extra != null ? extra.CardName : string.Empty);
    }

    private void Fill(BossCardEntry card, float factor, string confirmText, string note)
    {
        var definition = _turnManager.BossDefinition;
        if (_titleText != null)
            _titleText.text = UIStrings.BossPlayedTitle(definition != null ? definition.BossName : string.Empty);
        if (_cardNameText != null)
            _cardNameText.text = card.CardName;
        if (_descriptionText != null)
            _descriptionText.text = card.Description;
        if (_effectsText != null)
        {
            var effects = UIStrings.EventChoiceEffects(BossRules.ScaledEffects(definition, card, factor));
            _effectsText.text = string.IsNullOrEmpty(note) ? effects : $"{effects}\n<size=80%>{note}</size>";
        }
        if (_confirmLabel != null)
            _confirmLabel.text = confirmText;
        if (_learnMoreButton != null)
        {
            _learnMoreButton.gameObject.SetActive(_knowledgePopup != null && definition != null && definition.KnowledgeEntry != null);
            if (_learnMoreLabel != null)
                _learnMoreLabel.text = UIStrings.BossLearnMoreButton;
        }
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private void OpenKnowledge()
    {
        var definition = _turnManager.BossDefinition;
        if (_knowledgePopup != null && definition != null && definition.KnowledgeEntry != null)
            _knowledgePopup.Show(definition.KnowledgeEntry);
    }

    private void Confirm()
    {
        var waiting = _waitingConfirmation;
        _waitingConfirmation = false;
        Hide();
        if (waiting)
            _turnManager.AcknowledgeBoss();
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
