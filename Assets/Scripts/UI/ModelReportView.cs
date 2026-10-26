using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Relatorio tecnico do modelo: cada aba e uma folha de papel pautada que cabe inteira, com links pras fichas e pro mapa.
public class ModelReportView : MonoBehaviour
{
    // Folga na medida da altura, pra ultima palavra nao quebrar fora da celula por arredondamento.
    private const float MeasureSlack = 6f;

    [SerializeField] private ModelReportData _report;
    [SerializeField] private KnowledgeLibraryData _library;
    [SerializeField] private KnowledgePopupView _knowledgePopup;

    // Fechado enquanto o mapa esta aberto, porque fica por cima dele.
    [SerializeField] private AlmanacView _almanac;

    // So existe na cena da fase; sem ele o link do mapa nao aparece.
    [SerializeField] private RelationMapView _relationMap;

    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _fileText;

    // "Folha 3 de 10", no rodape do papel.
    [SerializeField] private TMP_Text _pageText;
    [SerializeField] private Button _closeButton;

    // Fundo escurecido de tela inteira; tocar nele tambem fecha.
    [SerializeField] private Button _backdropButton;

    // Botoes que abrem o relatorio (almanaque).
    [SerializeField] private Button[] _openButtons = Array.Empty<Button>();

    [Header("Folha")]
    // Area escrita do papel; o tamanho dela define ate onde a letra encolhe.
    [SerializeField] private RectTransform _sheetContent;
    [SerializeField] private ReportCellView _cellTemplate;

    // Pinca e roda do mouse; cada aba abre inteira, sem zoom.
    [SerializeField] private PaperZoomView _zoom;

    [Header("Abas")]
    [SerializeField] private RectTransform _tabContainer;
    [SerializeField] private Button _tabTemplate;

    [Header("Medidas (letra cheia)")]
    [SerializeField] private float _padding = 10f;
    [SerializeField] private float _minRowHeight = 40f;
    [SerializeField] private float _blankRowHeight = 18f;
    [SerializeField] private int _linksPerRow = 3;
    [SerializeField, Range(0.3f, 1f)] private float _minScale = 0.5f;
    [SerializeField] private float _titleFontSize = 44f;
    [SerializeField] private float _textFontSize = 30f;
    [SerializeField] private float _smallFontSize = 24f;
    [SerializeField] private float _ruleWidth = 2f;
    [SerializeField] private float _headerRuleWidth = 5f;

    [Header("Cores do papel")]
    [SerializeField] private Color _paperColor = new Color(0.98f, 0.96f, 0.9f);
    [SerializeField] private Color _ruleColor = new Color(0.68f, 0.8f, 0.86f);
    [SerializeField] private Color _columnRuleColor = new Color(0.62f, 0.75f, 0.66f);
    [SerializeField] private Color _headerRuleColor = new Color(0.22f, 0.42f, 0.33f);
    [SerializeField] private Color _inkColor = new Color(0.13f, 0.16f, 0.27f);
    [SerializeField] private Color _titleInkColor = new Color(0.11f, 0.22f, 0.45f);
    [SerializeField] private Color _softInkColor = new Color(0.4f, 0.42f, 0.48f);
    [SerializeField] private Color _accentColor = new Color(0.75f, 0.2f, 0.2f);
    [SerializeField] private Color _linkColor = new Color(0.16f, 0.36f, 0.75f);
    [SerializeField] private Color _positiveColor = new Color(0.18f, 0.63f, 0.35f);
    [SerializeField] private Color _negativeColor = new Color(0.84f, 0.25f, 0.25f);

    [Tooltip("Uma cor de marca-texto por marca, na ordem: source, magnitude, estimate, design, simulation, crisis, normal, excess, critical.")]
    [SerializeField] private Color[] _tagColors =
    {
        new Color(0.18f, 0.62f, 0.35f),
        new Color(0.79f, 0.54f, 0.07f),
        new Color(0.85f, 0.38f, 0.17f),
        new Color(0.18f, 0.39f, 0.78f),
        new Color(0.48f, 0.31f, 0.79f),
        new Color(0.84f, 0.25f, 0.25f),
        new Color(0.18f, 0.62f, 0.35f),
        new Color(0.79f, 0.54f, 0.07f),
        new Color(0.85f, 0.38f, 0.17f),
    };
    [SerializeField, Range(0f, 1f)] private float _tagFillStrength = 0.25f;
    [SerializeField] private Color _activeTabColor = new Color(0.98f, 0.96f, 0.9f);
    [SerializeField] private Color _inactiveTabColor = new Color(0.84f, 0.76f, 0.6f);

    private readonly List<ReportCellView> _cells = new List<ReportCellView>();
    private readonly List<Button> _tabs = new List<Button>();
    private ReportLayout _layout;
    private int _sheetIndex;
    private bool _shown;
    private bool _almanacWasOpen;
    private bool _waitingForMap;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    public int SheetIndex => _sheetIndex;

    public string SheetId => Sheet?.Id ?? string.Empty;

    public ReportLayout Layout => _layout;

    private ReportSheet Sheet => _report != null && _sheetIndex < _report.Sheets.Count ? _report.Sheets[_sheetIndex] : null;

    private static bool English => LocalizationManager.Current == Language.English;

    private void Start()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);
        if (_backdropButton != null)
            _backdropButton.onClick.AddListener(Hide);
        foreach (var button in _openButtons)
        {
            if (button != null)
                button.onClick.AddListener(Show);
        }
        if (_cellTemplate != null)
            _cellTemplate.gameObject.SetActive(false);
        if (_tabTemplate != null)
            _tabTemplate.gameObject.SetActive(false);
        RefreshLabels();
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;

        if (_shown == false)
            Hide();
    }

    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        if (_relationMap != null)
            _relationMap.Closed -= HandleMapClosed;
    }

    public void Show()
    {
        _shown = true;
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        RefreshLabels();
        RebuildTabs();
        Redraw();
    }

    public void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    public void SelectSheet(int index)
    {
        if (_report == null || index < 0 || index >= _report.Sheets.Count)
            return;
        _sheetIndex = index;
        RefreshTabColors();
        Redraw();
    }

    // Toque numa celula: se for link, abre a ficha ou o mapa.
    public void SelectCell(int index)
    {
        if (_layout == null || index < 0 || index >= _layout.Cells.Count)
            return;
        var cell = _layout.Cells[index];
        if (cell.Style == ReportCellStyle.Link)
            OpenLink(cell.LinkId);
    }

    private void HandleLanguageChanged()
    {
        RefreshLabels();
        if (IsOpen)
        {
            RebuildTabs();
            Redraw();
        }
    }

    private void RefreshLabels()
    {
        if (_titleText != null)
            _titleText.text = UIStrings.ModelReportTitle;
        if (_fileText != null)
            _fileText.text = UIStrings.ModelReportFile(_report != null ? _report.ModelVersion : string.Empty);
        if (_pageText != null)
            _pageText.text = UIStrings.ModelReportPage(_sheetIndex + 1, _report != null ? _report.Sheets.Count : 0);
        var close = _closeButton != null ? _closeButton.GetComponentInChildren<TMP_Text>() : null;
        if (close != null)
            close.text = UIStrings.BackButton;
        foreach (var button in _openButtons)
        {
            var label = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
            if (label != null)
                label.text = UIStrings.ModelReportButton;
        }
    }

    private void RebuildTabs()
    {
        foreach (var tab in _tabs)
        {
            if (tab != null)
                Destroy(tab.gameObject);
        }
        _tabs.Clear();
        if (_report == null || _tabTemplate == null || _tabContainer == null)
            return;

        for (var i = 0; i < _report.Sheets.Count; i++)
        {
            var tab = Instantiate(_tabTemplate, _tabContainer);
            tab.gameObject.SetActive(true);
            tab.name = "Tab_" + _report.Sheets[i].Id;
            var label = tab.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = _report.Sheets[i].Tab.Get(English);
            var index = i;
            tab.onClick.AddListener(() => SelectSheet(index));
            _tabs.Add(tab);
        }
        RefreshTabColors();
    }

    private void RefreshTabColors()
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i] != null && _tabs[i].targetGraphic != null)
                _tabs[i].targetGraphic.color = i == _sheetIndex ? _activeTabColor : _inactiveTabColor;
        }
        if (_pageText != null)
            _pageText.text = UIStrings.ModelReportPage(_sheetIndex + 1, _report != null ? _report.Sheets.Count : 0);
    }

    private void Redraw()
    {
        var sheet = Sheet;
        if (sheet == null || _cellTemplate == null || _sheetContent == null)
            return;

        Canvas.ForceUpdateCanvases();
        var area = _sheetContent.rect;
        var options = new ReportLayoutOptions
        {
            PageWidth = area.width > 0f ? area.width : 920f,
            PageHeight = area.height > 0f ? area.height : 1400f,
            Padding = _padding,
            MinRowHeight = _minRowHeight,
            BlankRowHeight = _blankRowHeight,
            LinksPerRow = _linksPerRow,
            MinScale = _minScale,
        };
        var labels = new ReportLabels
        {
            Notes = UIStrings.ModelReportNotes,
            NoteFormat = UIStrings.ModelReportNoteFormat,
            SeeAlso = UIStrings.ModelReportSeeAlso,
            AutoTag = UIStrings.ModelReportAutoTag,
            LinkLabel = LinkLabel,
        };

        var measure = _cellTemplate.Text;
        _layout = ReportSheetLayout.Build(sheet, English, labels, options,
            (text, style) => Measure(measure, text, style, Mathf.Infinity).x + _ruleWidth + MeasureSlack,
            (text, style, width) => Measure(measure, text, style, width - _ruleWidth - MeasureSlack).y);

        for (var i = 0; i < _layout.Cells.Count; i++)
        {
            if (i >= _cells.Count)
            {
                var created = Instantiate(_cellTemplate, _sheetContent);
                var index = i;
                if (created.Button != null)
                    created.Button.onClick.AddListener(() => SelectCell(index));
                _cells.Add(created);
            }
            Bind(_cells[i], _layout.Cells[i]);
        }
        for (var i = _layout.Cells.Count; i < _cells.Count; i++)
            _cells[i].gameObject.SetActive(false);
        if (_zoom != null)
            _zoom.ResetZoom();
    }

    private string LinkLabel(string id)
    {
        if (id == ModelReportLinks.RelationMap)
            return _relationMap != null ? UIStrings.ModelReportMapLink : null;
        var entry = _library != null ? _library.Find(id) : null;
        if (entry == null)
            return null;
        return AlmanacProgress.IsUnlocked(entry) ? entry.Title : UIStrings.ModelReportLockedLink;
    }

    private void OpenLink(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;
        if (id == ModelReportLinks.RelationMap)
        {
            OpenRelationMap();
            return;
        }
        var entry = _library != null ? _library.Find(id) : null;
        if (entry != null && AlmanacProgress.IsUnlocked(entry) && _knowledgePopup != null)
            _knowledgePopup.Show(entry);
    }

    // O mapa fica embaixo do almanaque e do relatorio: os dois fecham e voltam quando o mapa fecha.
    private void OpenRelationMap()
    {
        if (_relationMap == null)
            return;
        _almanacWasOpen = _almanac != null && _almanac.IsOpen;
        Hide();
        if (_almanacWasOpen)
            _almanac.Hide();
        if (_waitingForMap == false)
        {
            _relationMap.Closed += HandleMapClosed;
            _waitingForMap = true;
        }
        _relationMap.Show(null);
    }

    private void HandleMapClosed()
    {
        _relationMap.Closed -= HandleMapClosed;
        _waitingForMap = false;
        if (_almanacWasOpen && _almanac != null)
            _almanac.Show();
        var sheet = _sheetIndex;
        Show();
        SelectSheet(sheet);
    }

    private Vector2 Measure(TMP_Text measure, string text, ReportCellStyle style, float width)
    {
        ApplyTextStyle(measure, style, 1f);
        measure.margin = Vector4.zero;
        // Maiusculas pelo estilo nao entram na medida, entao mede o texto ja em maiusculas.
        if (style == ReportCellStyle.Label)
            text = text.ToUpperInvariant();
        return measure.GetPreferredValues(text, width, Mathf.Infinity);
    }

    private void Bind(ReportCellView view, ReportCell cell)
    {
        view.gameObject.SetActive(true);
        var rect = view.Rect;
        rect.anchoredPosition = new Vector2(cell.X, -cell.Y);
        rect.sizeDelta = new Vector2(cell.Width, cell.Height);

        // Pauta: risco embaixo de cada linha; na tabela, tambem entre as colunas.
        var blank = cell.Style == ReportCellStyle.Blank || string.IsNullOrEmpty(cell.Text) && cell.Column < 0;
        var heavy = cell.Style == ReportCellStyle.ColumnHeader || cell.Style == ReportCellStyle.Title;
        var bottom = blank ? 0f : heavy ? _headerRuleWidth : _ruleWidth;
        var right = cell.Column >= 0 && cell.LastColumn == false ? _ruleWidth : 0f;
        if (view.Frame != null)
            view.Frame.color = heavy ? _headerRuleColor : cell.Column >= 0 ? _columnRuleColor : _ruleColor;
        if (view.Fill != null)
        {
            view.Fill.color = FillColor(cell);
            var fill = view.Fill.rectTransform;
            fill.offsetMin = new Vector2(0f, bottom);
            fill.offsetMax = new Vector2(-right, 0f);
        }
        if (view.Text != null)
        {
            ApplyTextStyle(view.Text, cell.Style, _layout.Scale);
            view.Text.text = cell.Text;
            view.Text.color = TextColor(cell.Style);
            var padding = _padding * _layout.Scale;
            var textRect = view.Text.rectTransform;
            if (cell.Vertical)
            {
                // Deitado de baixo pra cima, encostado na base do cabecalho.
                textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.sizeDelta = new Vector2(cell.Height, cell.Width);
                textRect.anchoredPosition = Vector2.zero;
                textRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
                view.Text.alignment = TextAlignmentOptions.Left;
                view.Text.margin = new Vector4(padding + bottom, 0f, padding, 0f);
            }
            else
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                textRect.localRotation = Quaternion.identity;
                view.Text.margin = new Vector4(padding, padding, padding + right, padding + bottom);
            }
        }
        if (view.Button != null)
            view.Button.interactable = cell.Style == ReportCellStyle.Link;
    }

    private void ApplyTextStyle(TMP_Text text, ReportCellStyle style, float scale)
    {
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;
        var size = _textFontSize;
        switch (style)
        {
            case ReportCellStyle.Title:
                size = _titleFontSize;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.BottomLeft;
                break;
            case ReportCellStyle.Label:
                size = _smallFontSize;
                text.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
                text.alignment = TextAlignmentOptions.BottomLeft;
                break;
            case ReportCellStyle.ColumnHeader:
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.BottomLeft;
                break;
            case ReportCellStyle.RowHeader:
                text.fontStyle = FontStyles.Bold;
                break;
            case ReportCellStyle.Matrix:
                text.alignment = TextAlignmentOptions.Top;
                break;
            case ReportCellStyle.NoteLabel:
                size = _smallFontSize;
                text.fontStyle = FontStyles.Bold;
                break;
            case ReportCellStyle.AutoTag:
                size = _smallFontSize;
                text.fontStyle = FontStyles.Italic;
                break;
            case ReportCellStyle.Link:
                text.fontStyle = FontStyles.Underline;
                break;
        }
        text.fontSize = size * scale;
    }

    private Color FillColor(ReportCell cell)
    {
        if (cell.Style == ReportCellStyle.Matrix)
        {
            if (Mathf.Approximately(cell.Tint, 0f))
                return _paperColor;
            var strength = Mathf.Abs(cell.Tint) * 0.5f + 0.08f;
            return Color.Lerp(_paperColor, cell.Tint > 0f ? _positiveColor : _negativeColor, strength);
        }
        var tag = string.IsNullOrEmpty(cell.Tag) ? -1 : Array.IndexOf(ModelReportLinks.Tags, cell.Tag);
        if (tag >= 0 && tag < _tagColors.Length)
            return Color.Lerp(_paperColor, _tagColors[tag], _tagFillStrength);
        return _paperColor;
    }

    private Color TextColor(ReportCellStyle style)
    {
        switch (style)
        {
            case ReportCellStyle.Title:
            case ReportCellStyle.ColumnHeader:
            case ReportCellStyle.AutoTag:
                return _titleInkColor;
            case ReportCellStyle.Label:
                return _softInkColor;
            case ReportCellStyle.NoteLabel:
                return _accentColor;
            case ReportCellStyle.Link:
                return _linkColor;
            default:
                return _inkColor;
        }
    }
}
