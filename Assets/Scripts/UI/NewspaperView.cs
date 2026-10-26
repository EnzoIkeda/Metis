using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Jornal da cidade: abre pelo botao do HUD, mostra a edicao mais recente e deixa folhear as anteriores.
// O selo do botao acende com edicao nova e pulsa enquanto houver manchete forte sem ler.
public class NewspaperView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private NewsRoom _newsRoom;
    [SerializeField] private ParameterDisplayData _displayData;

    // Abre o "Por que mudou?" do parametro da manchete, so na edicao mais recente.
    [SerializeField] private ChangeBreakdownPopupView _breakdownPopup;

    [SerializeField] private Button _openButton;
    [SerializeField] private RectTransform _badge;
    [SerializeField] private float _pulseSpeed = 6f;
    [SerializeField] private float _pulseScale = 0.3f;

    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _backdropButton;
    [SerializeField] private TMP_Text _mastheadText;
    [SerializeField] private TMP_Text _sloganText;
    [SerializeField] private TMP_Text _editionText;
    [SerializeField] private TMP_Text _moodText;
    [SerializeField] private TMP_Text _headlineText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private TMP_Text _notesText;

    // Linha entre a manchete e as notas, escondida quando a edicao nao tem nota.
    [SerializeField] private GameObject _notesDivider;
    [SerializeField] private Button _whyButton;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _closeButton;

    private int _shownIndex = -1;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    public event Action Opened;

    private void Start()
    {
        if (_openButton != null)
            _openButton.onClick.AddListener(Show);
        if (_backdropButton != null)
            _backdropButton.onClick.AddListener(Hide);
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);
        if (_previousButton != null)
            _previousButton.onClick.AddListener(() => ShowEdition(_shownIndex - 1));
        if (_nextButton != null)
            _nextButton.onClick.AddListener(() => ShowEdition(_shownIndex + 1));
        if (_whyButton != null)
            _whyButton.onClick.AddListener(OpenBreakdown);
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;

        Hide();
    }

    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private NewsArchive Archive => _newsRoom != null ? _newsRoom.Archive : null;

    // Nao abre por cima do popup de evento nem depois que a fase acabou.
    public bool CanOpen
    {
        get
        {
            var machine = _turnManager != null ? _turnManager.Machine : null;
            return machine != null && machine.Outcome == GameOutcome.None && machine.CurrentPhase != TurnPhase.Event;
        }
    }

    private void Update()
    {
        if (_openButton != null)
            _openButton.interactable = CanOpen;
        if (IsOpen && CanOpen == false)
            Hide();

        var archive = Archive;
        if (_badge == null)
            return;
        var unread = archive != null && archive.HasUnread;
        if (_badge.gameObject.activeSelf != unread)
            _badge.gameObject.SetActive(unread);
        var pulse = unread && archive.HasStrongUnread ? 1f + _pulseScale * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _pulseSpeed)) : 1f;
        _badge.localScale = new Vector3(pulse, pulse, 1f);
    }

    public void Show()
    {
        var archive = Archive;
        if (CanOpen == false || archive == null)
            return;

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        ShowEdition(archive.Editions.Count - 1);
        archive.MarkAllRead();
        Opened?.Invoke();
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void HandleLanguageChanged()
    {
        if (IsOpen)
            ShowEdition(_shownIndex);
    }

    private void ShowEdition(int index)
    {
        var archive = Archive;
        var count = archive != null ? archive.Editions.Count : 0;
        _shownIndex = count == 0 ? -1 : Mathf.Clamp(index, 0, count - 1);
        var edition = _shownIndex >= 0 ? archive.Editions[_shownIndex] : null;

        SetText(_mastheadText, UIStrings.NewsMasthead);
        SetText(_sloganText, UIStrings.NewsSlogan);
        SetButtonLabel(_previousButton, UIStrings.NewsPreviousButton);
        SetButtonLabel(_nextButton, UIStrings.NewsNextButton);
        SetButtonLabel(_closeButton, UIStrings.NewsCloseButton);
        SetButtonLabel(_whyButton, UIStrings.NewsWhyButton);
        if (_previousButton != null)
            _previousButton.interactable = _shownIndex > 0;
        if (_nextButton != null)
            _nextButton.interactable = _shownIndex >= 0 && _shownIndex < count - 1;

        if (edition == null || edition.Items.Count == 0)
        {
            SetText(_editionText, edition != null ? UIStrings.NewsEditionLabel(edition.TurnIndex) : string.Empty);
            SetText(_moodText, string.Empty);
            SetText(_headlineText, UIStrings.NewsEmpty);
            SetText(_bodyText, string.Empty);
            SetText(_notesText, string.Empty);
            if (_notesDivider != null)
                _notesDivider.SetActive(false);
            if (_whyButton != null)
                _whyButton.gameObject.SetActive(false);
            return;
        }

        SetText(_editionText, UIStrings.NewsEditionLabel(edition.TurnIndex));
        SetText(_moodText, UIStrings.NewsMood(edition.Wellbeing));

        var headline = edition.Items[0];
        var story = FindStory(headline.StoryId);
        SetText(_headlineText, story != null ? Fill(story.Headline, headline, edition.TurnIndex) : string.Empty);
        var body = new StringBuilder(story != null ? Fill(story.Body, headline, edition.TurnIndex) : string.Empty);
        if (headline.HasCause)
            body.Append("\n<i>").Append(UIStrings.NewsCause(headline.CauseSource, headline.CauseFrom, headline.Delta > 0f)).Append("</i>");
        SetText(_bodyText, body.ToString());

        var notes = new StringBuilder();
        for (var i = 1; i < edition.Items.Count; i++)
        {
            var item = edition.Items[i];
            var note = FindStory(item.StoryId);
            if (note == null)
                continue;
            if (notes.Length > 0)
                notes.Append("\n\n");
            notes.Append("<b>").Append(Fill(note.Headline, item, edition.TurnIndex)).Append("</b>\n");
            notes.Append(Fill(note.Body, item, edition.TurnIndex));
        }
        SetText(_notesText, notes.ToString());
        if (_notesDivider != null)
            _notesDivider.SetActive(notes.Length > 0);

        if (_whyButton != null)
            _whyButton.gameObject.SetActive(CanExplain(edition, headline));
    }

    // O "Por que mudou?" so conhece o ultimo turno.
    private bool CanExplain(NewsEdition edition, NewsItem headline)
    {
        var breakdown = _turnManager != null ? _turnManager.LastBreakdown : null;
        return _breakdownPopup != null
            && NewsRules.UsesParameter(headline.Trigger)
            && breakdown != null
            && breakdown.TurnIndex == edition.TurnIndex
            && _shownIndex == Archive.Editions.Count - 1;
    }

    private void OpenBreakdown()
    {
        var edition = _shownIndex >= 0 ? Archive.Editions[_shownIndex] : null;
        if (edition == null || edition.Items.Count == 0)
            return;
        Hide();
        _breakdownPopup.Show(edition.Items[0].Parameter);
    }

    private NewsStoryData FindStory(string id)
    {
        return _newsRoom != null && _newsRoom.Library != null ? _newsRoom.Library.Find(id) : null;
    }

    private string Fill(string text, NewsItem item, int turnIndex)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        var value = _displayData != null ? _displayData.FormatValue(item.Parameter, item.Value, smallUnit: true) : item.Value.ToString("0.0");
        var change = _displayData != null
            ? _displayData.FormatAmount(item.Parameter, Math.Abs(item.Delta), smallUnit: true, extraDecimals: 1)
            : Math.Abs(item.Delta).ToString("0.0");
        return text
            .Replace("{valor}", value)
            .Replace("{variacao}", change)
            .Replace("{turno}", turnIndex.ToString())
            .Replace("{parametro}", item.Parameter.GetFullName());
    }

    private static void SetText(TMP_Text target, string text)
    {
        if (target != null)
            target.text = text;
    }

    private static void SetButtonLabel(Button button, string text)
    {
        if (button == null)
            return;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = text;
    }
}
