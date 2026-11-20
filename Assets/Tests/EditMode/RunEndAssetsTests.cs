using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class RunEndAssetsTests
{
    private const string ScenePath = "Assets/Scenes/City_Scene.unity";

    private static readonly FieldInfo LanguageField = typeof(LocalizationManager).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static);

    private object _previousLanguage;

    [SetUp]
    public void SetUp()
    {
        _previousLanguage = LanguageField.GetValue(null);
    }

    [TearDown]
    public void TearDown()
    {
        LanguageField.SetValue(null, _previousLanguage);
    }

    private static T Single<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
    }

    [Test]
    public void CityScene_HasTheRunEndSequenceAndSummaryWired()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var flow = new SerializedObject(Single<RunEndFlow>(scene));
            foreach (var field in new[] { "_turnManager", "_endFlow", "_sequenceRoot", "_sequence", "_summary" })
                Assert.That(flow.FindProperty(field).objectReferenceValue, Is.Not.Null, field);

            var view = (SlideSequenceView)flow.FindProperty("_sequence").objectReferenceValue;
            var viewSo = new SerializedObject(view);
            Assert.That(view.Slides.Count, Is.EqualTo(RunIntroController.SlideCount));
            Assert.That(view.Slides.All(slide => slide != null), Is.True);
            Assert.That(viewSo.FindProperty("_borderColors").arraySize, Is.EqualTo(RunIntroController.SlideCount));
            foreach (var field in new[] { "_canvasRect", "_slideArea", "_backImage", "_frontImage", "_captionBand", "_captionText", "_skipButton", "_skipLabel" })
                Assert.That(viewSo.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            var canvas = (RectTransform)viewSo.FindProperty("_canvasRect").objectReferenceValue;
            Assert.That(canvas.gameObject.scene, Is.EqualTo(scene), "a tela se encaixa no Canvas da propria cidade");

            var summary = new SerializedObject(Single<RunSummaryPopupView>(scene));
            foreach (var field in new[] { "_turnManager", "_panelRoot", "_titleText", "_messageText", "_headerText", "_linesText", "_menuButton", "_themes", "_bosses", "_trades" })
                Assert.That(summary.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(summary.FindProperty("_loadMenu").boolValue, Is.True);
            var menu = (Component)summary.FindProperty("_menuButton").objectReferenceValue;
            Assert.That(menu.GetComponent<ButtonClickSound>(), Is.Not.Null);

            var outcomes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameOutcomePopupView>(true)).ToList();
            Assert.That(outcomes, Is.Not.Empty);
            foreach (var outcome in outcomes)
                Assert.That(new SerializedObject(outcome).FindProperty("_runEnd").objectReferenceValue, Is.Not.Null, outcome.name);

            var tip = new SerializedObject(Single<TutorialTipView>(scene)).FindProperty("_blockingPanels");
            var blocking = Enumerable.Range(0, tip.arraySize).Select(i => tip.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
            Assert.That(blocking, Has.Member(flow.FindProperty("_sequenceRoot").objectReferenceValue));
            Assert.That(blocking, Has.Member(summary.FindProperty("_panelRoot").objectReferenceValue));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [TestCase(Language.Portuguese)]
    [TestCase(Language.English)]
    public void Captions_ExistForEveryEndSlideWithoutDashes(Language language)
    {
        LanguageField.SetValue(null, language);
        foreach (var ending in new[] { RunEnding.Defeat, RunEnding.BossVictory })
        {
            foreach (var step in RunEndFlow.Steps(ending))
            {
                foreach (CardArchetype archetype in System.Enum.GetValues(typeof(CardArchetype)))
                {
                    var caption = UIStrings.SlideCaption(step.CaptionKey, archetype);
                    Assert.That(caption, Is.Not.Empty, step.CaptionKey);
                    Assert.That(caption, Does.Not.Contain("—").And.Not.Contain("–"), step.CaptionKey);
                }
            }
        }
    }

    [TestCase(Language.Portuguese)]
    [TestCase(Language.English)]
    public void SummaryLines_ExistForEveryKind(Language language)
    {
        LanguageField.SetValue(null, language);
        foreach (RunSummaryLineKind kind in System.Enum.GetValues(typeof(RunSummaryLineKind)))
        {
            var line = UIStrings.RunSummaryLine(kind, 2, CardArchetype.Automacao, "Nome");
            Assert.That(line, Is.Not.Empty, kind.ToString());
            Assert.That(line, Does.Not.Contain("—").And.Not.Contain("–"), kind.ToString());
        }
        Assert.That(UIStrings.RunDefeatTitle, Is.Not.Empty);
        Assert.That(UIStrings.RunSummaryHeader, Is.Not.Empty);
        Assert.That(UIStrings.RunSummaryMenuButton, Is.Not.Empty);
    }
}
