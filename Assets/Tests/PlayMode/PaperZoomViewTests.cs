using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Zoom da folha do relatorio: roda do mouse amplia, arrasto move so com zoom e volta pra folha inteira.
public class PaperZoomViewTests
{
    private GameObject _canvas;
    private PaperZoomView _zoom;
    private RectTransform _viewport;
    private RectTransform _content;

    [TearDown]
    public void TearDown()
    {
        if (_canvas != null)
            Object.DestroyImmediate(_canvas);
    }

    private IEnumerator Build()
    {
        _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.SetActive(false);
        _viewport = (RectTransform)new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).transform;
        _viewport.SetParent(_canvas.transform, false);
        _viewport.anchorMin = _viewport.anchorMax = new Vector2(0.5f, 0.5f);
        _viewport.sizeDelta = new Vector2(200f, 300f);
        _content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
        _content.SetParent(_viewport, false);
        _zoom = _viewport.gameObject.AddComponent<PaperZoomView>();
        TestDataFactory.SetField(_zoom, "_viewport", _viewport);
        TestDataFactory.SetField(_zoom, "_content", _content);
        _canvas.SetActive(true);
        yield return null;
    }

    private Vector2 Center => RectTransformUtility.WorldToScreenPoint(null, _viewport.position);

    private PointerEventData Pointer(int id = 0)
    {
        return new PointerEventData(EventSystem.current) { pointerId = id, position = Center };
    }

    [UnityTest]
    public IEnumerator ScrollUp_ZoomsAroundThePointer()
    {
        yield return Build();

        var scroll = Pointer();
        scroll.scrollDelta = new Vector2(0f, 20f);
        _zoom.OnScroll(scroll);

        Assert.That(_zoom.Scale, Is.GreaterThan(1f));
        Assert.That(_content.localScale.x, Is.EqualTo(_zoom.Scale).Within(1e-4));
        // O meio da janela continua mostrando o meio da folha.
        var middle = _content.TransformPoint(new Vector3(100f, -150f, 0f));
        Assert.That(Vector2.Distance(middle, _viewport.TransformPoint(_viewport.rect.center)), Is.LessThan(0.5f));

        scroll.scrollDelta = new Vector2(0f, -40f);
        for (var i = 0; i < 5; i++)
            _zoom.OnScroll(scroll);
        Assert.That(_zoom.Scale, Is.EqualTo(1f), "rolando pra baixo volta a folha inteira e para");
    }

    [UnityTest]
    public IEnumerator Drag_MovesOnlyWhenZoomed_AndNotWithTwoFingers()
    {
        yield return Build();

        var drag = Pointer();
        drag.delta = new Vector2(-30f, 20f);
        _zoom.OnBeginDrag(drag);
        _zoom.OnDrag(drag);
        Assert.That(_content.anchoredPosition, Is.EqualTo(Vector2.zero), "sem zoom a folha fica parada");

        _zoom.ZoomAt(Center, null, 2f);
        var before = _content.anchoredPosition;
        _zoom.OnDrag(drag);
        Assert.That(_content.anchoredPosition.x, Is.LessThan(before.x));
        Assert.That(_content.anchoredPosition.y, Is.GreaterThan(before.y));

        var second = Pointer(1);
        _zoom.OnBeginDrag(second);
        before = _content.anchoredPosition;
        _zoom.OnDrag(drag);
        Assert.That(_content.anchoredPosition, Is.EqualTo(before), "com dois dedos quem move e a pinca");
        _zoom.OnEndDrag(second);
        _zoom.OnEndDrag(drag);
    }

    [UnityTest]
    public IEnumerator ResetZoom_ShowsTheWholePageAgain()
    {
        yield return Build();
        _zoom.ZoomAt(Center + new Vector2(50f, 50f), null, 3f);

        _zoom.ResetZoom();

        Assert.That(_zoom.Scale, Is.EqualTo(1f));
        Assert.That(_content.localScale, Is.EqualTo(Vector3.one));
        Assert.That(_content.anchoredPosition, Is.EqualTo(Vector2.zero));
    }
}
