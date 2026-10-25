using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mapa de relacoes: os 9 parametros como nos e as relacoes da matriz como setas.
public class RelationMapView : MonoBehaviour
{
    private enum Mode
    {
        Incoming,
        Outgoing,
    }

    // Tons claros, legiveis sobre o fundo azul do popup.
    private const string PositiveText = "#C6FF6B";
    private const string NegativeText = "#FFC2C2";

    // Forca de referencia pra espessura maxima (maior coeficiente da matriz).
    private const float ReferenceStrength = 1.3f;

    // Parcelas menores que isso (no indice) aparecem sem sinal, como zero.
    private const float MinimumShownAmount = 0.05f;

    [SerializeField] private CityStatsManager _cityStatsManager;
    [SerializeField] private ParameterDisplayData _displayData;
    [SerializeField] private InteractionNotesData _notes;

    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _detailText;
    [SerializeField] private Button _closeButton;

    // Fundo escurecido de tela inteira; tocar nele tambem fecha.
    [SerializeField] private Button _backdropButton;
    [SerializeField] private Button _incomingButton;
    [SerializeField] private Button _outgoingButton;

    // Botoes que abrem o mapa sem foco (ex. o do HUD).
    [SerializeField] private Button[] _openButtons = Array.Empty<Button>();

    [Header("Grafo")]
    [SerializeField] private RectTransform _graphArea;
    [SerializeField] private UILineGraphic _lines;
    [SerializeField] private RelationNodeView _nodeTemplate;
    [SerializeField] private float _nodeRadius = 70f;
    [SerializeField] private float _curvature = 0.16f;
    [SerializeField] private float _arrowLength = 26f;
    [SerializeField] private float _minWidth = 3f;
    [SerializeField] private float _maxWidth = 16f;

    [Header("Cores")]
    [SerializeField] private Color _supportColor = new Color(0.36f, 0.85f, 0.38f);
    [SerializeField] private Color _pressureColor = new Color(0.95f, 0.33f, 0.3f);
    [SerializeField] private Color _wellbeingColor = new Color(1f, 0.83f, 0.29f);
    [SerializeField] private Color _healthyRing = new Color(0.85f, 0.92f, 1f);
    [SerializeField] private Color _crisisRing = new Color(0.95f, 0.25f, 0.22f);
    [SerializeField] private Color _excessRing = new Color(1f, 0.6f, 0.15f);
    [SerializeField, Range(0f, 1f)] private float _overviewAlpha = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _hiddenAlpha = 0.04f;

    [Header("Botoes de modo")]
    [SerializeField] private Color _activeModeColor = Color.white;
    [SerializeField] private Color _inactiveModeColor = new Color(1f, 1f, 1f, 0.45f);

    private readonly Dictionary<CityParameterType, RelationNodeView> _nodes = new Dictionary<CityParameterType, RelationNodeView>();
    private readonly List<UILine> _lineBuffer = new List<UILine>();
    private List<RelationEdge> _edges = new List<RelationEdge>();

    private CityStats _stats;
    private CityParameterType? _selected;
    private CityParameterType? _pairOther;
    private Mode _mode = Mode.Incoming;
    private bool _dirty;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    public event Action Opened;

    private void Start()
    {
        _stats = _cityStatsManager != null ? _cityStatsManager.Stats : null;
        if (_stats != null)
            _stats.OnParameterChanged += HandleParameterChanged;

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Hide);
            var closeText = _closeButton.GetComponentInChildren<TMP_Text>();
            if (closeText != null)
                closeText.text = UIStrings.OkButton;
        }
        if (_backdropButton != null)
            _backdropButton.onClick.AddListener(Hide);
        if (_incomingButton != null)
            _incomingButton.onClick.AddListener(() => SetMode(Mode.Incoming));
        if (_outgoingButton != null)
            _outgoingButton.onClick.AddListener(() => SetMode(Mode.Outgoing));
        foreach (var button in _openButtons)
        {
            if (button != null)
                button.onClick.AddListener(() => Show(null));
        }

        BuildNodes();
        Hide();
    }

    private void OnDisable()
    {
        if (_stats != null)
            _stats.OnParameterChanged -= HandleParameterChanged;
    }

    // focus nulo abre a visao geral; com foco, ja mostra o que afeta aquele parametro.
    public void Show(CityParameterType? focus)
    {
        _selected = focus;
        _pairOther = null;
        _mode = Mode.Incoming;
        if (_titleText != null)
            _titleText.text = UIStrings.RelationMapTitle;
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        Redraw();
        Opened?.Invoke();
    }

    public void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void HandleParameterChanged(CityParameterType parameter, float value)
    {
        _dirty = true;
    }

    // Uma resolucao muda varios parametros em sequencia; redesenha uma vez so no fim do quadro.
    private void LateUpdate()
    {
        if (_dirty == false)
            return;
        _dirty = false;
        if (IsOpen)
            Redraw();
    }

    private void BuildNodes()
    {
        if (_nodeTemplate == null || _graphArea == null)
            return;

        _nodeTemplate.gameObject.SetActive(false);
        foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
        {
            var node = Instantiate(_nodeTemplate, _nodeTemplate.transform.parent);
            node.name = $"Node{parameter}";
            node.gameObject.SetActive(true);
            node.RectTransform.anchoredPosition = NodePosition(parameter);
            var captured = parameter;
            node.Bind(_displayData != null ? _displayData.GetIcon(parameter) : null, parameter.GetDisplayName(), () => HandleNodeClicked(captured));
            _nodes[parameter] = node;
        }
    }

    // Bem-estar no centro (e recalculado dos outros); os 8 alvos da matriz em volta, na ordem de resolucao.
    private Vector2 NodePosition(CityParameterType parameter)
    {
        if (parameter == CityStats.AnchorParameter)
            return Vector2.zero;

        var order = InteractionMatrix.Order;
        var index = 0;
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] == parameter)
                index = i;
        }

        var size = _graphArea.rect.size;
        var radius = Mathf.Min(size.x, size.y) * 0.5f - _nodeRadius - 30f;
        var angle = Mathf.PI * 0.5f - index * 2f * Mathf.PI / order.Count;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private void HandleNodeClicked(CityParameterType parameter)
    {
        if (_selected.HasValue == false)
            _selected = parameter;
        else if (_selected.Value == parameter)
            Deselect();
        else
            _pairOther = _pairOther == parameter ? (CityParameterType?)null : parameter;
        Redraw();
    }

    private void Deselect()
    {
        // Com um par aberto, tocar no selecionado volta pra lista; sem par, volta pra visao geral.
        if (_pairOther.HasValue)
            _pairOther = null;
        else
            _selected = null;
    }

    private void SetMode(Mode mode)
    {
        _mode = mode;
        _pairOther = null;
        Redraw();
    }

    private void Redraw()
    {
        _edges = RelationGraph.Build(_stats);
        RefreshNodes();
        DrawLines();
        RefreshModeButtons();
        if (_detailText != null)
            _detailText.text = BuildDetail();
    }

    private void RefreshNodes()
    {
        foreach (var entry in _nodes)
        {
            var parameter = entry.Key;
            var node = entry.Value;
            node.SetName(parameter.GetDisplayName());
            node.SetRingColor(RingColor(parameter));

            var selected = _selected == parameter || _pairOther == parameter;
            node.SetState(selected, _selected.HasValue && selected == false && IsHighlightedNeighbor(parameter) == false);
        }
    }

    private Color RingColor(CityParameterType parameter)
    {
        var matrix = _stats?.Interactions;
        if (matrix == null)
            return _healthyRing;

        switch (matrix.GetZone(_stats.GetValue(parameter)))
        {
            case InteractionZone.Crise: return _crisisRing;
            case InteractionZone.Excesso: return _excessRing;
            default: return _healthyRing;
        }
    }

    private bool IsHighlightedNeighbor(CityParameterType parameter)
    {
        if (_pairOther.HasValue)
            return false;
        foreach (var edge in _edges)
        {
            if (IsHighlighted(edge) && (edge.From == parameter || edge.To == parameter))
                return true;
        }
        return false;
    }

    private bool IsHighlighted(RelationEdge edge)
    {
        if (_selected.HasValue == false)
            return false;
        var selected = _selected.Value;
        if (_pairOther.HasValue)
        {
            var other = _pairOther.Value;
            return (edge.From == selected && edge.To == other) || (edge.From == other && edge.To == selected);
        }
        return _mode == Mode.Incoming ? edge.To == selected : edge.From == selected;
    }

    private void DrawLines()
    {
        if (_lines == null)
            return;

        _lineBuffer.Clear();
        // Primeiro as apagadas, depois as destacadas, pra estas ficarem por cima.
        foreach (var highlighted in new[] { false, true })
        {
            foreach (var edge in _edges)
            {
                if (IsHighlighted(edge) == highlighted)
                    _lineBuffer.Add(BuildLine(edge, highlighted));
            }
        }
        _lines.SetLines(_lineBuffer);
    }

    private UILine BuildLine(RelationEdge edge, bool highlighted)
    {
        var from = NodePosition(edge.From);
        var to = NodePosition(edge.To);
        var direction = (to - from).normalized;
        var start = from + direction * _nodeRadius;
        var end = to - direction * (_nodeRadius + 4f);

        // Curva pra direita do sentido da seta, pra ida e volta entre o mesmo par nao se sobreporem.
        var right = new Vector2(direction.y, -direction.x);
        var control = (start + end) * 0.5f + right * (Vector2.Distance(start, end) * _curvature);

        var isPart = edge.Kind == RelationKind.WellbeingPart;
        var color = isPart && edge.IsSupport ? _wellbeingColor : (edge.IsSupport ? _supportColor : _pressureColor);
        var alpha = _selected.HasValue ? (highlighted ? 1f : _hiddenAlpha) : _overviewAlpha;
        color.a *= alpha;

        var strength = isPart ? 0.4f : edge.Strength;
        var width = Mathf.Lerp(_minWidth, _maxWidth, Mathf.Clamp01(strength / ReferenceStrength));
        if (highlighted == false)
            width = Mathf.Max(_minWidth * 0.7f, width * 0.6f);

        return new UILine
        {
            Start = start,
            End = end,
            Control = control,
            Width = width,
            Color = color,
            ArrowLength = highlighted || _selected.HasValue == false ? _arrowLength : 0f,
            Dashed = isPart,
        };
    }

    private void RefreshModeButtons()
    {
        var visible = _selected.HasValue;
        SetModeButton(_incomingButton, visible, _mode == Mode.Incoming, UIStrings.RelationModeIncoming);
        SetModeButton(_outgoingButton, visible, _mode == Mode.Outgoing, UIStrings.RelationModeOutgoing);
    }

    private void SetModeButton(Button button, bool visible, bool active, string label)
    {
        if (button == null)
            return;
        button.gameObject.SetActive(visible);
        if (button.targetGraphic != null)
            button.targetGraphic.color = active ? _activeModeColor : _inactiveModeColor;
        var text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = label;
    }

    private string BuildDetail()
    {
        if (_selected.HasValue == false)
            return UIStrings.RelationMapIntro;

        var selected = _selected.Value;
        var body = new StringBuilder();
        AppendNodeHeader(body, selected);
        if (_pairOther.HasValue)
            AppendPair(body, selected, _pairOther.Value);
        else
            AppendList(body, selected);
        return body.ToString().TrimEnd();
    }

    private void AppendNodeHeader(StringBuilder body, CityParameterType parameter)
    {
        if (_stats?.Interactions == null)
        {
            body.Append(UIStrings.ParameterIcon(parameter)).Append(" <b>").Append(parameter.GetFullName()).Append("</b>\n");
            return;
        }
        var value = _stats.GetValue(parameter);
        body.Append(UIStrings.RelationNodeHeader(parameter, Level(parameter, value), _stats.Interactions.GetZone(value))).Append('\n');
    }

    private void AppendList(StringBuilder body, CityParameterType selected)
    {
        var incoming = _mode == Mode.Incoming;
        var edges = incoming ? RelationGraph.Incoming(_edges, selected) : RelationGraph.Outgoing(_edges, selected);

        if (incoming && selected == CityStats.AnchorParameter)
            body.Append(UIStrings.RelationWellbeingHeader);
        else
            body.Append(incoming ? UIStrings.RelationIncomingHeader(selected) : UIStrings.RelationOutgoingHeader(selected));
        body.Append('\n');

        if (edges.Count == 0)
            body.Append(UIStrings.RelationNoLinks).Append('\n');

        var anyReversed = false;
        foreach (var edge in edges)
        {
            anyReversed |= IsReversed(edge);
            var other = incoming ? edge.From : edge.To;
            body.Append(UIStrings.ParameterIcon(other)).Append(' ').Append(other.GetFullName());
            body.Append("<pos=46%>").Append(KindLabel(edge));
            if (edge.HasState)
                body.Append("<pos=78%>").Append(Colored(edge.To, edge.CurrentAmount));
            body.Append('\n');
        }

        body.Append("\n<size=80%>");
        if (anyReversed)
            body.Append(UIStrings.RelationReversedHint).Append(' ');
        body.Append(UIStrings.RelationPairHint).Append("</size>");
    }

    private void AppendPair(StringBuilder body, CityParameterType a, CityParameterType b)
    {
        var pair = RelationGraph.Between(_edges, a, b);
        if (pair.Count == 0)
        {
            body.Append('\n').Append(UIStrings.RelationNone(a, b));
            return;
        }

        foreach (var edge in pair)
        {
            body.Append('\n').Append(UIStrings.RelationPairTitle(edge.From, edge.To));
            body.Append(": ").Append(KindLabel(edge)).Append('\n');

            if (edge.HasState)
            {
                var amount = Colored(edge.To, edge.CurrentAmount);
                body.Append(edge.Kind == RelationKind.WellbeingPart ? UIStrings.RelationPartNow(amount) : UIStrings.RelationNow(amount, edge.To)).Append('\n');
                if (edge.Kind == RelationKind.Interaction && Mathf.Approximately(edge.ZoneFactor, 1f) == false)
                {
                    var zone = _stats.Interactions.GetZone(_stats.GetValue(edge.From));
                    body.Append(UIStrings.RelationZoneNote(edge.From, zone, edge.ZoneFactor, edge.IsSupport)).Append('\n');
                }
                if (IsReversed(edge))
                    body.Append(UIStrings.RelationReversedNote(edge.From, edge.IsSupport)).Append('\n');
            }

            if (_notes != null && _notes.TryGetNote(edge.From, edge.To, out var note))
            {
                body.Append("<size=85%>").Append(_notes.GetText(edge.From, edge.To)).Append("</size>\n");
                if (string.IsNullOrEmpty(note.Reference) == false)
                    body.Append("<size=70%><i>").Append(UIStrings.RelationReference(note.Reference)).Append("</i></size>\n");
            }
        }
    }

    // Fonte abaixo da metade da escala inverte o sentido: o apoio puxa pra baixo e a pressao alivia.
    private static bool IsReversed(RelationEdge edge)
    {
        return edge.Kind == RelationKind.Interaction
            && Math.Abs(edge.CurrentAmount) >= MinimumShownAmount
            && edge.CurrentAmount > 0f != edge.IsSupport;
    }

    private static string KindLabel(RelationEdge edge)
    {
        if (edge.Kind == RelationKind.WellbeingPart)
            return edge.IsSupport ? UIStrings.WellbeingPartAverage : UIStrings.WellbeingPartCrowding;
        return UIStrings.RelationLabel(edge.IsSupport, RelationGraph.GetStrength(edge.Strength));
    }

    private string Level(CityParameterType parameter, float value)
    {
        return _displayData != null ? _displayData.FormatValue(parameter, value, smallUnit: true) : value.ToString("0");
    }

    private string Colored(CityParameterType parameter, float amount)
    {
        if (Math.Abs(amount) < MinimumShownAmount)
            return "0";
        var sign = amount < 0f ? "−" : "+";
        var color = amount < 0f ? NegativeText : PositiveText;
        return $"<b><color={color}>{sign}{Magnitude(parameter, amount)}</color></b>";
    }

    private string Magnitude(CityParameterType parameter, float amount)
    {
        return _displayData != null
            ? _displayData.FormatAmount(parameter, Math.Abs(amount), smallUnit: true, extraDecimals: 1)
            : Math.Abs(amount).ToString("0.0");
    }
}
