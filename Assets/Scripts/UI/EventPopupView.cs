using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup do evento aleatorio
/// </summary>
public class EventPopupView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Button _closeButton;

    private void Start()
    {
        _turnManager.OnRandomEventTriggered += HandleEventTriggered;
        if (_closeButton != null)
            _closeButton.onClick.AddListener(HandleCloseClicked);

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
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private void HandleCloseClicked()
    {
        Hide();
        _turnManager.AcknowledgeEvent();
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
