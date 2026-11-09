using System;
using System.Collections.Generic;

// Tipo de destino oferecido no mapa. So acrescentar valores no fim.
public enum PhaseMapNodeType
{
    Unknown,
    Themed,
}

// Um no do mapa entre fases. Id identifica o destino; no tema, e o nome do asset.
[Serializable]
public struct PhaseMapNode
{
    public string Id;
    public PhaseMapNodeType Type;
    public bool IsAvailable;
}

// Destinos oferecidos em cada parada do mapa.
public static class PhaseMapProvider
{
    public const string UnknownCityId = "next";
    public const int MaxThemedNodes = 2;

    // Sempre a cidade desconhecida, mais temas ainda nao jogados na rodada.
    public static List<PhaseMapNode> BuildOffer(IReadOnlyList<string> themeNames, IReadOnlyCollection<string> playedThemes, Random random)
    {
        var offer = new List<PhaseMapNode> { new PhaseMapNode { Id = UnknownCityId, Type = PhaseMapNodeType.Unknown, IsAvailable = true } };
        var candidates = new List<string>();
        foreach (var theme in themeNames)
        {
            if (candidates.Contains(theme) == false && (playedThemes == null || Contains(playedThemes, theme) == false))
                candidates.Add(theme);
        }

        while (candidates.Count > 0 && offer.Count <= MaxThemedNodes)
        {
            var index = random.Next(candidates.Count);
            offer.Add(new PhaseMapNode { Id = candidates[index], Type = PhaseMapNodeType.Themed, IsAvailable = true });
            candidates.RemoveAt(index);
        }
        return offer;
    }

    // Mantem a oferta ja sorteada, sem temas que deixaram de existir; sem oferta, sorteia uma.
    public static List<PhaseMapNode> ResolveOffer(IReadOnlyList<PhaseMapNode> saved, IReadOnlyList<string> themeNames, IReadOnlyCollection<string> playedThemes, Random random)
    {
        var offer = new List<PhaseMapNode>();
        if (saved != null)
        {
            foreach (var node in saved)
            {
                if (node.Type != PhaseMapNodeType.Themed || Contains(themeNames, node.Id))
                    offer.Add(node);
            }
        }
        return offer.Count > 0 ? offer : BuildOffer(themeNames, playedThemes, random);
    }

    private static bool Contains(IEnumerable<string> names, string name)
    {
        foreach (var candidate in names)
        {
            if (candidate == name)
                return true;
        }
        return false;
    }
}
