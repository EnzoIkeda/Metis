using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Atalho pra criar um prefab a partir do GameObject selecionado na Hierarchy sem precisar
/// arrastar entre paineis ( por causa de um bug da Unity que as vezes me aparece )
/// </summary>
public static class CreatePrefabFromSelection
{
    private const string DefaultFolder = "Assets/Prefabs/UI";

    [MenuItem("Tools/Metis/Create Prefab From Selection")]
    private static void CreatePrefab()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Selecione um GameObject na Hierarchy antes de usar este comando");
            return;
        }

        if (Directory.Exists(DefaultFolder) == false)
            Directory.CreateDirectory(DefaultFolder);

        var path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolder}/{selected.name}.prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(selected, path, InteractionMode.UserAction);
        AssetDatabase.Refresh();
        Debug.Log($"Prefab criado em '{path}' e '{selected.name}' na Hierarchy agora é uma instância dele.");
    }
}
