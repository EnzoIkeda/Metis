using System.Linq;
using NUnit.Framework;
using UnityEditor;

public class RoadTechGlowAssetsTests
{
    // Os degraus das luzes das ruas seguem a Pesquisa minima das cartas de cada tier.
    [TestCase(CardTier.CidadeDigital, RoadTechGlow.DigitalPesquisa)]
    [TestCase(CardTier.CidadeConectada, RoadTechGlow.ConnectedPesquisa)]
    [TestCase(CardTier.SmartCity, RoadTechGlow.SmartPesquisa)]
    public void Steps_MatchTheLowestPesquisaOfEachTier(CardTier tier, float expected)
    {
        var lowest = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Card" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(card => card != null && card.Tier == tier)
            .Min(card => card.RequiredPesquisa);
        Assert.That(lowest, Is.EqualTo(expected));
    }
}
