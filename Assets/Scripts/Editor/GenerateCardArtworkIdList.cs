using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Gera a lista de IDs usados pra nomear as imagens de Art/Cards, a partir dos CardData existentes.
public static class GenerateCardArtworkIdList
{
    private const string CardDataFolder = "Assets/Data/Card";
    private const string CardAssetPrefix = "Card";
    private const string OutputPath = "Assets/Art/CardArtworkIDs.txt";

    [MenuItem("Tools/Metis/Generate Card Artwork ID List")]
    private static void GenerateList()
    {
        var guids = AssetDatabase.FindAssets("t:CardData", new[] { CardDataFolder });
        var ids = guids
            .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(name => name.StartsWith(CardAssetPrefix))
            .Select(name => name.Substring(CardAssetPrefix.Length))
            .OrderBy(id => id)
            .ToList();

        if (ids.Count == 0)
        {
            Debug.LogWarning($"Nenhum CardData encontrado em '{CardDataFolder}'.");
            return;
        }

        var directory = Path.GetDirectoryName(OutputPath);
        if (Directory.Exists(directory) == false)
            Directory.CreateDirectory(directory);

        File.WriteAllLines(OutputPath, ids);
        AssetDatabase.Refresh();

        Debug.Log($"Lista com {ids.Count} IDs de carta salva em '{OutputPath}'.");
    }
}
