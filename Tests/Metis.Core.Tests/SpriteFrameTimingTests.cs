namespace Metis.Core.Tests;

public class SpriteFrameTimingTests
{
    [Test]
    public void FrameIndex_AtStart_IsFirstFrame()
    {
        Assert.That(SpriteFrameTiming.FrameIndex(0d, 15f, 75), Is.EqualTo(0));
    }

    [Test]
    public void FrameIndex_AdvancesWithTime()
    {
        Assert.That(SpriteFrameTiming.FrameIndex(1d, 15f, 75), Is.EqualTo(15));
        Assert.That(SpriteFrameTiming.FrameIndex(1.99d, 15f, 75), Is.EqualTo(29));
    }

    [Test]
    public void FrameIndex_WrapsAroundAfterLastFrame()
    {
        Assert.That(SpriteFrameTiming.FrameIndex(5d, 15f, 75), Is.EqualTo(0));
        Assert.That(SpriteFrameTiming.FrameIndex(5.5d, 15f, 75), Is.EqualTo(7));
    }

    [Test]
    public void FrameIndex_NoFrames_IsMinusOne()
    {
        Assert.That(SpriteFrameTiming.FrameIndex(1d, 15f, 0), Is.EqualTo(-1));
    }

    [TestCase(0f)]
    [TestCase(-3f)]
    public void FrameIndex_NonPositiveRate_StaysOnFirstFrame(float framesPerSecond)
    {
        Assert.That(SpriteFrameTiming.FrameIndex(10d, framesPerSecond, 75), Is.EqualTo(0));
    }

    [Test]
    public void FrameIndex_NegativeOrNaNTime_IsFirstFrame()
    {
        Assert.That(SpriteFrameTiming.FrameIndex(-1d, 15f, 75), Is.EqualTo(0));
        Assert.That(SpriteFrameTiming.FrameIndex(double.NaN, 15f, 75), Is.EqualTo(0));
    }

    [Test]
    public void FrameIndex_VeryLongSession_StaysInRange()
    {
        var index = SpriteFrameTiming.FrameIndex(86400d * 30, 15f, 75);
        Assert.That(index, Is.InRange(0, 74));
    }
}
