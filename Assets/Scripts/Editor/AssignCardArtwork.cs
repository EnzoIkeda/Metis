using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Varre Art/Cards por imagens nomeadas com o ID da carta e atribui cada uma ao CardData correspondente.
public static class AssignCardArtwork
{
    private const string ArtworkFolder = "Assets/Art/Cards";
    private const string CardDataFolder = "Assets/Data/Card";
    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg" };

    [MenuItem("Tools/Metis/Assign Card Artwork")]
    private static void AssignArtwork()
    {
        if (Directory.Exists(ArtworkFolder) == false)
        {
            Debug.LogWarning($"Pasta '{ArtworkFolder}' nao existe. Crie ela e coloque as imagens nomeadas com o ID de cada carta antes de rodar este comando.");
            return;
        }

        var imagePaths = Directory.GetFiles(ArtworkFolder)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            .OrderBy(path => path)
            .ToList();

        if (imagePaths.Count == 0)
        {
            Debug.LogWarning($"Nenhuma imagem encontrada em '{ArtworkFolder}'.");
            return;
        }

        var assigned = 0;
        var missingCards = new List<string>();

        foreach (var imagePath in imagePaths)
        {
            var normalizedPath = imagePath.Replace('\\', '/');
            var cardId = Path.GetFileNameWithoutExtension(normalizedPath);
            var sprite = LoadAsSprite(normalizedPath);
            if (sprite == null)
            {
                Debug.LogWarning($"Nao foi possivel carregar '{normalizedPath}' como Sprite.");
                continue;
            }

            var cardPath = $"{CardDataFolder}/Card{cardId}.asset";
            var cardData = AssetDatabase.LoadAssetAtPath<CardData>(cardPath);
            if (cardData == null)
            {
                missingCards.Add(cardId);
                continue;
            }

            var serialized = new SerializedObject(cardData);
            serialized.FindProperty("_artwork").objectReferenceValue = sprite;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(cardData);
            assigned++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Artwork atribuida a {assigned} de {imagePaths.Count} imagens encontradas.");
        if (missingCards.Count > 0)
            Debug.LogWarning($"Nenhuma carta encontrada para os IDs: {string.Join(", ", missingCards)}");
    }

    private static Sprite LoadAsSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
