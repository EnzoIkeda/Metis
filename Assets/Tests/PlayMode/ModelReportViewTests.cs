using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Relatorio tecnico montado de verdade: abas, folha que cabe no papel, links pras fichas e ida e volta do mapa.
public class ModelReportViewTests
{
    private const string TestKey = "Metis.Almanac.ReportPlayModeTests";

    private readonly List<Object> _created = new List<Object>();
    private GameObject _canvas;
    private ModelReportView _view;
    private GameObject _panel;
    private RectTransform _tabs;
    private TMP_Text _page;
    private RectTransform _content;
    private Button _close;
    private KnowledgePopupView _popup;
    private KnowledgeEntryData _unlocked;
    private KnowledgeEntryData _locked;
    private RelationMapView _map;

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(TestKey);
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_prefsKeyOverride", TestKey);
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_state", null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvas != null)
            Object.DestroyImmediate(_canvas);
        foreach (var created in _created)
        {
            if (created != null)
                Object.DestroyImmediate(created);
        }
        _created.Clear();
        PlayerPrefs.DeleteKey(TestKey);
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_prefsKeyOverride", null);
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_state", null);
    }

    private static ReportText T(string text) => new ReportText { Pt = text, En = text + " EN" };

    private static ReportSheet Sheet(string id, params string[] links)
    {
        return new ReportSheet
        {
            Id = id,
            Tab = T("Aba " + id),
            Title = T("Titulo " + id),
            Intro = T("Introducao " + id),
            Columns = new[] { T("Indicador"), T("Valor") },
            Rows = new[] { new ReportRow { Cells = new[] { T("Renda"), T("55") } } },
            Notes = new[] { T("Nota") },
            SeeAlso = links,
        };
    }

    private KnowledgeEntryData Entry(string id)
    {
        var entry = ScriptableObject.CreateInstance<KnowledgeEntryData>();
        entry.name = id;
        TestDataFactory.SetField(entry, "_title", "Ficha " + id);
        TestDataFactory.SetField(entry, "_titleEn", "Entry " + id);
        TestDataFactory.SetField(entry, "_category", KnowledgeCategory.Concept);
        _created.Add(entry);
        return entry;
    }

    private static GameObject Ui(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var component in components)
            go.AddComponent(component);
        return go;
    }

    private static TMP_Text Text(string name, Transform parent)
    {
        return Ui(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
    }

    private ReportCellView CellTemplate(Transform parent)
    {
        var root = Ui("Cell", parent, typeof(Image), typeof(Button), typeof(ReportCellView));
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        var fill = Ui("Fill", root.transform, typeof(Image));
        var fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        var text = Text("Text", root.transform);
        var view = root.GetComponent<ReportCellView>();
        TestDataFactory.SetField(view, "_frame", root.GetComponent<Image>());
        TestDataFactory.SetField(view, "_fill", fill.GetComponent<Image>());
        TestDataFactory.SetField(view, "_text", text);
        TestDataFactory.SetField(view, "_button", root.GetComponent<Button>());
        return view;
    }

    private IEnumerator Build(bool withMap)
    {
        _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _canvas.SetActive(false);

        _unlocked = Entry("Aberta");
        _locked = Entry("Fechada");
        AlmanacProgress.Unlock(_unlocked);
        var library = ScriptableObject.CreateInstance<KnowledgeLibraryData>();
        TestDataFactory.SetField(library, "_entries", new[] { _unlocked, _locked });
        _created.Add(library);

        var report = ScriptableObject.CreateInstance<ModelReportData>();
        report.EditorReplace(new ReportFile
        {
            Version = 1,
            ModelVersion = "1.0",
            Fingerprint = "P:1",
            Sheets = new[] { Sheet("Primeira", "Aberta", "Fechada", ModelReportLinks.RelationMap), Sheet("Segunda") },
        });
        _created.Add(report);

        var popupHost = Ui("KnowledgeController", _canvas.transform);
        _popup = popupHost.AddComponent<KnowledgePopupView>();
        TestDataFactory.SetField(_popup, "_panelRoot", Ui("KnowledgePanel", _canvas.transform));

        if (withMap)
        {
            var mapHost = Ui("MapController", _canvas.transform);
            _map = mapHost.AddComponent<RelationMapView>();
            TestDataFactory.SetField(_map, "_panelRoot", Ui("MapPanel", _canvas.transform));
        }

        _panel = Ui("ReportPanel", _canvas.transform);
        var content = (RectTransform)Ui("Content", _panel.transform).transform;
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = new Vector2(900f, 1300f);
        _content = content;
        _tabs = (RectTransform)Ui("Tabs", _panel.transform).transform;
        var tabTemplate = Ui("TabTemplate", _tabs, typeof(Image), typeof(Button)).GetComponent<Button>();
        Text("Label", tabTemplate.transform);
        _page = Text("Page", _panel.transform);
        _close = Ui("Close", _panel.transform, typeof(Image), typeof(Button)).GetComponent<Button>();

        var host = Ui("ReportController", _canvas.transform);
        _view = host.AddComponent<ModelReportView>();
        TestDataFactory.SetField(_view, "_report", report);
        TestDataFactory.SetField(_view, "_library", library);
        TestDataFactory.SetField(_view, "_knowledgePopup", _popup);
        TestDataFactory.SetField(_view, "_relationMap", _map);
        TestDataFactory.SetField(_view, "_panelRoot", _panel);
        TestDataFactory.SetField(_view, "_pageText", _page);
        TestDataFactory.SetField(_view, "_closeButton", _close);
        TestDataFactory.SetField(_view, "_sheetContent", content);
        TestDataFactory.SetField(_view, "_cellTemplate", CellTemplate(content));
        TestDataFactory.SetField(_view, "_tabContainer", _tabs);
        TestDataFactory.SetField(_view, "_tabTemplate", tabTemplate);

        _canvas.SetActive(true);
        var start = Time.realtimeSinceStartup;
        while (_panel.activeSelf && Time.realtimeSinceStartup - start < 5f)
            yield return null;
        Assert.That(_panel.activeSelf, Is.False, "o relatorio comeca fechado");
    }

    private int CellIndex(System.Func<ReportCell, bool> match)
    {
        var cells = _view.Layout.Cells;
        for (var i = 0; i < cells.Count; i++)
        {
            if (match(cells[i]))
                return i;
        }
        return -1;
    }

    private static bool English => LocalizationManager.Current == Language.English;

    [UnityTest]
    public IEnumerator Show_OpensFirstSheet_AndNumbersThePage()
    {
        yield return Build(withMap: false);

        _view.Show();

        Assert.That(_view.IsOpen, Is.True);
        Assert.That(_view.SheetId, Is.EqualTo("Primeira"));
        Assert.That(_page.text, Is.EqualTo(UIStrings.ModelReportPage(1, 2)));
        Assert.That(_view.Layout.Cells[0].Text, Is.EqualTo(English ? "Titulo Primeira EN" : "Titulo Primeira"));
        var tabs = _tabs.GetComponentsInChildren<Button>(false);
        Assert.That(tabs.Length, Is.EqualTo(2));
        Assert.That(tabs[1].GetComponentInChildren<TMP_Text>().text, Is.EqualTo(English ? "Aba Segunda EN" : "Aba Segunda"));
    }

    [UnityTest]
    public IEnumerator Tab_SwitchesSheetAndReusesCells()
    {
        yield return Build(withMap: false);
        _view.Show();

        _tabs.GetComponentsInChildren<Button>(false)[1].onClick.Invoke();

        Assert.That(_view.SheetId, Is.EqualTo("Segunda"));
        Assert.That(_page.text, Is.EqualTo(UIStrings.ModelReportPage(2, 2)));
        var active = _view.GetComponentInParent<Canvas>().GetComponentsInChildren<ReportCellView>(false).Length;
        Assert.That(active, Is.EqualTo(_view.Layout.Cells.Count));
    }

    [UnityTest]
    public IEnumerator Sheet_FitsInsideThePaper()
    {
        yield return Build(withMap: false);
        _view.Show();

        var layout = _view.Layout;
        Assert.That(layout.Fits, Is.True);
        Assert.That(layout.Height, Is.LessThanOrEqualTo(_content.rect.height + 0.5f));
        Assert.That(layout.Cells.All(c => c.X + c.Width <= _content.rect.width + 0.5f), Is.True);
    }

    [UnityTest]
    public IEnumerator Links_OpenUnlockedEntry_HideLockedTitle_AndSkipMapWithoutIt()
    {
        yield return Build(withMap: false);
        _view.Show();

        var links = _view.Layout.Cells.Where(c => c.Style == ReportCellStyle.Link).ToList();
        Assert.That(links.Select(c => c.LinkId), Is.EqualTo(new[] { "Aberta", "Fechada" }), "sem mapa na cena, o link do mapa some");
        Assert.That(links[1].Text, Is.EqualTo(UIStrings.ModelReportLockedLink));

        _view.SelectCell(CellIndex(c => c.LinkId == "Fechada"));
        Assert.That(_popup.IsOpen, Is.False, "ficha bloqueada nao abre");

        _view.SelectCell(CellIndex(c => c.LinkId == "Aberta"));
        Assert.That(_popup.IsOpen, Is.True);
        Assert.That(_popup.Current, Is.EqualTo(_unlocked));
    }

    [UnityTest]
    public IEnumerator MapLink_HidesReportAndReopensItWhenTheMapCloses()
    {
        yield return Build(withMap: true);
        yield return null;
        _view.Show();

        _view.SelectCell(CellIndex(c => c.LinkId == ModelReportLinks.RelationMap));

        Assert.That(_map.IsOpen, Is.True);
        Assert.That(_view.IsOpen, Is.False);

        _map.Hide();

        Assert.That(_view.IsOpen, Is.True);
        Assert.That(_view.SheetId, Is.EqualTo("Primeira"));
    }

    [UnityTest]
    public IEnumerator CloseButton_HidesTheReport()
    {
        yield return Build(withMap: false);
        _view.Show();

        _close.onClick.Invoke();

        Assert.That(_view.IsOpen, Is.False);
    }
}
