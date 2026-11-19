using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class RunIntroTests
{
    private GameObject _canvas;
    private GameObject _controllerHost;
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        // A abertura le o baralho do save: nunca o save real.
        _directory = Path.Combine(Path.GetTempPath(), "metis-intro-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_directory, "run_save.json"));
        MetaProgressionManager.ResetRun();
        MetaProgressionManager.SetArchetype(CardArchetype.Automacao);
    }

    [TearDown]
    public void TearDown()
    {
        RunIntroHandoff.PlayFlyover = false;
        if (_controllerHost != null)
            Object.DestroyImmediate(_controllerHost);
        if (_canvas != null)
            Object.DestroyImmediate(_canvas);
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private static RectTransform Child(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private SlideSequenceView CreateView()
    {
        _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        var root = Child("SlideSequence", _canvas.transform);
        root.gameObject.SetActive(false);
        var view = root.gameObject.AddComponent<SlideSequenceView>();
        var area = Child("SlideArea", root);
        var back = Child("Back", area).gameObject.AddComponent<Image>();
        var front = Child("Front", area).gameObject.AddComponent<Image>();
        var band = Child("Band", root).gameObject.AddComponent<Image>();
        var caption = Child("Caption", band.transform).gameObject.AddComponent<TextMeshProUGUI>();
        var skip = Child("Skip", root).gameObject.AddComponent<Button>();

        var texture = new Texture2D(4, 4);
        var slides = Enumerable.Range(0, RunIntroController.SlideCount)
            .Select(_ => Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one / 2f)).ToArray();
        TestDataFactory.SetField(view, "_canvasRect", (RectTransform)_canvas.transform);
        TestDataFactory.SetField(view, "_slideArea", area);
        TestDataFactory.SetField(view, "_backImage", back);
        TestDataFactory.SetField(view, "_frontImage", front);
        TestDataFactory.SetField(view, "_captionBand", band);
        TestDataFactory.SetField<TMP_Text>(view, "_captionText", caption);
        TestDataFactory.SetField(view, "_skipButton", skip);
        TestDataFactory.SetField(view, "_slides", slides);
        TestDataFactory.SetField(view, "_borderColors", Enumerable.Repeat(Color.white, slides.Length).ToArray());
        root.gameObject.SetActive(true);
        return view;
    }

    private RunIntroController CreateController(SlideSequenceView view)
    {
        _controllerHost = new GameObject("RunIntro");
        _controllerHost.SetActive(false);
        var controller = _controllerHost.AddComponent<RunIntroController>();
        TestDataFactory.SetField(controller, "_view", view);
        TestDataFactory.SetField(controller, "_loadCity", false);
        _controllerHost.SetActive(true);
        return controller;
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        while (condition() == false && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    [UnityTest]
    public IEnumerator TapsAdvanceTheSlidesAndShowTheirCaptions()
    {
        var view = CreateView();
        var controller = CreateController(view);
        yield return WaitUntil(() => view.Sequence != null, 2f);

        Assert.That(view.Sequence.CurrentIndex, Is.EqualTo(0));
        Assert.That(view.CurrentCaption, Is.EqualTo(UIStrings.SlideCaption(UIStrings.IntroCaptionKey(0), MetaProgressionManager.Archetype)));

        view.Tap();
        view.Tap();
        Assert.That(view.Sequence.CurrentIndex, Is.EqualTo(1), "o primeiro toque completa a fusao e o segundo avanca");
        Assert.That(view.CurrentCaption, Is.EqualTo(UIStrings.SlideCaption(UIStrings.IntroCaptionKey(1), MetaProgressionManager.Archetype)));
        Assert.That(controller.IsDone, Is.False);
    }

    [UnityTest]
    public IEnumerator AdvancesByItselfInRealTimeWhilePaused()
    {
        var view = CreateView();
        CreateController(view);
        yield return WaitUntil(() => view.Sequence != null, 2f);

        Time.timeScale = 0f;
        try
        {
            yield return WaitUntil(() => view.Sequence.CurrentIndex > 0, 6f);
        }
        finally
        {
            Time.timeScale = 1f;
        }
        Assert.That(view.Sequence.CurrentIndex, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator SkipEntersTheCityWithTheFlyover()
    {
        var view = CreateView();
        var controller = CreateController(view);
        yield return WaitUntil(() => view.Sequence != null, 2f);

        view.Skip();
        Assert.That(controller.IsDone, Is.True);
        Assert.That(RunIntroHandoff.PlayFlyover, Is.True);
        Assert.That(view.IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator TappingThroughTheLastSlideAlsoFinishes()
    {
        var view = CreateView();
        var controller = CreateController(view);
        yield return WaitUntil(() => view.Sequence != null, 2f);

        for (var i = 0; i < RunIntroController.SlideCount * 2; i++)
            view.Tap();
        Assert.That(controller.IsDone, Is.True);
        Assert.That(RunIntroHandoff.ConsumeFlyover(), Is.True);
        Assert.That(RunIntroHandoff.PlayFlyover, Is.False, "o aviso vale uma vez so");
    }
}
