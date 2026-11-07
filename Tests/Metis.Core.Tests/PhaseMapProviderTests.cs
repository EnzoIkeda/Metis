namespace Metis.Core.Tests;

public class PhaseMapProviderTests
{
    private static readonly string[] Themes = { "A", "B", "C", "D" };

    private static PhaseMapNode Themed(string id) => new PhaseMapNode { Id = id, Type = PhaseMapNodeType.Themed, IsAvailable = true };

    [Test]
    public void BuildOffer_WithoutThemes_OffersOnlyTheUnknownCity()
    {
        var offer = PhaseMapProvider.BuildOffer(Array.Empty<string>(), null, new Random(1));

        Assert.That(offer.Single().Id, Is.EqualTo(PhaseMapProvider.UnknownCityId));
        Assert.That(offer.Single().Type, Is.EqualTo(PhaseMapNodeType.Unknown));
        Assert.That(offer.Single().IsAvailable, Is.True);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void BuildOffer_AddsAtMostTwoDifferentThemesAfterTheUnknownCity(int seed)
    {
        var offer = PhaseMapProvider.BuildOffer(Themes, null, new Random(seed));

        Assert.That(offer.Count, Is.EqualTo(3));
        Assert.That(offer[0].Type, Is.EqualTo(PhaseMapNodeType.Unknown));
        Assert.That(offer.Skip(1).All(node => node.Type == PhaseMapNodeType.Themed), Is.True);
        Assert.That(offer[1].Id, Is.Not.EqualTo(offer[2].Id));
    }

    [Test]
    public void BuildOffer_LeavesOutThemesAlreadyPlayed()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var offer = PhaseMapProvider.BuildOffer(Themes, new[] { "A", "B", "C" }, new Random(seed));
            Assert.That(offer.Select(node => node.Id), Is.EqualTo(new[] { PhaseMapProvider.UnknownCityId, "D" }));
        }
    }

    [Test]
    public void BuildOffer_SameSeedGivesTheSameOffer()
    {
        var first = PhaseMapProvider.BuildOffer(Themes, null, new Random(7));
        var second = PhaseMapProvider.BuildOffer(Themes, null, new Random(7));

        Assert.That(second.Select(node => node.Id), Is.EqualTo(first.Select(node => node.Id)));
    }

    [Test]
    public void ResolveOffer_KeepsTheOfferAlreadyDrawn()
    {
        var saved = PhaseMapProvider.BuildOffer(Themes, null, new Random(1));

        for (var seed = 0; seed < 20; seed++)
            Assert.That(PhaseMapProvider.ResolveOffer(saved, Themes, null, new Random(seed)).Select(node => node.Id), Is.EqualTo(saved.Select(node => node.Id)));
    }

    [Test]
    public void ResolveOffer_DropsThemesThatNoLongerExist()
    {
        var saved = new List<PhaseMapNode> { new PhaseMapNode { Id = PhaseMapProvider.UnknownCityId, IsAvailable = true }, Themed("Sumiu"), Themed("A") };

        var offer = PhaseMapProvider.ResolveOffer(saved, new[] { "A" }, null, new Random(1));

        Assert.That(offer.Select(node => node.Id), Is.EqualTo(new[] { PhaseMapProvider.UnknownCityId, "A" }));
    }

    [Test]
    public void ResolveOffer_WithoutSavedOffer_DrawsOne()
    {
        Assert.That(PhaseMapProvider.ResolveOffer(new List<PhaseMapNode>(), Themes, null, new Random(1)).Count, Is.EqualTo(3));
        Assert.That(PhaseMapProvider.ResolveOffer(null, Themes, null, new Random(1)).Count, Is.EqualTo(3));
    }

    [Test]
    public void ApplyMapChoice_Theme_BecomesCurrentAndCountsAsPlayed()
    {
        var save = new RunSaveData();
        save.MapOffer.Add(Themed("A"));

        RunSaveRules.ApplyMapChoice(save, Themed("A"));

        Assert.That(save.Meta.CurrentTheme, Is.EqualTo("A"));
        Assert.That(save.Meta.PlayedThemes, Is.EqualTo(new[] { "A" }));
        Assert.That(save.MapOffer, Is.Empty);
    }

    [Test]
    public void ApplyMapChoice_UnknownCity_ClearsTheCurrentTheme()
    {
        var save = new RunSaveData();
        save.Meta.CurrentTheme = "A";
        save.Meta.PlayedThemes.Add("A");

        RunSaveRules.ApplyMapChoice(save, new PhaseMapNode { Id = PhaseMapProvider.UnknownCityId, IsAvailable = true });

        Assert.That(save.Meta.CurrentTheme, Is.Empty);
        Assert.That(save.Meta.PlayedThemes, Is.EqualTo(new[] { "A" }));
    }
}
