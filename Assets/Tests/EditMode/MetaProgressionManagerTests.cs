using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MetaProgressionManagerTests
{
    private string _directory;
    private string _path;

    [SetUp]
    public void SetUp()
    {
        // Save numa pasta temporaria, pra nunca apagar a rodada de verdade de quem roda os testes.
        _directory = Path.Combine(Path.GetTempPath(), "metis-editmode-" + System.Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "run_save.json");
        SetStaticField("_savePathOverride", _path);
        MetaProgressionManager.ResetRun();
        MetaProgressionManager.ConsumeDiscardedSaveNotice();
    }

    [TearDown]
    public void TearDown()
    {
        MetaProgressionManager.ResetRun();
        SetStaticField("_savePathOverride", null);
        SetStaticField("_save", null);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private static void SetStaticField(string name, object value)
    {
        var field = typeof(MetaProgressionManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        field.SetValue(null, value);
    }

    // Zera o cache estatico em memoria pra forcar a proxima leitura a vir do arquivo, provando a ida e volta pelo JSON.
    private static void ForceReloadFromFile()
    {
        SetStaticField("_save", null);
    }

    [Test]
    public void FreshRun_HasDefaultsAndIsNotANewPhase()
    {
        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Geral));
        Assert.That(MetaProgressionManager.LoadedCardNames, Is.Empty);
        Assert.That(MetaProgressionManager.LoadedAdvantageNames, Is.Empty);
        Assert.That(MetaProgressionManager.PhasesCompleted, Is.EqualTo(0));
        Assert.That(MetaProgressionManager.IsNewPhase, Is.False);
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.False);
        Assert.That(MetaProgressionManager.SavedPhase, Is.Null);
    }

    [Test]
    public void SetArchetype_PersistsAcrossReload_AndStartsARun()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Automacao);

        ForceReloadFromFile();

        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Automacao));
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.True);
        Assert.That(MetaProgressionManager.ContinueSceneName, Is.EqualTo(MetaProgressionManager.CitySceneName));
    }

    [Test]
    public void AddCardReward_DeduplicatesByName()
    {
        MetaProgressionManager.AddCardReward("CartaX");
        MetaProgressionManager.AddCardReward("CartaX");

        Assert.That(MetaProgressionManager.LoadedCardNames, Has.Exactly(1).EqualTo("CartaX"));
    }

    [Test]
    public void AddAdvantageReward_DeduplicatesByName_AndPersists()
    {
        MetaProgressionManager.AddAdvantageReward("VantagemY");
        MetaProgressionManager.AddAdvantageReward("VantagemY");

        ForceReloadFromFile();

        Assert.That(MetaProgressionManager.LoadedAdvantageNames, Has.Exactly(1).EqualTo("VantagemY"));
    }

    [Test]
    public void IncrementPhasesCompleted_MakesIsNewPhaseTrue()
    {
        Assert.That(MetaProgressionManager.IsNewPhase, Is.False);

        MetaProgressionManager.IncrementPhasesCompleted();

        Assert.That(MetaProgressionManager.PhasesCompleted, Is.EqualTo(1));
        Assert.That(MetaProgressionManager.IsNewPhase, Is.True);
    }

    [Test]
    public void ResetRun_ClearsEverythingAndDeletesTheFile()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Industria);
        MetaProgressionManager.AddCardReward("Carta");
        MetaProgressionManager.AddAdvantageReward("Vantagem");
        MetaProgressionManager.IncrementPhasesCompleted();
        MetaProgressionManager.SavePhase(new PhaseSaveData { TurnIndex = 3 });

        MetaProgressionManager.ResetRun();

        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Geral));
        Assert.That(MetaProgressionManager.LoadedCardNames, Is.Empty);
        Assert.That(MetaProgressionManager.LoadedAdvantageNames, Is.Empty);
        Assert.That(MetaProgressionManager.PhasesCompleted, Is.EqualTo(0));
        Assert.That(MetaProgressionManager.SavedPhase, Is.Null);
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.False);
        Assert.That(File.Exists(_path), Is.False);
    }

    [Test]
    public void FullState_RoundTripsThroughFileAfterReload()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Sustentabilidade);
        MetaProgressionManager.AddCardReward("CartaA");
        MetaProgressionManager.AddAdvantageReward("VantagemA");
        MetaProgressionManager.IncrementPhasesCompleted();
        MetaProgressionManager.IncrementPhasesCompleted();

        ForceReloadFromFile();

        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Sustentabilidade));
        Assert.That(MetaProgressionManager.LoadedCardNames, Is.EquivalentTo(new[] { "CartaA" }));
        Assert.That(MetaProgressionManager.LoadedAdvantageNames, Is.EquivalentTo(new[] { "VantagemA" }));
        Assert.That(MetaProgressionManager.PhasesCompleted, Is.EqualTo(2));
    }

    // Estados dos geradores usam os 64 bits inteiros; o JSON nao pode arredondar.
    [Test]
    public void SavedPhase_RoundTripsEveryFieldThroughFile()
    {
        var phase = new PhaseSaveData
        {
            TurnIndex = 12,
            AtEvent = true,
            PendingEventId = "EventoZ",
            PresetName = "PresetSeco",
            LayoutSeed = 0xFEDCBA9876543210UL,
            BackgroundIndex = 7,
            AnchorBonus = -4.5f,
            IsRevealed = true,
            RevealedUntilPhaseEnd = true,
            HandRandomState = ulong.MaxValue - 3,
            EventRandomState = 0x8000000000000001UL,
        };
        phase.Parameters.Add(new ParameterValueSave { Parameter = CityParameterType.Renda, Value = 33.25f });
        phase.Hand.AddRange(new[] { "A", "B" });
        phase.DrawPile.AddRange(new[] { "C", "A", "D" });
        phase.DiscardPile.Add("E");

        MetaProgressionManager.SavePhase(phase);
        ForceReloadFromFile();
        var loaded = MetaProgressionManager.SavedPhase;

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.TurnIndex, Is.EqualTo(12));
        Assert.That(loaded.AtEvent, Is.True);
        Assert.That(loaded.PendingEventId, Is.EqualTo("EventoZ"));
        Assert.That(loaded.PresetName, Is.EqualTo("PresetSeco"));
        Assert.That(loaded.LayoutSeed, Is.EqualTo(0xFEDCBA9876543210UL));
        Assert.That(loaded.BackgroundIndex, Is.EqualTo(7));
        Assert.That(loaded.AnchorBonus, Is.EqualTo(-4.5f));
        Assert.That(loaded.IsRevealed, Is.True);
        Assert.That(loaded.RevealedUntilPhaseEnd, Is.True);
        Assert.That(loaded.HandRandomState, Is.EqualTo(ulong.MaxValue - 3));
        Assert.That(loaded.EventRandomState, Is.EqualTo(0x8000000000000001UL));
        Assert.That(loaded.Parameters, Has.Count.EqualTo(1));
        Assert.That(loaded.Parameters[0].Parameter, Is.EqualTo(CityParameterType.Renda));
        Assert.That(loaded.Parameters[0].Value, Is.EqualTo(33.25f));
        Assert.That(loaded.Hand, Is.EqualTo(new[] { "A", "B" }));
        Assert.That(loaded.DrawPile, Is.EqualTo(new[] { "C", "A", "D" }));
        Assert.That(loaded.DiscardPile, Is.EqualTo(new[] { "E" }));
    }

    [Test]
    public void EnterPhaseMap_ClearsPhaseAndContinuesFromTheMap()
    {
        MetaProgressionManager.SavePhase(new PhaseSaveData { TurnIndex = 20 });

        MetaProgressionManager.EnterPhaseMap();
        ForceReloadFromFile();

        Assert.That(MetaProgressionManager.SavedPhase, Is.Null);
        Assert.That(MetaProgressionManager.ContinueSceneName, Is.EqualTo(MetaProgressionManager.PhaseMapSceneName));
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.True);
    }

    [Test]
    public void IncrementPhasesCompleted_LeavesTheMapForAFreshPhase()
    {
        MetaProgressionManager.EnterPhaseMap();

        MetaProgressionManager.IncrementPhasesCompleted();

        Assert.That(MetaProgressionManager.ContinueSceneName, Is.EqualTo(MetaProgressionManager.CitySceneName));
        Assert.That(MetaProgressionManager.SavedPhase, Is.Null);
    }

    [Test]
    public void DiscardSavedPhase_KeepsTheRunButDropsThePhase()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Industria);
        MetaProgressionManager.SavePhase(new PhaseSaveData { TurnIndex = 5 });

        MetaProgressionManager.DiscardSavedPhase();
        ForceReloadFromFile();

        Assert.That(MetaProgressionManager.SavedPhase, Is.Null);
        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Industria));
        Assert.That(MetaProgressionManager.HasRunInProgress, Is.True);
    }

    [Test]
    public void IncompatibleVersion_IsDiscardedWithNoticeOnce()
    {
        var old = new RunSaveData { Version = RunSaveData.CurrentVersion + 1 };
        old.Meta.PhasesCompleted = 4;
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, JsonUtility.ToJson(old));
        ForceReloadFromFile();

        Assert.That(MetaProgressionManager.HasRunInProgress, Is.False);
        Assert.That(MetaProgressionManager.PhasesCompleted, Is.EqualTo(0));
        Assert.That(File.Exists(_path), Is.False);
        Assert.That(MetaProgressionManager.ConsumeDiscardedSaveNotice(), Is.True);
        Assert.That(MetaProgressionManager.ConsumeDiscardedSaveNotice(), Is.False);
    }

    [Test]
    public void CorruptedFile_IsDiscardedWithNotice()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ isto nao e json");
        ForceReloadFromFile();

        Assert.That(MetaProgressionManager.HasRunInProgress, Is.False);
        Assert.That(MetaProgressionManager.ConsumeDiscardedSaveNotice(), Is.True);
    }
}
