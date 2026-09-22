using System.Reflection;
using NUnit.Framework;

public class MetaProgressionManagerTests
{
    [SetUp]
    public void SetUp()
    {
        MetaProgressionManager.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        // PlayerPrefs e persistencia de verdade (registro/plist), nao deixar estado de teste vazando.
        MetaProgressionManager.ResetRun();
    }

    // Zera o cache estatico em memoria pra forcar a proxima leitura a vir do PlayerPrefs de novo, provando que o round-trip de serializacao funciona, nao so o objeto em memoria.
    private static void ForceReloadFromPlayerPrefs()
    {
        var field = typeof(MetaProgressionManager).GetField("_state", BindingFlags.NonPublic | BindingFlags.Static);
        field.SetValue(null, null);
    }

    [Test]
    public void FreshRun_HasDefaultsAndIsNotANewPhase()
    {
        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Geral));
        Assert.That(MetaProgressionManager.LoadedCardNames, Is.Empty);
        Assert.That(MetaProgressionManager.LoadedAdvantageNames, Is.Empty);
        Assert.That(MetaProgressionManager.PhasesCompleted, Is.EqualTo(0));
        Assert.That(MetaProgressionManager.IsNewPhase, Is.False);
    }

    [Test]
    public void SetArchetype_PersistsAcrossReload()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Automacao);

        ForceReloadFromPlayerPrefs();

        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Automacao));
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

        ForceReloadFromPlayerPrefs();

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
    public void ResetRun_ClearsEverythingBackToDefaults()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Industria);
        MetaProgressionManager.AddCardReward("Carta");
        MetaProgressionManager.AddAdvantageReward("Vantagem");
        MetaProgressionManager.IncrementPhasesCompleted();

        MetaProgressionManager.ResetRun();

        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Geral));
        Assert.That(MetaProgressionManager.LoadedCardNames, Is.Empty);
        Assert.That(MetaProgressionManager.LoadedAdvantageNames, Is.Empty);
        Assert.That(MetaProgressionManager.PhasesCompleted, Is.EqualTo(0));
    }

    [Test]
    public void FullState_RoundTripsThroughPlayerPrefsAfterReload()
    {
        MetaProgressionManager.SetArchetype(CardArchetype.Sustentabilidade);
        MetaProgressionManager.AddCardReward("CartaA");
        MetaProgressionManager.AddAdvantageReward("VantagemA");
        MetaProgressionManager.IncrementPhasesCompleted();
        MetaProgressionManager.IncrementPhasesCompleted();

        ForceReloadFromPlayerPrefs();

        Assert.That(MetaProgressionManager.Archetype, Is.EqualTo(CardArchetype.Sustentabilidade));
        Assert.That(MetaProgressionManager.LoadedCardNames, Is.EquivalentTo(new[] { "CartaA" }));
        Assert.That(MetaProgressionManager.LoadedAdvantageNames, Is.EquivalentTo(new[] { "VantagemA" }));
        Assert.That(MetaProgressionManager.PhasesCompleted, Is.EqualTo(2));
    }
}
