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
    public void Build_CommunityBasica_OnlyGoesToTheCommunityDeck()
    {
        var comunitaria = new FakeCard { Id = "Horta", Tier = CardTier.Basica, Archetype = CardArchetype.Comunitario };
        var basica = new FakeCard { Id = "Basica", Tier = CardTier.Basica, Archetype = CardArchetype.Industria };
        var geral = new FakeCard { Id = "Geral", Tier = CardTier.CidadeDigital, Archetype = CardArchetype.Geral };
        var outro = new FakeCard { Id = "Outro", Tier = CardTier.CidadeDigital, Archetype = CardArchetype.Automacao };
        var all = new List<FakeCard> { comunitaria, basica, geral, outro };

        foreach (var archetype in new[] { CardArchetype.Sustentabilidade, CardArchetype.Industria, CardArchetype.Automacao })
            Assert.That(DeckBuilder.Build(all, archetype, new List<string>()), Has.No.Member(comunitaria), archetype.ToString());
        Assert.That(DeckBuilder.Build(all, CardArchetype.Comunitario, new List<string>()), Is.EquivalentTo(new[] { comunitaria, basica, geral }));
    }

    [Test]
    public void Build_CommunityCardLoadedAsReward_JoinsAnotherDeck()
    {
        var comunitaria = new FakeCard { Id = "Horta", Tier = CardTier.Basica, Archetype = CardArchetype.Comunitario };

        var pool = DeckBuilder.Build(new List<FakeCard> { comunitaria }, CardArchetype.Automacao, new List<string> { "Horta" });

        Assert.That(pool, Is.EquivalentTo(new[] { comunitaria }));
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
