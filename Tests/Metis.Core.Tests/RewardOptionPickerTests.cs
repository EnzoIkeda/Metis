using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class RewardOptionPickerTests
{
    [Test]
    public void Draw_ReturnsDistinctItemsUpToCount()
    {
        var pool = Enumerable.Range(0, 10).ToList();

        var options = RewardOptionPicker.Draw(pool, 3, new Random(3));

        Assert.That(options, Has.Count.EqualTo(3));
        Assert.That(options, Is.Unique);
        Assert.That(pool, Has.Count.EqualTo(10), "nao deveria mexer no pool original");
    }

    [Test]
    public void Draw_CountLargerThanPool_ReturnsWholePool()
    {
        var pool = new List<string> { "a", "b" };

        var options = RewardOptionPicker.Draw(pool, 5, new Random(3));

        Assert.That(options, Is.EquivalentTo(pool));
    }

    [Test]
    public void Draw_SameSeed_ProducesSameOptions()
    {
        var pool = Enumerable.Range(0, 30).ToList();

        var first = RewardOptionPicker.Draw(pool, 3, new Random(9));
        var second = RewardOptionPicker.Draw(pool, 3, new Random(9));

        Assert.That(second, Is.EqualTo(first));
    }
}
