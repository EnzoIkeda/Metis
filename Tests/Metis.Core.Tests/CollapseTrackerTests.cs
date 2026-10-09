namespace Metis.Core.Tests;

public class CollapseTrackerTests
{
    [Test]
    public void Update_EnteringCollapse_ReturnsTrueOnce()
    {
        var tracker = new CollapseTracker();

        Assert.That(tracker.Update(CityParameterType.Energia, true), Is.True);
        Assert.That(tracker.Update(CityParameterType.Energia, true), Is.False, "aviso repetido enquanto continua em Colapso");
    }

    [Test]
    public void Update_OutsideCollapse_NeverReturnsTrue()
    {
        var tracker = new CollapseTracker();

        Assert.That(tracker.Update(CityParameterType.Energia, false), Is.False);
    }

    [Test]
    public void Update_LeavingAndEnteringAgain_ReturnsTrueAgain()
    {
        var tracker = new CollapseTracker();
        tracker.Update(CityParameterType.Saude, true);

        tracker.Update(CityParameterType.Saude, false);

        Assert.That(tracker.Update(CityParameterType.Saude, true), Is.True);
    }

    [Test]
    public void Update_ParametersAreTrackedIndependently()
    {
        var tracker = new CollapseTracker();
        tracker.Update(CityParameterType.Renda, true);

        Assert.That(tracker.Update(CityParameterType.Seguranca, true), Is.True);
        Assert.That(tracker.Update(CityParameterType.Renda, true), Is.False);
    }
}
