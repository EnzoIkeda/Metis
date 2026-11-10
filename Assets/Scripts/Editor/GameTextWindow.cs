using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Consulta, verifica, revisa e edita os textos do jogo.
public class GameTextWindow : EditorWindow
{
    private const string AllOrigins = "Todas as origens";
    private const string AllTexts = "Todos os textos";
    private const string AnyIssue = "Com qualquer problema";
    private const string AnyReview = "Qualquer revisão";
    private const string PendingReview = "Pendentes";
    private const string DoneReview = "Revisados";
    private const string SelectedKeyState = "Metis.GameText.Selected";

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
        { GameTextOrigin.Theme, "Fases temáticas" },
        { GameTextOrigin.Dilemma, "Dilemas" },
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
    private PopupField<string> _reviewFilter;
    private GameTextReview _review = new GameTextReview();
    private GameTextItem _shown;
    private TextField _ptField;
    private TextField _enField;
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

        _reviewFilter = new PopupField<string>(new List<string> { AnyReview, PendingReview, DoneReview }, 0);
        _reviewFilter.RegisterValueChangedCallback(_ => ApplyFilters());
        toolbar.Add(_reviewFilter);

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
        _list.selectionChanged += _ => OnSelectionChanged();
        split.Add(_list);
        var detailScroll = new ScrollView();
        _detail = detailScroll.contentContainer;
        _detail.style.paddingLeft = 8;
        _detail.style.paddingRight = 8;
        split.Add(detailScroll);
        rootVisualElement.Add(split);

        Reload();

        // Salvar um texto da interface recompila e recria a janela: volta para o mesmo texto.
        var restored = SessionState.GetString(SelectedKeyState, string.Empty);
        SessionState.EraseString(SelectedKeyState);
        if (restored.Length > 0)
            SelectByKey(restored);
    }

    private void Reload()
    {
        var selectedKey = _shown != null ? GameTextReview.KeyOf(_shown) : null;
        _all.Clear();
        _issues.Clear();
        _all.AddRange(GameTextCollector.Collect());
        foreach (var item in _all)
            _issues[item] = GameTextRules.Check(item);
        _review = GameTextReviewStore.Load();
        ApplyFilters();
        SelectByKey(selectedKey);
    }

    private void SelectByKey(string key)
    {
        if (key == null)
            return;
        var index = _visible.FindIndex(item => GameTextReview.KeyOf(item) == key);
        if (index >= 0)
        {
            _list.selectedIndex = index;
            _list.ScrollToItem(index);
        }
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
            if (_reviewFilter.value == PendingReview && _review.IsReviewed(item))
                continue;
            if (_reviewFilter.value == DoneReview && _review.IsReviewed(item) == false)
                continue;
            if (GameTextRules.Matches(item, _search.value))
                _visible.Add(item);
        }
        var withIssues = _all.Count(item => _issues[item].Count > 0);
        var reviewed = _all.Count(_review.IsReviewed);
        _counter.text = $"{_visible.Count} de {_all.Count} textos; {withIssues} com algum problema; {reviewed} revisados.";
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
        var flag = (_issues[item].Count > 0 ? " [!]" : string.Empty) + (_review.IsReviewed(item) ? " [revisado]" : string.Empty);
        row.Q<Label>("title").text = $"{OriginNames[item.Origin]}: {item.Id}{flag}";
        row.Q<Label>("text").text = OneLine(string.IsNullOrEmpty(item.Pt) ? item.En : item.Pt);
    }

    private static string OneLine(string text)
    {
        var flat = (text ?? string.Empty).Replace('\n', ' ');
        return flat.Length > 70 ? flat.Substring(0, 70) + "..." : flat;
    }

    private void OnSelectionChanged()
    {
        if (hasUnsavedChanges && _shown != null)
        {
            if (EditorUtility.DisplayDialog("Textos do jogo", $"Salvar as alterações em {_shown.Id}?", "Salvar", "Descartar"))
            {
                var pending = _list.selectedItem as GameTextItem;
                Save();
                if (pending != null)
                    SelectByKey(GameTextReview.KeyOf(pending));
                return;
            }
            hasUnsavedChanges = false;
        }
        ShowSelected();
    }

    private void ShowSelected()
    {
        _detail.Clear();
        _shown = _list.selectedItem as GameTextItem;
        _ptField = null;
        _enField = null;
        hasUnsavedChanges = false;
        var item = _shown;
        if (item == null)
            return;

        var where = item.Line > 0 ? $"{item.Location}:{item.Line}" : item.Location;
        AddLabel($"{OriginNames[item.Origin]}: {item.Id} {item.Field}", true);
        AddLabel(where, false);
        foreach (var issue in _issues[item])
            AddLabel("Problema: " + IssueNames[issue], true);

        var editable = item.EditKind != GameTextEditKind.ReadOnly;
        if (editable == false)
            AddLabel(item.ReadOnlyReason, false);

        _ptField = AddText(item.IsPaired ? "Português" : "Texto (sem par de idioma)", item.Pt, editable);
        if (item.IsPaired)
            _enField = AddText("Inglês", item.En, editable);

        var isReviewed = _review.IsReviewed(item);
        AddLabel(isReviewed ? "Situação: revisado" : "Situação: pendente de revisão", false);
        if (editable)
            AddButton("Salvar", Save);
        AddButton(isReviewed ? "Voltar para pendente" : "Marcar como revisado", ToggleReview);
        if (isReviewed == false)
            AddButton("Marcar e ir para o próximo pendente", MarkAndNext);
        AddButton("Abrir origem", () => OpenOrigin(item));
    }

    private void AddButton(string text, Action action)
    {
        var button = new Button(action) { text = text };
        button.style.marginTop = 6;
        _detail.Add(button);
    }

    private void Save()
    {
        var item = _shown;
        if (item == null || _ptField == null)
            return;
        var pt = _ptField.value;
        var en = _enField != null ? _enField.value : item.En;
        if (GameTextSaveRules.HasChange(item, pt, en) == false)
        {
            hasUnsavedChanges = false;
            return;
        }

        var plan = GameTextSaver.Prepare(item, pt, en);
        if (plan.CanCommit == false)
        {
            EditorUtility.DisplayDialog("Não foi possível salvar", string.Join("\n", plan.Errors), "OK");
            return;
        }

        var message = $"Destino: {plan.Destination}\n\nPT antes: {Short(item.Pt)}\nPT depois: {Short(plan.Pt)}";
        if (item.IsPaired)
            message += $"\n\nEN antes: {Short(item.En)}\nEN depois: {Short(plan.En)}";
        if (plan.Warnings.Count > 0)
            message += "\n\nAvisos:\n" + string.Join("\n", plan.Warnings);
        message += "\n\nA marca de revisão deste texto deixa de valer.";
        if (EditorUtility.DisplayDialog("Salvar texto", message, "Salvar", "Cancelar") == false)
            return;

        if (GameTextSaver.Commit(plan, out var error) == false)
        {
            EditorUtility.DisplayDialog("Erro ao salvar", error, "OK");
            return;
        }
        hasUnsavedChanges = false;
        if (item.EditKind == GameTextEditKind.Interface)
            SessionState.SetString(SelectedKeyState, GameTextReview.KeyOf(item));
        Reload();
    }

    public override void SaveChanges()
    {
        Save();
        base.SaveChanges();
    }

    private void ToggleReview()
    {
        if (_review.IsReviewed(_shown))
            _review.Unmark(_shown);
        else
            _review.Mark(_shown, DateTime.Now.ToString("yyyy-MM-dd"));
        GameTextReviewStore.Save(_review);
        var key = GameTextReview.KeyOf(_shown);
        ApplyFilters();
        SelectByKey(key);
    }

    private void MarkAndNext()
    {
        var index = _visible.IndexOf(_shown);
        var current = _shown;
        _review.Mark(current, DateTime.Now.ToString("yyyy-MM-dd"));
        GameTextReviewStore.Save(_review);
        ApplyFilters();

        // Com o filtro de pendentes o item sai da lista e o proximo ocupa o lugar dele.
        var next = _visible.FindIndex(Math.Max(0, index), item => item != current && _review.IsReviewed(item) == false);
        if (next >= 0)
        {
            _list.selectedIndex = next;
            _list.ScrollToItem(next);
        }
    }

    private static string Short(string text)
    {
        var value = text ?? string.Empty;
        return value.Length > 220 ? value.Substring(0, 220) + "..." : value;
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

    private TextField AddText(string title, string text, bool editable)
    {
        AddLabel(title, true);
        var field = new TextField { value = text ?? string.Empty, multiline = true, isReadOnly = editable == false };
        field.style.whiteSpace = WhiteSpace.Normal;
        if (editable)
            field.RegisterValueChangedCallback(_ => RefreshUnsaved());
        _detail.Add(field);
        return field;
    }

    private void RefreshUnsaved()
    {
        if (_shown == null || _ptField == null)
            return;
        hasUnsavedChanges = GameTextSaveRules.HasChange(_shown, _ptField.value, _enField != null ? _enField.value : _shown.En);
        saveChangesMessage = $"Salvar as alterações em {_shown.Id}?";
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
