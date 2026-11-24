using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RoadTrafficControllerTests
{
    private readonly List<Object> _created = new List<Object>();
    private string _saveDirectory;

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-traffic-" + System.Guid.NewGuid().ToString("N"));
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", Path.Combine(_saveDirectory, "run_save.json"));
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            if (obj != null)
                Object.DestroyImmediate(obj);
        _created.Clear();
        MetaProgressionManager.ResetRun();
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_savePathOverride", null);
        TestDataFactory.SetStaticField(typeof(MetaProgressionManager), "_save", null);
        if (Directory.Exists(_saveDirectory))
            Directory.Delete(_saveDirectory, true);
    }

    private GameObject Template(string name)
    {
        var template = new GameObject(name);
        template.SetActive(false);
        _created.Add(template);
        return template;
    }

    private PlacementManager City()
    {
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Sem conjunto de pecas"));
        var host = new GameObject("PlacementManager");
        host.SetActive(false);
        _created.Add(host);
        var placement = host.AddComponent<PlacementManager>();
        TestDataFactory.SetField(placement, "_width", 15);
        TestDataFactory.SetField(placement, "_height", 15);
        host.SetActive(true);
        return placement;
    }

    private (TurnManager Turns, CityStatsManager Stats, CarTrafficController Cars, PedestrianTrafficController People) Phase(float population, float mobility, CardData card)
    {
        var placement = City();
        var host = new GameObject("Phase");
        host.SetActive(false);
        _created.Add(host);
        var statsManager = host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");
        var effects = host.AddComponent<CityEffectsController>();
        var turnManager = host.AddComponent<TurnManager>();
        TestDataFactory.SetField(turnManager, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(turnManager, "_cardPool", new[] { card });
        TestDataFactory.SetField(turnManager, "_eventPool", new RandomEventData[0]);
        TestDataFactory.SetField(turnManager, "_advantagePool", new PassiveAdvantageData[0]);
        TestDataFactory.SetField(turnManager, "_effects", effects);
        TestDataFactory.SetField(turnManager, "_handSize", 1);

        var cars = host.AddComponent<CarTrafficController>();
        TestDataFactory.SetField(cars, "_carPrefabs", new[] { Template("Car") });
        var people = host.AddComponent<PedestrianTrafficController>();
        TestDataFactory.SetField(people, "_pedestrianPrefabs", new[] { Template("Person") });
        foreach (RoadTrafficController controller in new RoadTrafficController[] { cars, people })
        {
            SetBase(controller, "_placementManager", placement);
            SetBase(controller, "_turnManager", turnManager);
            SetBase(controller, "_cityStatsManager", statsManager);
        }
        host.SetActive(true);
        Set(statsManager.Stats, population, mobility);
        return (turnManager, statsManager, cars, people);
    }

    // Os campos ficam na base comum, que o auxiliar de reflexao nao procura.
    private static void SetBase(RoadTrafficController controller, string field, object value)
    {
        typeof(RoadTrafficController)
            .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(controller, value);
    }

    private static void Set(CityStats stats, float population, float mobility)
    {
        var values = new Dictionary<CityParameterType, float>();
        foreach (CityParameterType parameter in System.Enum.GetValues(typeof(CityParameterType)))
            values[parameter] = parameter == CityParameterType.Populacao ? population : parameter == CityParameterType.Mobilidade ? mobility : 55f;
        stats.RestoreState(values, stats.AnchorBonus);
        stats.RecomputeDerivedParameters();
    }

    private static IEnumerator PlayTurn(TurnManager turnManager, CardData card)
    {
        var turn = turnManager.Machine.TurnIndex;
        Assert.That(turnManager.PlayCard(card), Is.True);
        var deadline = Time.realtimeSinceStartup + 6f;
        while (turnManager.Machine.TurnIndex == turn && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(turnManager.Machine.TurnIndex, Is.EqualTo(turn + 1));
    }

    private static IEnumerator WaitSettled(RoadTrafficController controller, float seconds = 30f)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        while ((controller.ActiveCount != controller.TargetCount || ActiveChildren(controller) != controller.TargetCount) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(controller.ActiveCount, Is.EqualTo(controller.TargetCount));
        Assert.That(ActiveChildren(controller), Is.EqualTo(controller.TargetCount));
    }

    private static Transform Container(RoadTrafficController controller)
    {
        var placement = (PlacementManager)typeof(RoadTrafficController)
            .GetField("_placementManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(controller);
        return placement.transform.Find(controller is CarTrafficController ? "Cars" : "Pedestrians");
    }

    private static int ActiveChildren(RoadTrafficController controller)
    {
        var count = 0;
        foreach (Transform child in Container(controller))
            if (child.gameObject.activeSelf)
                count++;
        return count;
    }

    [UnityTest]
    public IEnumerator PhaseLoad_FillsTheStreetsAtOnce_FromThePopulation()
    {
        var card = TestDataFactory.CreateCard();
        var phase = Phase(80f, 60f, card);
        // Os valores entram depois do Awake, antes do Start dos controladores.
        yield return null;
        yield return null;

        Assert.That(phase.Cars.IsRunning, Is.True);
        Assert.That(phase.Cars.TargetCount, Is.EqualTo(TrafficDensity.ActorCount(80f, 4, 14)));
        Assert.That(phase.Cars.ActiveCount, Is.EqualTo(phase.Cars.TargetCount), "sem entrada aos poucos ao carregar");
        Assert.That(phase.People.TargetCount, Is.EqualTo(TrafficDensity.PedestrianCount(80f, 60f, 4, 18)));
        Assert.That(phase.People.ActiveCount, Is.EqualTo(phase.People.TargetCount));
    }

    [UnityTest]
    public IEnumerator TurnThatChangesPopulation_ResizesTheTraffic_ReusingActors()
    {
        var card = TestDataFactory.CreateCard();
        var phase = Phase(90f, 60f, card);
        yield return null;
        yield return null;
        var full = phase.Cars.ActiveCount;
        var created = Container(phase.Cars).childCount;
        Assert.That(full, Is.EqualTo(TrafficDensity.ActorCount(90f, 4, 14)));

        Set(phase.Stats.Stats, 5f, 60f);
        yield return PlayTurn(phase.Turns, card);
        var population = phase.Stats.Stats.GetValue(CityParameterType.Populacao);
        Assert.That(phase.Cars.TargetCount, Is.EqualTo(TrafficDensity.ActorCount(population, 4, 14)));
        Assert.That(phase.Cars.TargetCount, Is.LessThan(full));
        yield return WaitSettled(phase.Cars);
        yield return WaitSettled(phase.People);

        Set(phase.Stats.Stats, 90f, 60f);
        yield return PlayTurn(phase.Turns, card);
        Assert.That(phase.Cars.TargetCount, Is.GreaterThan(TrafficDensity.ActorCount(population, 4, 14)));
        yield return WaitSettled(phase.Cars);
        Assert.That(Container(phase.Cars).childCount, Is.LessThanOrEqualTo(Mathf.Max(created, phase.Cars.TargetCount)), "quem saiu volta antes de criar outro");
    }

    [UnityTest]
    public IEnumerator LowMobility_SlowsTheCars_ButNotThePedestrians()
    {
        var card = TestDataFactory.CreateCard();
        var phase = Phase(50f, 0f, card);
        yield return null;
        yield return null;

        Assert.That(phase.Cars.SpeedFactor, Is.EqualTo(TrafficDensity.SlowestSpeedFactor));
        Assert.That(phase.People.SpeedFactor, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator WithoutStats_TrafficStillRuns_AtTheMiddleOfTheRange()
    {
        var placement = City();
        var host = new GameObject("Traffic");
        host.SetActive(false);
        _created.Add(host);
        var cars = host.AddComponent<CarTrafficController>();
        TestDataFactory.SetField(cars, "_carPrefabs", new[] { Template("Car") });
        SetBase(cars, "_placementManager", placement);
        host.SetActive(true);
        yield return null;

        Assert.That(cars.TargetCount, Is.EqualTo(TrafficDensity.ActorCount(50f, 4, 14)));
        Assert.That(cars.ActiveCount, Is.EqualTo(cars.TargetCount));
        Assert.That(cars.SpeedFactor, Is.EqualTo(1f));
    }
}
