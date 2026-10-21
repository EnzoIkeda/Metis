using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class RelationMapViewTests
{
    private GameObject _host;
    private GameObject _canvas;
    private GameObject _viewObject;
    private string _saveDirectory;

    private CityStats _stats;
    private RelationMapView _view;
    private GameObject _panel;
    private TMP_Text _detail;
    private Transform _graph;

    [SetUp]
    public void SetUp()
    {
        // A cidade le o save ao acordar; numa pasta temporaria, pra nunca tocar na rodada de verdade.
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-playmode-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_saveDirectory, "run_save.json"));
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in new[] { _viewObject, _canvas, _host })
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_saveDirectory))
            Directory.Delete(_saveDirectory, true);
    }

    private static GameObject UiObject(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var component in components)
            go.AddComponent(component);
        return go;
    }

    private IEnumerator CreateMap()
    {
        _host = new GameObject("CityHost");
        _host.SetActive(false);
        var statsManager = _host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");
        _host.SetActive(true);
        _stats = statsManager.Stats;

        _canvas = new GameObject("Canvas", typeof(Canvas));
        _panel = UiObject("Panel", _canvas.transform);
        var graph = UiObject("Graph", _panel.transform);
        ((RectTransform)graph.transform).sizeDelta = new Vector2(940f, 860f);
        _graph = graph.transform;
        var lines = UiObject("Lines", graph.transform, typeof(CanvasRenderer), typeof(UILineGraphic));
        var template = UiObject("NodeTemplate", graph.transform, typeof(CanvasRenderer), typeof(Image), typeof(Button));
        template.SetActive(false);
        var node = template.AddComponent<RelationNodeView>();
        TestDataFactory.SetField(node, "_ring", template.GetComponent<Image>());
        TestDataFactory.SetField(node, "_button", template.GetComponent<Button>());
        template.SetActive(true);
        var detail = UiObject("Detail", _panel.transform, typeof(TextMeshProUGUI));
        _detail = detail.GetComponent<TMP_Text>();

        _viewObject = new GameObject("RelationMapController");
        _viewObject.SetActive(false);
        _view = _viewObject.AddComponent<RelationMapView>();
        TestDataFactory.SetField(_view, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(_view, "_panelRoot", _panel);
        TestDataFactory.SetField(_view, "_detailText", _detail);
        TestDataFactory.SetField(_view, "_graphArea", (RectTransform)graph.transform);
        TestDataFactory.SetField(_view, "_lines", lines.GetComponent<UILineGraphic>());
        TestDataFactory.SetField(_view, "_nodeTemplate", node);
        _viewObject.SetActive(true);

        // O Start so roda no proximo ciclo de Update; espera ate os nos existirem.
        var frames = 0;
        while (_graph.Find("NodeRenda") == null && frames++ < 60)
            yield return null;
    }

    private void Click(CityParameterType parameter)
    {
        _graph.Find($"Node{parameter}").GetComponent<Button>().onClick.Invoke();
    }

    [UnityTest]
    public IEnumerator Start_BuildsOneNodePerParameter_AndStartsHidden()
    {
        yield return CreateMap();

        foreach (CityParameterType parameter in System.Enum.GetValues(typeof(CityParameterType)))
            Assert.That(_graph.Find($"Node{parameter}"), Is.Not.Null, parameter.ToString());
        Assert.That(_panel.activeSelf, Is.False);
        Assert.That(_view.IsOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator ShowWithFocus_ListsEveryRelationThatAffectsTheParameter()
    {
        yield return CreateMap();

        _view.Show(CityParameterType.Renda);

        Assert.That(_view.IsOpen, Is.True);
        foreach (var edge in RelationGraph.Incoming(RelationGraph.Build(_stats), CityParameterType.Renda))
            Assert.That(_detail.text, Does.Contain(edge.From.GetFullName()), edge.From.ToString());
    }

    [UnityTest]
    public IEnumerator TappingASecondParameter_ShowsThePair_AndTappingTheFirstGoesBack()
    {
        yield return CreateMap();
        _view.Show(CityParameterType.Renda);
        var list = _detail.text;

        Click(CityParameterType.Populacao);
        Assert.That(_detail.text, Does.Contain(UIStrings.RelationPairTitle(CityParameterType.Populacao, CityParameterType.Renda)));
        Assert.That(_detail.text, Does.Contain(UIStrings.RelationPairTitle(CityParameterType.Renda, CityParameterType.Populacao)));

        Click(CityParameterType.Renda);
        Assert.That(_detail.text, Is.EqualTo(list));

        Click(CityParameterType.Renda);
        Assert.That(_detail.text, Is.EqualTo(UIStrings.RelationMapIntro));
    }

    [UnityTest]
    public IEnumerator ParameterChangeWhileOpen_RedrawsTheCurrentEffects()
    {
        yield return CreateMap();
        _view.Show(CityParameterType.Renda);
        var before = _detail.text;

        _stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Populacao, Amount = 40f });
        yield return null;
        yield return null;

        Assert.That(_detail.text, Is.Not.EqualTo(before));
    }
}
