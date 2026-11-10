using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup do evento aleatorio; num dilema, os botoes de opcao ficam no lugar do fechar.
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

    [Header("Dilema")]
    [SerializeField] private VerticalLayoutGroup _popupLayout;
    [SerializeField] private GameObject _choicesRoot;
    [SerializeField] private Button _choiceTemplate;

    [Tooltip("Sem o botao de fechar, o rodape nao precisa da folga dele.")]
    [SerializeField] private int _choiceBottomPadding = 45;
    [SerializeField] private float _choiceTextWidth = 770f;
    [SerializeField] private float _choiceMinHeight = 110f;
    [SerializeField] private float _choiceVerticalMargin = 36f;

    private readonly List<Button> _choiceButtons = new List<Button>();
    private KnowledgeEntryData _shownEntry;
    private int _defaultBottomPadding;

    private void Start()
    {
        _turnManager.OnRandomEventTriggered += HandleEventTriggered;
        if (_closeButton != null)
            _closeButton.onClick.AddListener(HandleCloseClicked);
        if (_knowledgeButton != null)
            _knowledgeButton.onClick.AddListener(OpenKnowledge);
        if (_popupLayout != null)
            _defaultBottomPadding = _popupLayout.padding.bottom;
        if (_choiceTemplate != null)
            _choiceTemplate.gameObject.SetActive(false);

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
            _descriptionText.text = Description(triggeredEvent);
        _shownEntry = triggeredEvent.KnowledgeEntry;
        KnowledgeButtonRules.Refresh(_knowledgeButton, _knowledgePopup, _shownEntry);
        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        // Depois de abrir, porque texto em objeto inativo nao mede a altura certa.
        RefreshChoices(triggeredEvent);
        SfxPlayer.Play(SoundEffect.EventPopup);
    }

    // A consequencia lembra qual decisao levou a ela.
    private string Description(RandomEventData triggeredEvent)
    {
        if (triggeredEvent.IsFollowUpOnly == false || _turnManager.History == null)
            return triggeredEvent.Description;

        var origin = DilemmaLog.OriginOf(_turnManager.History, triggeredEvent.name, _turnManager.FindEvent);
        if (origin.HasValue == false)
            return triggeredEvent.Description;

        var label = _turnManager.FindEvent(origin.Value.EventId).ChoiceData[origin.Value.ChoiceIndex].DisplayLabel;
        return $"<i>{UIStrings.EventFollowUpOrigin(origin.Value.Turn, label)}</i>\n\n{triggeredEvent.Description}";
    }

    private void RefreshChoices(RandomEventData triggeredEvent)
    {
        var choices = triggeredEvent.ChoiceData;
        var isDilemma = choices.Count > 0;
        if (_closeButton != null)
            _closeButton.gameObject.SetActive(isDilemma == false);
        if (_popupLayout != null)
            _popupLayout.padding.bottom = isDilemma ? _choiceBottomPadding : _defaultBottomPadding;
        if (_choicesRoot != null)
            _choicesRoot.SetActive(isDilemma);

        for (var i = 0; i < choices.Count; i++)
            BindChoice(ChoiceButton(i), choices[i]);
        for (var i = choices.Count; i < _choiceButtons.Count; i++)
            _choiceButtons[i].gameObject.SetActive(false);
    }

    // A altura vem do texto medido na largura fixa, porque quebra de linha dentro de grupo de layout cria ciclo.
    private void BindChoice(Button button, EventChoiceData choice)
    {
        button.gameObject.SetActive(true);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        label.text = ChoiceText(choice);
        var element = button.GetComponent<LayoutElement>();
        if (element != null)
            element.preferredHeight = Mathf.Max(_choiceMinHeight, label.GetPreferredValues(label.text, _choiceTextWidth, Mathf.Infinity).y + _choiceVerticalMargin);
    }

    private static string ChoiceText(EventChoiceData choice)
    {
        var effects = UIStrings.EventChoiceEffects(choice.Rule.Effects);
        return string.IsNullOrEmpty(choice.DisplayDetail)
            ? $"<b>{choice.DisplayLabel}</b>\n{effects}"
            : $"<b>{choice.DisplayLabel}</b>\n<size=85%>{choice.DisplayDetail}</size>\n{effects}";
    }

    private Button ChoiceButton(int index)
    {
        while (_choiceButtons.Count <= index)
        {
            var created = Instantiate(_choiceTemplate, _choiceTemplate.transform.parent);
            var choiceIndex = _choiceButtons.Count;
            created.onClick.AddListener(() => HandleChoiceClicked(choiceIndex));
            _choiceButtons.Add(created);
        }
        return _choiceButtons[index];
    }

    private void HandleChoiceClicked(int choiceIndex)
    {
        if (_turnManager.ChooseEventOption(choiceIndex) == false)
            return;

        if (_knowledgePopup != null)
            _knowledgePopup.Hide();
        Hide();
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
