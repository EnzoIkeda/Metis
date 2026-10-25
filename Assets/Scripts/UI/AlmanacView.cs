using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Almanaque: grade das fichas por aba, com as ainda bloqueadas em "???" e o contador de descobertas.
public class AlmanacView : MonoBehaviour
{
    private enum Tab
    {
        Cards,
        Events,
        Concepts,
    }

    [SerializeField] private KnowledgeLibraryData _library;
    [SerializeField] private KnowledgePopupView _knowledgePopup;

    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _counterText;
    [SerializeField] private TMP_Text _hintText;
    [SerializeField] private Button _closeButton;

    // Fundo escurecido de tela inteira; tocar nele tambem fecha.
    [SerializeField] private Button _backdropButton;

    // Botoes que abrem o almanaque (menu principal, pausa).
    [SerializeField] private Button[] _openButtons = Array.Empty<Button>();

    [Header("Abas")]
    [SerializeField] private Button _cardsTabButton;
    [SerializeField] private Button _eventsTabButton;
    [SerializeField] private Button _conceptsTabButton;
    [SerializeField] private Color _activeTabColor = Color.white;
    [SerializeField] private Color _inactiveTabColor = new Color(1f, 1f, 1f, 0.45f);

    [Header("Grade")]
    [SerializeField] private RectTransform _gridContainer;
    [SerializeField] private Button _entryTemplate;
    [SerializeField] private ScrollRect _scroll;

    private readonly List<Button> _spawnedEntries = new List<Button>();
    private Tab _tab = Tab.Cards;

    // Aberto antes do proprio Start (por outro componente no mesmo quadro): nao fechar.
    private bool _shown;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    private void Start()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Hide);
            var closeText = _closeButton.GetComponentInChildren<TMP_Text>();
            if (closeText != null)
                closeText.text = UIStrings.BackButton;
        }
        if (_backdropButton != null)
            _backdropButton.onClick.AddListener(Hide);
        foreach (var button in _openButtons)
        {
            if (button != null)
                button.onClick.AddListener(Show);
        }
        RefreshOpenButtonLabels();
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        if (_cardsTabButton != null)
            _cardsTabButton.onClick.AddListener(() => SelectTab(Tab.Cards));
        if (_eventsTabButton != null)
            _eventsTabButton.onClick.AddListener(() => SelectTab(Tab.Events));
        if (_conceptsTabButton != null)
            _conceptsTabButton.onClick.AddListener(() => SelectTab(Tab.Concepts));
        if (_entryTemplate != null)
            _entryTemplate.gameObject.SetActive(false);

        if (_shown == false)
            Hide();
    }

    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    // O idioma pode mudar com o almanaque fechado (configuracoes do menu ou da pausa).
    private void HandleLanguageChanged()
    {
        RefreshOpenButtonLabels();
        if (IsOpen)
            Redraw();
    }

    private void RefreshOpenButtonLabels()
    {
        foreach (var button in _openButtons)
        {
            var label = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
            if (label != null)
                label.text = UIStrings.AlmanacButton;
        }
    }

    public void Show()
    {
        _shown = true;
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        Redraw();
    }

    public void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void SelectTab(Tab tab)
    {
        _tab = tab;
        Redraw();
    }

    private void Redraw()
    {
        if (_titleText != null)
            _titleText.text = UIStrings.AlmanacTitle;
        SetTabLabel(_cardsTabButton, UIStrings.AlmanacTabCards, Tab.Cards);
        SetTabLabel(_eventsTabButton, UIStrings.AlmanacTabEvents, Tab.Events);
        SetTabLabel(_conceptsTabButton, UIStrings.AlmanacTabConcepts, Tab.Concepts);

        var unlocked = 0;
        var total = 0;
        if (_library != null)
        {
            foreach (var entry in _library.Entries)
            {
                if (entry == null)
                    continue;
                total++;
                if (AlmanacProgress.IsUnlocked(entry))
                    unlocked++;
            }
        }
        if (_counterText != null)
            _counterText.text = UIStrings.AlmanacCounter(unlocked, total);
        if (_hintText != null)
        {
            _hintText.text = UIStrings.AlmanacHint;
            _hintText.gameObject.SetActive(unlocked < total);
        }

        RebuildGrid();
    }

    private void SetTabLabel(Button button, string label, Tab tab)
    {
        if (button == null)
            return;
        var text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = label;
        if (button.targetGraphic != null)
            button.targetGraphic.color = _tab == tab ? _activeTabColor : _inactiveTabColor;
    }

    private void RebuildGrid()
    {
        foreach (var spawned in _spawnedEntries)
        {
            if (spawned != null)
                Destroy(spawned.gameObject);
        }
        _spawnedEntries.Clear();

        if (_library == null || _entryTemplate == null || _gridContainer == null)
            return;

        foreach (var entry in _library.Entries)
        {
            if (entry == null || BelongsToTab(entry.Category, _tab) == false)
                continue;

            var isUnlocked = AlmanacProgress.IsUnlocked(entry);
            var button = Instantiate(_entryTemplate, _gridContainer);
            button.gameObject.SetActive(true);
            button.interactable = isUnlocked;
            var label = isUnlocked ? entry.Title : UIStrings.KnowledgeLockedEntry;
            var entryView = button.GetComponent<AlmanacEntryView>();
            if (entryView != null)
            {
                entryView.Bind(label, isUnlocked ? entry.Image : null);
            }
            else
            {
                var text = button.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = label;
            }
            if (isUnlocked)
            {
                var target = entry;
                button.onClick.AddListener(() => OpenEntry(target));
            }
            _spawnedEntries.Add(button);
        }

        if (_scroll != null)
            _scroll.verticalNormalizedPosition = 1f;
    }

    private void OpenEntry(KnowledgeEntryData entry)
    {
        if (_knowledgePopup != null)
            _knowledgePopup.Show(entry);
    }

    // Parametros da cidade ficam junto dos conceitos.
    private static bool BelongsToTab(KnowledgeCategory category, Tab tab)
    {
        switch (tab)
        {
            case Tab.Cards:
                return category == KnowledgeCategory.Card;
            case Tab.Events:
                return category == KnowledgeCategory.Event;
            default:
                return category == KnowledgeCategory.Concept || category == KnowledgeCategory.Parameter;
        }
    }
}
