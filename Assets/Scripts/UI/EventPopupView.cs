using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup do evento aleatorio.
public class EventPopupView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Button _closeButton;

    // Abre a ficha do evento; some quando o evento ainda nao tem ficha.
    [SerializeField] private Button _knowledgeButton;
    [SerializeField] private KnowledgePopupView _knowledgePopup;

    private KnowledgeEntryData _shownEntry;

    private void Start()
    {
        _turnManager.OnRandomEventTriggered += HandleEventTriggered;
        if (_closeButton != null)
            _closeButton.onClick.AddListener(HandleCloseClicked);
        if (_knowledgeButton != null)
            _knowledgeButton.onClick.AddListener(OpenKnowledge);

        Hide();
    }

    private void OnDisable()
    {
        if (_turnManager != null)
            _turnManager.OnRandomEventTriggered -= HandleEventTriggered;
    }

    private void HandleEventTriggered(RandomEventData triggeredEvent)
    {
        if (_titleText != null)
            _titleText.text = triggeredEvent.Title;
        if (_descriptionText != null)
            _descriptionText.text = triggeredEvent.Description;
        _shownEntry = triggeredEvent.KnowledgeEntry;
        KnowledgeButtonRules.Refresh(_knowledgeButton, _knowledgePopup, _shownEntry);
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        SfxPlayer.Play(SoundEffect.EventPopup);
    }

    private void OpenKnowledge()
    {
        if (_knowledgePopup != null && _shownEntry != null)
            _knowledgePopup.Show(_shownEntry);
    }

    private void HandleCloseClicked()
    {
        if (_knowledgePopup != null)
            _knowledgePopup.Hide();
        Hide();
        _turnManager.AcknowledgeEvent();
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
