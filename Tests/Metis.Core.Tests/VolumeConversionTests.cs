namespace Metis.Core.Tests;

public class VolumeConversionTests
{
    [Test]
    public void LinearToDecibels_FullVolume_IsZero()
    {
        Assert.That(VolumeConversion.LinearToDecibels(1f), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void LinearToDecibels_Half_IsAboutMinusSix()
    {
        Assert.That(VolumeConversion.LinearToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(0.001f));
    }

    [TestCase(0f)]
    [TestCase(-0.5f)]
    [TestCase(float.NaN)]
    public void LinearToDecibels_ZeroNegativeOrNaN_IsSilence(float linear)
    {
        Assert.That(VolumeConversion.LinearToDecibels(linear), Is.EqualTo(VolumeConversion.SilenceDecibels));
    }

    [Test]
    public void LinearToDecibels_TinyValue_NeverGoesBelowSilenceFloor()
    {
        Assert.That(VolumeConversion.LinearToDecibels(0.00001f), Is.EqualTo(VolumeConversion.SilenceDecibels));
    }

    [Test]
    public void LinearToDecibels_AboveOne_IsClampedToZero()
    {
        Assert.That(VolumeConversion.LinearToDecibels(2f), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void LinearToDecibels_IsMonotonic()
    {
        var previous = VolumeConversion.LinearToDecibels(0.01f);
        for (var step = 2; step <= 100; step++)
        {
            var current = VolumeConversion.LinearToDecibels(step / 100f);
            Assert.That(current, Is.GreaterThan(previous));
            previous = current;
        }
    }
}
