using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class CityIntroFlyoverTests
{
    private GameObject _cameraHost;
    private GameObject _uiHost;
    private string _directory;

    private static readonly Vector3 RestPosition = new Vector3(1.5f, 20f, -7f);
    private const float RestSize = 19f;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "metis-flyover-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_directory, "run_save.json"));
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        RunIntroHandoff.PlayFlyover = false;
        foreach (var host in new[] { _uiHost, _cameraHost })
        {
            if (host != null)
                Object.DestroyImmediate(host);
        }
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private sealed class Scene
    {
        public Camera Camera;
        public CameraZoomController Zoom;
        public CityIntroFlyover Flyover;
        public CanvasGroup Hud;
        public GameObject SkipPanel;
        public Button SkipButton;
        public WelcomePopupView Welcome;
        public GameObject WelcomePanel;
    }

    private Scene Create(float duration = 0.6f)
    {
        var scene = new Scene();
        _cameraHost = new GameObject("Camera");
        _cameraHost.transform.SetPositionAndRotation(RestPosition, Quaternion.Euler(30f, 45f, 0f));
        scene.Camera = _cameraHost.AddComponent<Camera>();
        scene.Camera.orthographic = true;
        scene.Camera.orthographicSize = RestSize;
        scene.Zoom = _cameraHost.AddComponent<CameraZoomController>();

        _uiHost = new GameObject("UI");
        _uiHost.SetActive(false);
        var hud = new GameObject("Hud");
        hud.transform.SetParent(_uiHost.transform);
        scene.Hud = hud.AddComponent<CanvasGroup>();
        scene.SkipPanel = new GameObject("SkipPanel");
        scene.SkipPanel.transform.SetParent(_uiHost.transform);
        scene.SkipButton = scene.SkipPanel.AddComponent<Button>();
        scene.WelcomePanel = new GameObject("WelcomePanel");
        scene.WelcomePanel.transform.SetParent(_uiHost.transform);

        scene.Flyover = _uiHost.AddComponent<CityIntroFlyover>();
        TestDataFactory.SetField(scene.Flyover, "_camera", scene.Camera);
        TestDataFactory.SetField(scene.Flyover, "_cameraControls", new Behaviour[] { scene.Zoom });
        TestDataFactory.SetField(scene.Flyover, "_hiddenGroups", new[] { scene.Hud });
        TestDataFactory.SetField(scene.Flyover, "_skipPanel", scene.SkipPanel);
        TestDataFactory.SetField(scene.Flyover, "_skipButton", scene.SkipButton);
        TestDataFactory.SetField(scene.Flyover, "_duration", duration);
        TestDataFactory.SetField(scene.Flyover, "_revealDuration", 0.1f);

        scene.Welcome = _uiHost.AddComponent<WelcomePopupView>();
        TestDataFactory.SetField(scene.Welcome, "_panelRoot", scene.WelcomePanel);
        TestDataFactory.SetField(scene.Welcome, "_flyover", scene.Flyover);
        _uiHost.SetActive(true);
        return scene;
    }

    private static void AssertAtRest(Camera camera)
    {
        Assert.That(camera.transform.position, Is.EqualTo(RestPosition), "a camera volta exatamente ao enquadramento da cena");
        Assert.That(camera.orthographicSize, Is.EqualTo(RestSize));
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds = 5f)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        while (condition() == false && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    [UnityTest]
    public IEnumerator WithoutTheMark_NoFlyoverAndWelcomeOpensRightAway()
    {
        var scene = Create();
        yield return null;

        Assert.That(scene.Flyover.IsPlaying, Is.False);
        Assert.That(scene.SkipPanel.activeSelf, Is.False);
        Assert.That(scene.WelcomePanel.activeSelf, Is.True);
        Assert.That(scene.Zoom.enabled, Is.True);
        Assert.That(scene.Hud.alpha, Is.EqualTo(1f));
        AssertAtRest(scene.Camera);
    }

    [UnityTest]
    public IEnumerator WithTheMark_FliesFromCloseAndLandsOnTheRestFramingBeforeTheWelcome()
    {
        RunIntroHandoff.PlayFlyover = true;
        var scene = Create();
        Assert.That(RunIntroHandoff.PlayFlyover, Is.False, "a marca e de uso unico");
        yield return null;

        Assert.That(scene.Flyover.IsPlaying, Is.True);
        Assert.That(scene.Camera.orthographicSize, Is.LessThan(RestSize - 1f), "comeca de perto");
        Assert.That(scene.WelcomePanel.activeSelf, Is.False, "as boas-vindas esperam o sobrevoo");
        Assert.That(scene.SkipPanel.activeSelf, Is.True);
        Assert.That(scene.Zoom.enabled, Is.False);
        Assert.That(scene.Hud.alpha, Is.EqualTo(0f));
        Assert.That(scene.Hud.blocksRaycasts, Is.False);

        yield return WaitUntil(() => scene.Flyover.IsPlaying == false);
        Assert.That(scene.Flyover.IsPlaying, Is.False, "o sobrevoo termina sozinho");
        AssertAtRest(scene.Camera);
        Assert.That(scene.WelcomePanel.activeSelf, Is.True);
        Assert.That(scene.SkipPanel.activeSelf, Is.False);
        Assert.That(scene.Zoom.enabled, Is.True);
        Assert.That(scene.Hud.blocksRaycasts, Is.True);

        yield return WaitUntil(() => scene.Hud.alpha >= 1f);
        Assert.That(scene.Hud.alpha, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator Tap_SkipsToTheEndAndOpensTheWelcome()
    {
        RunIntroHandoff.PlayFlyover = true;
        var scene = Create(duration: 30f);
        yield return null;
        yield return null;

        scene.SkipButton.onClick.Invoke();
        Assert.That(scene.Flyover.IsPlaying, Is.False);
        AssertAtRest(scene.Camera);
        Assert.That(scene.WelcomePanel.activeSelf, Is.True);
        Assert.That(scene.Zoom.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator RunsInRealTimeWhilePaused()
    {
        RunIntroHandoff.PlayFlyover = true;
        Time.timeScale = 0f;
        try
        {
            var scene = Create(duration: 0.3f);
            yield return WaitUntil(() => scene.Flyover.IsPlaying == false);
            Assert.That(scene.Flyover.IsPlaying, Is.False);
            AssertAtRest(scene.Camera);
        }
        finally
        {
            Time.timeScale = 1f;
        }
    }
}
