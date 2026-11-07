using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class PhaseMapViewTests
{
    private GameObject _root;
    private Transform _container;
    private PhaseMapView _view;
    private Sprite _unknown;
    private Sprite _themed;
    private Sprite _unavailable;

    private static Sprite Pin()
    {
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
    }

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Root", typeof(Canvas));
        _container = new GameObject("Container", typeof(RectTransform)).transform;
        _container.SetParent(_root.transform, false);

        var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(Button));
        template.transform.SetParent(_root.transform, false);
        var pin = new GameObject("Pin", typeof(RectTransform), typeof(Image));
        pin.transform.SetParent(template.transform, false);
        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(template.transform, false);
        var node = template.AddComponent<PhaseMapNodeView>();
        TestDataFactory.SetField(node, "_button", template.GetComponent<Button>());
        TestDataFactory.SetField(node, "_pin", pin.GetComponent<Image>());
        TestDataFactory.SetField(node, "_label", (TMP_Text)label);

        // Fica inativo para nao ler nem gravar a rodada.
        var host = new GameObject("View");
        host.SetActive(false);
        host.transform.SetParent(_root.transform, false);
        _view = host.AddComponent<PhaseMapView>();
        _unknown = Pin();
        _themed = Pin();
        _unavailable = Pin();
        TestDataFactory.SetField(_view, "_nodeTemplate", node);
        TestDataFactory.SetField(_view, "_nodeContainer", _container);
        TestDataFactory.SetField(_view, "_unknownPin", _unknown);
        TestDataFactory.SetField(_view, "_themedPin", _themed);
        TestDataFactory.SetField(_view, "_unavailablePin", _unavailable);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    private PhaseMapNodeView[] Shown()
    {
        return _container.GetComponentsInChildren<PhaseMapNodeView>(false);
    }

    private static Sprite PinOf(PhaseMapNodeView node)
    {
        return node.transform.Find("Pin").GetComponent<Image>().sprite;
    }

    [UnityTest]
    public IEnumerator ShowNodes_DrawsOneButtonPerDestination_WithThePinOfItsType()
    {
        _view.ShowNodes(new[]
        {
            new PhaseMapNode { Id = PhaseMapProvider.UnknownCityId, Type = PhaseMapNodeType.Unknown, IsAvailable = true },
            new PhaseMapNode { Id = "Tema", Type = PhaseMapNodeType.Themed, IsAvailable = true },
            new PhaseMapNode { Id = "Outro", Type = PhaseMapNodeType.Themed, IsAvailable = false },
        });
        yield return null;

        var nodes = Shown();
        Assert.That(nodes.Length, Is.EqualTo(3));
        Assert.That(nodes.Select(PinOf), Is.EqualTo(new[] { _unknown, _themed, _unavailable }));
        Assert.That(nodes.Select(node => node.Button.interactable), Is.EqualTo(new[] { true, true, false }));
    }

    [UnityTest]
    public IEnumerator ShowNodes_ReplacesTheDestinationsShownBefore()
    {
        var one = new[] { new PhaseMapNode { Id = PhaseMapProvider.UnknownCityId, IsAvailable = true } };
        _view.ShowNodes(one);
        yield return null;

        _view.ShowNodes(one);
        yield return null;

        Assert.That(Shown().Length, Is.EqualTo(1));
    }
}
