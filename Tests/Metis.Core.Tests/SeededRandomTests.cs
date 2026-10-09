namespace Metis.Core.Tests;

public class SeededRandomTests
{
    [Test]
    public void SameState_ProducesSameSequence()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);

        for (int i = 0; i < 100; i++)
            Assert.That(a.Next(1000), Is.EqualTo(b.Next(1000)));
    }

    [Test]
    public void ResumingFromSavedState_ContinuesTheSameSequence()
    {
        var original = new SeededRandom(7);
        for (int i = 0; i < 10; i++)
            original.Next(50);

        var resumed = new SeededRandom(original.State);

        for (int i = 0; i < 100; i++)
            Assert.That(resumed.Next(50), Is.EqualTo(original.Next(50)));
    }

    [Test]
    public void Next_StaysWithinBounds()
    {
        var random = new SeededRandom(123);
        for (int i = 0; i < 10000; i++)
        {
            Assert.That(random.Next(6), Is.InRange(0, 5));
            Assert.That(random.Next(-3, 4), Is.InRange(-3, 3));
            Assert.That(random.NextDouble(), Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            Assert.That(random.Next(), Is.InRange(0, int.MaxValue - 1));
        }
    }

    [Test]
    public void Next_CoversEveryValueOfASmallRange()
    {
        var random = new SeededRandom(99);
        var seen = new bool[6];
        for (int i = 0; i < 1000; i++)
            seen[random.Next(6)] = true;

        Assert.That(seen, Has.All.True);
    }

    [Test]
    public void NewSeed_DiffersBetweenCalls()
    {
        var first = SeededRandom.NewSeed();
        var second = SeededRandom.NewSeed();

        Assert.That(second, Is.Not.EqualTo(first));
    }
}
