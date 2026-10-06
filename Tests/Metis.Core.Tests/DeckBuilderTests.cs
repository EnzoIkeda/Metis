using System.Collections.Generic;

namespace Metis.Core.Tests;

public class DeckBuilderTests
{
    [Test]
    public void Build_KeepsBasicaGeralAndChosenArchetypeOnly()
    {
        var basica = new FakeCard { Id = "Basica", Tier = CardTier.Basica, Archetype = CardArchetype.Industria };
        var geral = new FakeCard { Id = "Geral", Tier = CardTier.SmartCity, Archetype = CardArchetype.Geral };
        var escolhido = new FakeCard { Id = "Escolhido", Tier = CardTier.CidadeDigital, Archetype = CardArchetype.Automacao };
        var outro = new FakeCard { Id = "Outro", Tier = CardTier.CidadeDigital, Archetype = CardArchetype.Industria };

        var pool = DeckBuilder.Build(new List<FakeCard> { basica, geral, escolhido, outro }, CardArchetype.Automacao, new List<string>());

        Assert.That(pool, Is.EquivalentTo(new[] { basica, geral, escolhido }));
    }

    [Test]
    public void Build_AddsLoadedRewardCardsById_WithoutDuplicates()
    {
        var basica = new FakeCard { Id = "Basica" };
        var recompensa = new FakeCard { Id = "Recompensa", Tier = CardTier.SmartCity, Archetype = CardArchetype.Industria };

        var pool = DeckBuilder.Build(new List<FakeCard> { basica, recompensa }, CardArchetype.Automacao, new List<string> { "Recompensa", "Basica", "Inexistente" });

        Assert.That(pool, Is.EquivalentTo(new[] { basica, recompensa }));
    }
}
