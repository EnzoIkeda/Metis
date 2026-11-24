using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class CityWalkControllerTests
{
    private readonly List<Object> _assets = new List<Object>();
    private readonly List<GameObject> _scene = new List<GameObject>();
    private string _saveDirectory;
    private bool _fog;
    private AmbientMode _ambientMode;

    private class Rig
    {
        public TurnManager Turns;
        public PlacementManager Placement;
        public CityWalkController Walk;
        public Camera Main;
        public Camera WalkCamera;
        public Behaviour Control;
        public CanvasGroup Group;
        public Button WalkButton;
        public GameObject Overlay;
        public GameObject Blocker;
        public WalkStickView Stick;
    }

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-walk-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_saveDirectory, "run_save.json"));
        MetaProgressionManager.ResetRun();
        MetaProgressionManager.SetArchetype(CardArchetype.Sustentabilidade);
        _fog = RenderSettings.fog;
        _ambientMode = RenderSettings.ambientMode;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _scene)
            if (host != null)
                Object.DestroyImmediate(host);
        _scene.Clear();
        foreach (var asset in _assets)
            if (asset != null)
                Object.DestroyImmediate(asset);
        _assets.Clear();
        RenderSettings.fog = _fog;
        RenderSettings.ambientMode = _ambientMode;
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_saveDirectory))
            Directory.Delete(_saveDirectory, true);
    }

    private GameObject Host(string name, bool active = true)
    {
        var go = new GameObject(name);
        go.SetActive(active);
        _scene.Add(go);
        return go;
    }

    private Rig Build()
    {
        var rig = new Rig();
        var city = Host("PlacementManager", false);
        rig.Placement = city.AddComponent<PlacementManager>();
        TestDataFactory.SetField(rig.Placement, "_width", 15);
        TestDataFactory.SetField(rig.Placement, "_height", 15);
        city.SetActive(true);

        var phase = Host("Phase", false);
        var statsManager = phase.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");
        var effects = phase.AddComponent<CityEffectsController>();
        rig.Turns = phase.AddComponent<TurnManager>();
        var card = TestDataFactory.CreateCard("CardWalkTest");
        _assets.Add(card);
        TestDataFactory.SetField(rig.Turns, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(rig.Turns, "_placementManager", rig.Placement);
        TestDataFactory.SetField(rig.Turns, "_cardPool", new[] { card });
        TestDataFactory.SetField(rig.Turns, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(rig.Turns, "_advantagePool", new PassiveAdvantageData[0]);
        TestDataFactory.SetField(rig.Turns, "_effects", effects);
        TestDataFactory.SetField(rig.Turns, "_handSize", 1);
        phase.SetActive(true);

        var main = Host("MainCamera");
        rig.Main = main.AddComponent<Camera>();
        rig.Main.orthographic = true;
        rig.Control = main.AddComponent<CameraPanController>();
        var walkCamera = Host("WalkCamera");
        rig.WalkCamera = walkCamera.AddComponent<Camera>();
        rig.Group = Host("Hud").AddComponent<CanvasGroup>();
        rig.WalkButton = Host("ButtonWalk").AddComponent<Button>();
        rig.Overlay = Host("WalkOverlay");
        rig.Stick = Host("Stick").AddComponent<WalkStickView>();
        rig.Stick.transform.SetParent(rig.Overlay.transform);
        rig.Blocker = Host("Popup", false);

        var walkHost = Host("CityWalk", false);
        rig.Walk = walkHost.AddComponent<CityWalkController>();
        TestDataFactory.SetField(rig.Walk, "_turnManager", rig.Turns);
        TestDataFactory.SetField(rig.Walk, "_placementManager", rig.Placement);
        TestDataFactory.SetField(rig.Walk, "_mainCamera", rig.Main);
        TestDataFactory.SetField(rig.Walk, "_walkCamera", rig.WalkCamera);
        TestDataFactory.SetField(rig.Walk, "_cameraControls", new Behaviour[] { rig.Control });
        TestDataFactory.SetField(rig.Walk, "_walkButton", rig.WalkButton);
        TestDataFactory.SetField(rig.Walk, "_hiddenGroups", new[] { rig.Group });
        TestDataFactory.SetField(rig.Walk, "_overlay", rig.Overlay);
        TestDataFactory.SetField(rig.Walk, "_stick", rig.Stick);
        TestDataFactory.SetField(rig.Walk, "_blockingPanels", new[] { rig.Blocker });
        TestDataFactory.SetField(rig.Walk, "_pollSeconds", 0.01f);
        TestDataFactory.SetField(rig.Walk, "_bodyRadius", 0.06f);
        walkHost.SetActive(true);
        return rig;
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds = 5f)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        while (condition() == false && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    private static void SetStick(WalkStickView stick, Vector2 value)
    {
        typeof(WalkStickView).GetField("<Value>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(stick, value);
    }

    [UnityTest]
    public IEnumerator EnterAndExit_RestoreCameraHudAndLighting()
    {
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        var rig = Build();
        rig.Group.alpha = 1f;
        rig.Group.interactable = true;
        rig.Group.blocksRaycasts = true;
        yield return WaitUntil(() => rig.Walk.CanWalk());
        Assert.That(rig.Overlay.activeSelf, Is.False);
        Assert.That(rig.WalkCamera.enabled, Is.False);

        rig.Walk.Enter();
        Assert.That(rig.Walk.IsWalking, Is.True);
        Assert.That(rig.Main.enabled, Is.False);
        Assert.That(rig.WalkCamera.enabled, Is.True);
        Assert.That(rig.WalkCamera.orthographic, Is.False);
        Assert.That(rig.Control.enabled, Is.False);
        Assert.That(rig.Group.alpha, Is.EqualTo(0f));
        Assert.That(rig.Group.interactable, Is.False);
        Assert.That(rig.Overlay.activeSelf, Is.True);
        Assert.That(rig.WalkButton.gameObject.activeSelf, Is.False);
        Assert.That(RenderSettings.fog, Is.True);
        Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
        var pose = rig.Walk.Pose;
        Assert.That(rig.Placement.Plan.IsRoad((int)pose.X, (int)pose.Z), Is.True);

        rig.Walk.Exit();
        Assert.That(rig.Walk.IsWalking, Is.False);
        Assert.That(rig.Main.enabled, Is.True);
        Assert.That(rig.WalkCamera.enabled, Is.False);
        Assert.That(rig.Control.enabled, Is.True);
        Assert.That(rig.Group.alpha, Is.EqualTo(1f));
        Assert.That(rig.Group.interactable, Is.True);
        Assert.That(rig.Group.blocksRaycasts, Is.True);
        Assert.That(rig.Overlay.activeSelf, Is.False);
        Assert.That(rig.WalkButton.gameObject.activeSelf, Is.True);
        Assert.That(RenderSettings.fog, Is.False);
        Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Skybox));
    }

    [UnityTest]
    public IEnumerator TheButton_HidesWhileAPopupIsOpen()
    {
        var rig = Build();
        yield return WaitUntil(() => rig.WalkButton.gameObject.activeSelf);
        Assert.That(rig.WalkButton.gameObject.activeSelf, Is.True);

        rig.Blocker.SetActive(true);
        yield return WaitUntil(() => rig.WalkButton.gameObject.activeSelf == false);
        Assert.That(rig.WalkButton.gameObject.activeSelf, Is.False);
        rig.Walk.Enter();
        Assert.That(rig.Walk.IsWalking, Is.False, "com popup aberto o passeio nao comeca");

        rig.Blocker.SetActive(false);
        yield return WaitUntil(() => rig.WalkButton.gameObject.activeSelf);
        Assert.That(rig.WalkButton.gameObject.activeSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator WalkingInEveryDirection_NeverLeavesTheStreets()
    {
        var rig = Build();
        yield return WaitUntil(() => rig.Walk.CanWalk());
        rig.Walk.Enter();
        var plan = rig.Placement.Plan;
        var start = rig.Walk.Pose;
        var moved = false;
        foreach (var direction in new[] { Vector2.up, Vector2.right, new Vector2(-1f, 1f), Vector2.down })
        {
            SetStick(rig.Stick, direction);
            var until = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < until)
            {
                var pose = rig.Walk.Pose;
                Assert.That(plan.IsRoad(StreetWalk.CellOf(pose.X), StreetWalk.CellOf(pose.Z)), Is.True, $"{pose.X},{pose.Z}");
                moved |= Mathf.Abs(pose.X - start.X) + Mathf.Abs(pose.Z - start.Z) > 0.05f;
                yield return null;
            }
        }
        SetStick(rig.Stick, Vector2.zero);
        Assert.That(moved, Is.True);
    }

    [UnityTest]
    public IEnumerator LeavingTheSceneMidWalk_RestoresTheFog()
    {
        RenderSettings.fog = false;
        var rig = Build();
        yield return WaitUntil(() => rig.Walk.CanWalk());
        rig.Walk.Enter();
        Assert.That(RenderSettings.fog, Is.True);
        Object.DestroyImmediate(rig.Walk.gameObject);
        Assert.That(RenderSettings.fog, Is.False);
    }
}
