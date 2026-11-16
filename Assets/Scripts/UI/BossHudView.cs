using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Painel do chefe no HUD: Impeto, proxima carta e quando ela sai. So existe na fase do chefe.
public class BossHudView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private BossPlayPopupView _popup;
    [SerializeField] private GameObject _root;
    [SerializeField] private Button _openButton;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _momentumText;
    [SerializeField] private Image _momentumFill;
    [SerializeField] private TMP_Text _nextText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _mechanicText;

    public string StatusText => _statusText != null ? _statusText.text : string.Empty;

    public string MechanicText => _mechanicText != null ? _mechanicText.text : string.Empty;

    private void Start()
    {
        var boss = _turnManager != null ? _turnManager.Boss : null;
        if (_root != null)
            _root.SetActive(boss != null);
        if (boss == null)
            return;

        if (_openButton != null)
            _openButton.onClick.AddListener(OpenNext);
        _turnManager.Machine.OnPhaseChanged += HandlePhaseChanged;
        _turnManager.OnBossCardPlayed += HandleBossCardPlayed;
        LocalizationManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= Refresh;
        if (_turnManager == null)
            return;
        _turnManager.OnBossCardPlayed -= HandleBossCardPlayed;
        if (_turnManager.Machine != null)
            _turnManager.Machine.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(TurnPhase phase)
    {
        if (phase == TurnPhase.Action || phase == TurnPhase.Resolution)
            Refresh();
    }

    private void HandleBossCardPlayed(BossCardEntry card)
    {
        Refresh();
    }

    private void OpenNext()
    {
        var next = _turnManager.Boss.Next as BossCardEntry;
        if (_popup != null && next != null)
            _popup.ShowPreview(next);
    }

    public void Refresh()
    {
        var boss = _turnManager.Boss;
        var definition = _turnManager.BossDefinition;
        if (boss == null || definition == null)
            return;

        var next = boss.Next as BossCardEntry;
        var cost = next != null ? next.MomentumCost : 0;
        if (_nameText != null)
            _nameText.text = definition.BossName;
        if (_momentumText != null)
            _momentumText.text = $"{UIStrings.BossMomentumLabel} {UIStrings.BossMomentumValue(boss.Momentum, cost)}";
        if (_momentumFill != null)
            _momentumFill.fillAmount = cost > 0 ? Mathf.Clamp01((float)boss.Momentum / cost) : 0f;
        if (_nextText != null)
            _nextText.text = next != null ? UIStrings.BossNextCard(next.CardName) : string.Empty;
        if (_statusText != null)
        {
            var turns = boss.TurnsUntilNextPlay;
            _statusText.text = turns < 0 ? string.Empty : turns == 1 ? UIStrings.BossPlaysThisTurn : UIStrings.BossCharging(turns - 1);
        }
        if (_mechanicText != null)
        {
            var extra = _turnManager.BossExtraCard;
            var extraCount = extra != null && _turnManager.Hand != null ? _turnManager.Hand.CountInDeck(extra) : 0;
            _mechanicText.text = UIStrings.BossMechanicStatus(definition.Mechanic, boss.DamageFactor, extraCount, boss.CardCostOffset);
        }
    }
}
