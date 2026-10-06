using System.Collections.Generic;

namespace Metis.Core.Tests;

public class RewardPoolTests
{
    private sealed class FakeAdvantage : IAdvantageDefinition
    {
        public string Id { get; init; } = "";
        public IReadOnlyList<StatModifier> StatEffects { get; init; } = new StatModifier[0];
    }

    [Test]
    public void CardPool_LeavesOutCardsAlreadyInTheDeck()
    {
        var inDeck = new FakeCard { Id = "NoBaralho" };
        var outside = new FakeCard { Id = "Fora" };

        var pool = RewardOptionPicker.CardPool(new List<FakeCard> { inDeck, outside }, new List<FakeCard> { inDeck });

        Assert.That(pool, Is.EqualTo(new[] { outside }));
    }

    [Test]
    public void AdvantagePool_LeavesOutAdvantagesAlreadyLoaded()
    {
        var loaded = new FakeAdvantage { Id = "Carregada" };
        var fresh = new FakeAdvantage { Id = "Nova" };

        var pool = RewardOptionPicker.AdvantagePool(new List<FakeAdvantage> { loaded, fresh }, new List<string> { "Carregada" });

        Assert.That(pool, Is.EqualTo(new[] { fresh }));
    }
}
