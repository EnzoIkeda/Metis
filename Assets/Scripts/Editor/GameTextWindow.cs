using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Lista todos os textos do jogo para leitura e verificacao. Esta etapa nao altera nada.
public class GameTextWindow : EditorWindow
{
    private const string AllOrigins = "Todas as origens";
    private const string AllTexts = "Todos os textos";
    private const string AnyIssue = "Com qualquer problema";

    private static readonly Dictionary<GameTextOrigin, string> OriginNames = new Dictionary<GameTextOrigin, string>
    {
        { GameTextOrigin.Card, "Cartas" },
        { GameTextOrigin.Event, "Eventos" },
        { GameTextOrigin.Advantage, "Vantagens" },
        { GameTextOrigin.Preset, "Presets" },
        { GameTextOrigin.Structure, "Estruturas" },
        { GameTextOrigin.Knowledge, "Fichas" },
        { GameTextOrigin.News, "Jornal" },
        { GameTextOrigin.RelationNote, "Mapa de relações" },
        { GameTextOrigin.ParameterUnit, "Unidades" },
        { GameTextOrigin.Report, "Relatório técnico" },
        { GameTextOrigin.Interface, "Interface" },
        { GameTextOrigin.SceneOrPrefab, "Cenas e prefabs" },
    };

    private static readonly Dictionary<GameTextIssue, string> IssueNames = new Dictionary<GameTextIssue, string>
    {
        { GameTextIssue.MissingPortuguese, "Falta português" },
        { GameTextIssue.MissingEnglish, "Falta inglês" },
        { GameTextIssue.LikelyUntranslated, "PT e EN iguais" },
        { GameTextIssue.PlaceholderMismatch, "Variáveis diferentes entre PT e EN" },
        { GameTextIssue.Dash, "Travessão" },
        { GameTextIssue.MetisWithoutAccent, "Metis sem acento" },
        { GameTextIssue.YearsInsteadOfTurns, "Anos no lugar de turnos" },
        { GameTextIssue.ExtraSpace, "Espaço sobrando" },
        { GameTextIssue.AccentInTitleFont, "Acento na fonte de título" },
    };

    private readonly List<GameTextItem> _all = new List<GameTextItem>();
    private readonly Dictionary<GameTextItem, List<GameTextIssue>> _issues = new Dictionary<GameTextItem, List<GameTextIssue>>();
    private readonly List<GameTextItem> _visible = new List<GameTextItem>();

    private ToolbarSearchField _search;
    private PopupField<string> _originFilter;
    private PopupField<string> _issueFilter;
    private Label _counter;
    private ListView _list;
    private VisualElement _detail;

    [MenuItem("Tools/Metis/Textos/Verificar Textos")]
    private static void Open()
    {
        var window = GetWindow<GameTextWindow>("Textos do jogo");
        window.minSize = new Vector2(720, 420);
    }

    private void CreateGUI()
    {
        var toolbar = new Toolbar();
        _search = new ToolbarSearchField();
        _search.style.flexGrow = 1;
        _search.RegisterValueChangedCallback(_ => ApplyFilters());
        toolbar.Add(_search);

        var origins = new List<string> { AllOrigins };
        origins.AddRange(OriginNames.Values);
        _originFilter = new PopupField<string>(origins, 0);
        _originFilter.RegisterValueChangedCallback(_ => ApplyFilters());
        toolbar.Add(_originFilter);

        var filters = new List<string> { AllTexts, AnyIssue };
        filters.AddRange(IssueNames.Values);
        _issueFilter = new PopupField<string>(filters, 0);
        _issueFilter.RegisterValueChangedCallback(_ => ApplyFilters());
        toolbar.Add(_issueFilter);

        toolbar.Add(new ToolbarButton(Reload) { text = "Atualizar" });
        rootVisualElement.Add(toolbar);

        _counter = new Label();
        _counter.style.marginLeft = 4;
        _counter.style.marginTop = 2;
        _counter.style.marginBottom = 2;
        rootVisualElement.Add(_counter);

        var split = new TwoPaneSplitView(0, 320, TwoPaneSplitViewOrientation.Horizontal);
        split.style.flexGrow = 1;
        _list = new ListView(_visible, 36, MakeRow, BindRow) { selectionType = SelectionType.Single };
        _list.selectionChanged += _ => ShowSelected();
        split.Add(_list);
        var detailScroll = new ScrollView();
        _detail = detailScroll.contentContainer;
        _detail.style.paddingLeft = 8;
        _detail.style.paddingRight = 8;
        split.Add(detailScroll);
        rootVisualElement.Add(split);

        Reload();
    }

    private void Reload()
    {
        _all.Clear();
        _issues.Clear();
        _all.AddRange(GameTextCollector.Collect());
        foreach (var item in _all)
            _issues[item] = GameTextRules.Check(item);
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var origin = OriginNames.FirstOrDefault(pair => pair.Value == _originFilter.value);
        var hasOrigin = _originFilter.value != AllOrigins;
        var issueName = _issueFilter.value;
        var specific = IssueNames.FirstOrDefault(pair => pair.Value == issueName);

        _visible.Clear();
        foreach (var item in _all)
        {
            if (hasOrigin && item.Origin != origin.Key)
                continue;
            if (issueName == AnyIssue && _issues[item].Count == 0)
                continue;
            if (issueName != AnyIssue && issueName != AllTexts && _issues[item].Contains(specific.Key) == false)
                continue;
            if (GameTextRules.Matches(item, _search.value))
                _visible.Add(item);
        }
        var withIssues = _all.Count(item => _issues[item].Count > 0);
        _counter.text = $"{_visible.Count} de {_all.Count} textos; {withIssues} com algum problema. Somente leitura.";
        _list.ClearSelection();
        _list.Rebuild();
        ShowSelected();
    }

    private static VisualElement MakeRow()
    {
        var row = new VisualElement();
        row.style.paddingLeft = 4;
        row.style.justifyContent = Justify.Center;
        var title = new Label { name = "title" };
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        var text = new Label { name = "text" };
        row.Add(title);
        row.Add(text);
        return row;
    }

    private void BindRow(VisualElement row, int index)
    {
        var item = _visible[index];
        var flag = _issues[item].Count > 0 ? " [!]" : string.Empty;
        row.Q<Label>("title").text = $"{OriginNames[item.Origin]}: {item.Id}{flag}";
        row.Q<Label>("text").text = OneLine(string.IsNullOrEmpty(item.Pt) ? item.En : item.Pt);
    }

    private static string OneLine(string text)
    {
        var flat = (text ?? string.Empty).Replace('\n', ' ');
        return flat.Length > 70 ? flat.Substring(0, 70) + "..." : flat;
    }

    private void ShowSelected()
    {
        _detail.Clear();
        if (_list.selectedItem is not GameTextItem item)
            return;

        var where = item.Line > 0 ? $"{item.Location}:{item.Line}" : item.Location;
        AddLabel($"{OriginNames[item.Origin]}: {item.Id} {item.Field}", true);
        AddLabel(where, false);
        foreach (var issue in _issues[item])
            AddLabel("Problema: " + IssueNames[issue], true);

        AddText(item.IsPaired ? "Português" : "Texto (sem par de idioma)", item.Pt);
        if (item.IsPaired)
            AddText("Inglês", item.En);

        var open = new Button(() => OpenOrigin(item)) { text = "Abrir origem" };
        open.style.marginTop = 8;
        _detail.Add(open);
    }

    private void AddLabel(string text, bool bold)
    {
        var label = new Label(text);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 4;
        if (bold)
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        _detail.Add(label);
    }

    private void AddText(string title, string text)
    {
        AddLabel(title, true);
        var field = new TextField { value = text ?? string.Empty, multiline = true, isReadOnly = true };
        field.style.whiteSpace = WhiteSpace.Normal;
        _detail.Add(field);
    }

    private static void OpenOrigin(GameTextItem item)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath(item.Location);
        if (asset == null)
            return;
        if (item.Location.EndsWith(".cs", StringComparison.Ordinal))
        {
            AssetDatabase.OpenAsset(asset, item.Line);
            return;
        }
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
