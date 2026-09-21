using System;

namespace Metis.Core.Tests;

public class MusicRotationTests
{
    [Test]
    public void Next_ZeroTracks_ReturnsNegativeOne()
    {
        var rotation = new MusicRotation();

        Assert.That(rotation.Next(0, new Random(0)), Is.EqualTo(-1));
    }

    [Test]
    public void Next_SingleTrack_AlwaysReturnsZero()
    {
        var rotation = new MusicRotation();

        for (var seed = 0; seed < 10; seed++)
            Assert.That(rotation.Next(1, new Random(seed)), Is.EqualTo(0));
    }

    [Test]
    public void Next_MultipleTracks_NeverImmediatelyRepeats()
    {
        var rotation = new MusicRotation();
        var random = new Random(0);
        var previous = rotation.Next(5, random);

        for (var i = 0; i < 50; i++)
        {
            var next = rotation.Next(5, random);
            Assert.That(next, Is.Not.EqualTo(previous));
            previous = next;
        }
    }

    [Test]
    public void Next_MultipleTracks_StaysWithinRange()
    {
        var rotation = new MusicRotation();
        var random = new Random(0);

        for (var i = 0; i < 50; i++)
            Assert.That(rotation.Next(3, random), Is.InRange(0, 2));
    }
}
