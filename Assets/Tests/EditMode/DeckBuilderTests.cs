using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class DeckBuilderTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private CardData Card(string name, CardTier tier, CardArchetype archetype)
    {
        var card = TestDataFactory.CreateCard(name: name, tier: tier, archetype: archetype);
        _created.Add(card);
        return card;
    }

    [Test]
    public void Build_IncludesBasicaTierRegardlessOfArchetype()
    {
        var basica = Card("Basica", CardTier.Basica, CardArchetype.Industria);
        var pool = DeckBuilder.Build(new List<CardData> { basica }, CardArchetype.Sustentabilidade, new List<string>());

        Assert.That(pool, Has.Member(basica));
    }

    [Test]
    public void Build_IncludesGeralArchetypeRegardlessOfTier()
    {
        var geral = Card("Geral", CardTier.SmartCity, CardArchetype.Geral);
        var pool = DeckBuilder.Build(new List<CardData> { geral }, CardArchetype.Sustentabilidade, new List<string>());

        Assert.That(pool, Has.Member(geral));
    }

    [Test]
    public void Build_IncludesCardsMatchingChosenArchetype()
    {
        var doArquetipo = Card("DoArquetipo", CardTier.CidadeDigital, CardArchetype.Sustentabilidade);
        var pool = DeckBuilder.Build(new List<CardData> { doArquetipo }, CardArchetype.Sustentabilidade, new List<string>());

        Assert.That(pool, Has.Member(doArquetipo));
    }

    [Test]
    public void Build_ExcludesNonBasicaCardsOfOtherArchetypes()
    {
        var outroArquetipo = Card("OutroArquetipo", CardTier.CidadeDigital, CardArchetype.Industria);
        var pool = DeckBuilder.Build(new List<CardData> { outroArquetipo }, CardArchetype.Sustentabilidade, new List<string>());

        Assert.That(pool, Has.No.Member(outroArquetipo));
    }

    [Test]
    public void Build_AddsLoadedRewardCardsByNameEvenIfArchetypeDoesNotMatch()
    {
        var recompensa = Card("RecompensaAutomacao", CardTier.SmartCity, CardArchetype.Automacao);
        var pool = DeckBuilder.Build(new List<CardData> { recompensa }, CardArchetype.Sustentabilidade, new List<string> { "RecompensaAutomacao" });

        Assert.That(pool, Has.Member(recompensa));
    }

    [Test]
    public void Build_LoadedCardAlreadyInPool_IsNotDuplicated()
    {
        var basica = Card("Basica", CardTier.Basica, CardArchetype.Geral);
        var pool = DeckBuilder.Build(new List<CardData> { basica }, CardArchetype.Sustentabilidade, new List<string> { "Basica" });

        Assert.That(pool, Has.Exactly(1).EqualTo(basica));
    }

    [Test]
    public void Build_UnknownLoadedCardName_IsIgnoredSilently()
    {
        var basica = Card("Basica", CardTier.Basica, CardArchetype.Geral);

        Assert.DoesNotThrow(() =>
            DeckBuilder.Build(new List<CardData> { basica }, CardArchetype.Sustentabilidade, new List<string> { "NomeInexistente" }));
    }
}
