using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RoadTechGlowControllerTests
{
    private readonly List<Object> _created = new List<Object>();
    private string _saveDirectory;

    [SetUp]
    public void SetUp()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "metis-roadglow-" + System.Guid.NewGuid().ToString("N"));
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

    private (RoadTechGlowController Glow, CityStatsManager Stats, PlacementManager Placement) Build(float pesquisa)
    {
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Sem conjunto de pecas"));
        var city = new GameObject("PlacementManager");
        city.SetActive(false);
        _created.Add(city);
        var placement = city.AddComponent<PlacementManager>();
        TestDataFactory.SetField(placement, "_width", 15);
        TestDataFactory.SetField(placement, "_height", 15);
        city.SetActive(true);

        var host = new GameObject("Glow");
        host.SetActive(false);
        _created.Add(host);
        var statsManager = host.AddComponent<CityStatsManager>();
        TestDataFactory.InvokePrivateMethod(statsManager, "Reset");

        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _created.Add(material);
        var glow = host.AddComponent<RoadTechGlowController>();
        TestDataFactory.SetField(glow, "_placementManager", placement);
        TestDataFactory.SetField(glow, "_cityStatsManager", statsManager);
        TestDataFactory.SetField(glow, "_glowMaterial", material);
        TestDataFactory.SetField(glow, "_fadeSeconds", 0.3f);
        TestDataFactory.SetField(glow, "_pulseStepInterval", 0.02f);
        host.SetActive(true);
        // O Start do controlador so roda no proximo quadro, ja com a Pesquisa no lugar.
        SetPesquisa(statsManager, pesquisa);
        return (glow, statsManager, placement);
    }

    private static void SetPesquisa(CityStatsManager statsManager, float value)
    {
        var stats = statsManager.Stats;
        stats.ApplyModifier(new StatModifier { Parameter = CityParameterType.Pesquisa, Amount = value - stats.GetValue(CityParameterType.Pesquisa) });
    }

    private static IEnumerator WaitStarted(RoadTechGlowController glow)
    {
        var deadline = Time.realtimeSinceStartup + 10f;
        while (glow.IsRunning == false && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(glow.IsRunning, Is.True);
    }

    private static Renderer Layer(PlacementManager placement, string name)
    {
        return placement.transform.Find("RoadTechGlow/" + name).GetComponent<MeshRenderer>();
    }

    [UnityTest]
    public IEnumerator LowPesquisa_LeavesTheStreetsDark()
    {
        var (glow, _, placement) = Build(5f);
        yield return WaitStarted(glow);

        Assert.That(glow.CurbLevel, Is.EqualTo(0f));
        Assert.That(glow.ActivePulses, Is.EqualTo(0));
        Assert.That(Layer(placement, "Curbs").enabled, Is.False);
        Assert.That(Layer(placement, "Center").enabled, Is.False);
        Assert.That(Layer(placement, "Crossings").enabled, Is.False);
    }

    [UnityTest]
    public IEnumerator SmartCity_StartsWithEveryLayerLit()
    {
        var (glow, _, placement) = Build(80f);
        yield return WaitStarted(glow);

        Assert.That(glow.CurbLevel, Is.EqualTo(1f));
        Assert.That(glow.SmartLevel, Is.EqualTo(1f));
        Assert.That(glow.ActivePulses, Is.EqualTo(glow.PulseTarget));
        Assert.That(glow.PulseTarget, Is.GreaterThan(0));
        Assert.That(Layer(placement, "Curbs").enabled, Is.True);
        Assert.That(Layer(placement, "Center").enabled, Is.True);
        Assert.That(Layer(placement, "Crossings").enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator RisingPesquisa_LightsUpGradually_AndFallingTurnsItOff()
    {
        var (glow, stats, placement) = Build(5f);
        yield return WaitStarted(glow);

        SetPesquisa(stats, 60f);
        glow.Evaluate();
        Assert.That(glow.CurbLevel, Is.EqualTo(0f), "o brilho nao pula de uma vez");
        var deadline = Time.realtimeSinceStartup + 20f;
        while ((glow.CurbLevel < glow.CurbTarget || glow.ActivePulses < glow.PulseTarget) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(glow.CurbLevel, Is.EqualTo(glow.CurbTarget));
        Assert.That(glow.ActivePulses, Is.EqualTo(glow.PulseTarget));
        Assert.That(Layer(placement, "Center").enabled, Is.True);

        SetPesquisa(stats, 0f);
        glow.Evaluate();
        deadline = Time.realtimeSinceStartup + 30f;
        while ((glow.CurbLevel > 0f || glow.ActivePulses > 0) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(glow.ActivePulses, Is.EqualTo(0));
        Assert.That(Layer(placement, "Curbs").enabled, Is.False);
    }
}
