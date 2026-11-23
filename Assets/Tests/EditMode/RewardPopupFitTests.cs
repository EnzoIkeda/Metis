using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Toda carta e toda vantagem que pode sair na recompensa cabe na carta da tela, nos dois idiomas.
public class RewardPopupFitTests
{
    // Abaixo disso a descricao fica dificil de ler no celular.
    private const float MinReadableSize = 18f;

    private static IEnumerable<Object> AllOptions()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Card" }))
            yield return AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
        foreach (var guid in AssetDatabase.FindAssets("t:PassiveAdvantageData", new[] { "Assets/Data" }))
            yield return AssetDatabase.LoadAssetAtPath<PassiveAdvantageData>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static void SetLanguage(Language language)
    {
        typeof(LocalizationManager).GetField("_current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, language);
    }

    private static T Field<T>(object target, string name) where T : class
    {
        return target.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(target) as T;
    }

    [Test]
    public void EveryRewardOption_FitsTheCard_InBothLanguages()
    {
        var previous = EditorSceneManager.GetActiveScene().path;
        var language = LocalizationManager.Current;
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/City_Scene.unity");
            var popup = Object.FindFirstObjectByType<PhaseRewardPopupView>(FindObjectsInactive.Include);
            var cards = Field<RewardCardView[]>(popup, "_optionCards");
            Assert.That(cards, Is.Not.Null.And.Length.GreaterThanOrEqualTo(3));
            var panel = Field<GameObject>(popup, "_panelRoot");
            panel.SetActive(true);
            var card = cards[0];
            card.gameObject.SetActive(true);
            var title = Field<TMP_Text>(card, "_nameText");
            var description = Field<TMP_Text>(card, "_descriptionText");
            var options = AllOptions().ToList();
            Assert.That(options.Count, Is.GreaterThan(60));

            var problems = new List<string>();
            foreach (var current in new[] { Language.Portuguese, Language.English })
            {
                SetLanguage(current);
                foreach (var option in options)
                {
                    card.Bind(option);
                    foreach (var text in new[] { title, description })
                    {
                        text.ForceMeshUpdate();
                        if (text.isTextOverflowing || text.fontSize < MinReadableSize)
                            problems.Add($"{option.name} ({current}) {text.name}: tamanho {text.fontSize:0.#}, transborda {text.isTextOverflowing}");
                    }
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
        finally
        {
            SetLanguage(language);
            if (string.IsNullOrEmpty(previous) == false)
                EditorSceneManager.OpenScene(previous);
        }
    }

    [Test]
    public void TheRewardPopup_HasOneChooseButtonPerCard()
    {
        var previous = EditorSceneManager.GetActiveScene().path;
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/City_Scene.unity");
            var popup = Object.FindFirstObjectByType<PhaseRewardPopupView>(FindObjectsInactive.Include);
            var cards = Field<RewardCardView[]>(popup, "_optionCards");
            Assert.That(new SerializedObject(popup).FindProperty("_optionCount").intValue, Is.LessThanOrEqualTo(cards.Length));
            Assert.That(Field<TMP_Text>(popup, "_hintText"), Is.Not.Null);
            foreach (var card in cards)
            {
                Assert.That(card.ChooseButton, Is.Not.Null);
                Assert.That(card.ChooseButton.onClick.GetPersistentEventCount(), Is.EqualTo(0), "o clique e ligado por codigo");
                foreach (var field in new[] { "_kindText", "_nameText", "_artworkImage", "_artworkPlaceholder", "_descriptionText", "_chooseText", "_advantageArtwork" })
                    Assert.That(new SerializedObject(card).FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            }
        }
        finally
        {
            if (string.IsNullOrEmpty(previous) == false)
                EditorSceneManager.OpenScene(previous);
        }
    }
}
