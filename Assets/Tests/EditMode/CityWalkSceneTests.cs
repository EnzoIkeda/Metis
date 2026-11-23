using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CityWalkSceneTests
{
    private static Object[] Array(SerializedObject target, string field)
    {
        var property = target.FindProperty(field);
        return Enumerable.Range(0, property.arraySize).Select(i => property.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
    }

    [Test]
    public void CityScene_WiresTheWalk_IntoTheHudFlyoverAndTutorial()
    {
        var previous = EditorSceneManager.GetActiveScene().path;
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/City_Scene.unity");
            var walk = Object.FindFirstObjectByType<CityWalkController>(FindObjectsInactive.Include);
            Assert.That(walk, Is.Not.Null);
            var serialized = new SerializedObject(walk);
            foreach (var field in new[] { "_turnManager", "_placementManager", "_flyover", "_mainCamera", "_walkCamera", "_walkButton", "_overlay", "_stick", "_lookArea", "_backButton", "_backButtonText", "_hint", "_hintText", "_carTraffic" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);

            var walkCamera = (Camera)serialized.FindProperty("_walkCamera").objectReferenceValue;
            var mainCamera = (Camera)serialized.FindProperty("_mainCamera").objectReferenceValue;
            Assert.That(walkCamera, Is.Not.EqualTo(mainCamera), "a camera isometrica calibrada nao muda");
            Assert.That(walkCamera.orthographic, Is.False);
            Assert.That(walkCamera.enabled, Is.False);
            Assert.That(((GameObject)serialized.FindProperty("_overlay").objectReferenceValue).activeSelf, Is.False);

            // O sobrevoo esconde o botao novo, e o passeio esconde tudo o que o sobrevoo esconde.
            var flyover = new SerializedObject(Object.FindFirstObjectByType<CityIntroFlyover>(FindObjectsInactive.Include));
            var flyoverGroups = Array(flyover, "_hiddenGroups");
            var button = (UnityEngine.UI.Button)serialized.FindProperty("_walkButton").objectReferenceValue;
            Assert.That(flyoverGroups, Has.Member(button.GetComponent<CanvasGroup>()));
            var walkGroups = Array(serialized, "_hiddenGroups");
            foreach (var group in flyoverGroups.Where(group => group != button.GetComponent<CanvasGroup>()))
                Assert.That(walkGroups, Has.Member(group));

            // Nenhuma dica fala por cima do passeio, e o passeio nao abre por cima de um popup do tutorial.
            var tutorial = new SerializedObject(Object.FindFirstObjectByType<TutorialTipView>(FindObjectsInactive.Include));
            var tutorialBlocking = Array(tutorial, "_blockingPanels");
            Assert.That(tutorialBlocking, Has.Member(serialized.FindProperty("_overlay").objectReferenceValue));
            var walkBlocking = Array(serialized, "_blockingPanels");
            foreach (var panel in tutorialBlocking.Where(panel => panel != serialized.FindProperty("_overlay").objectReferenceValue))
                Assert.That(walkBlocking, Has.Member(panel));
        }
        finally
        {
            if (string.IsNullOrEmpty(previous) == false)
                EditorSceneManager.OpenScene(previous);
        }
    }
}
