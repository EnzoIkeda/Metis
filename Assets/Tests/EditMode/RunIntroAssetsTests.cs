using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class RunIntroAssetsTests
{
    private const string ScenePath = "Assets/Scenes/RunIntro.unity";

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

    [Test]
    public void RunIntro_IsInTheBuildRightAfterTheDeckSelection()
    {
        var paths = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToList();
        var intro = paths.IndexOf(ScenePath);
        Assert.That(intro, Is.GreaterThan(0), "a abertura precisa estar no build");
        Assert.That(paths[intro - 1], Is.EqualTo("Assets/Scenes/DeckSelection.unity"));
        Assert.That(System.IO.Path.GetFileNameWithoutExtension(ScenePath), Is.EqualTo(RunIntroController.SceneName));
    }

    [Test]
    public void RunIntro_HasEverySlideColorAndReference()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SlideSequenceView>(true)).Single();
            var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RunIntroController>(true)).Single();
            var so = new SerializedObject(view);

            Assert.That(view.Slides.Count, Is.EqualTo(RunIntroController.SlideCount));
            Assert.That(view.Slides.All(slide => slide != null), Is.True, "toda tela precisa da imagem");
            Assert.That(so.FindProperty("_borderColors").arraySize, Is.EqualTo(RunIntroController.SlideCount));
            foreach (var field in new[] { "_canvasRect", "_slideArea", "_backImage", "_frontImage", "_captionBand", "_captionText", "_skipButton", "_skipLabel" })
                Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(new SerializedObject(controller).FindProperty("_view").objectReferenceValue, Is.EqualTo(view));
            Assert.That(new SerializedObject(controller).FindProperty("_loadCity").boolValue, Is.True);

            var skip = (Component)so.FindProperty("_skipButton").objectReferenceValue;
            Assert.That(skip.GetComponent<ButtonClickSound>(), Is.Not.Null);
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MusicPlayer>(true)).Count(), Is.EqualTo(1));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [TestCase(Language.Portuguese)]
    [TestCase(Language.English)]
    public void Captions_ExistForEverySlideWithoutDashes(Language language)
    {
        LanguageField.SetValue(null, language);
        var steps = RunIntroController.Steps();
        Assert.That(steps.Select(step => step.ImageIndex), Is.EqualTo(Enumerable.Range(0, RunIntroController.SlideCount)));

        foreach (var step in steps)
        {
            var caption = UIStrings.SlideCaption(step.CaptionKey, CardArchetype.Sustentabilidade);
            Assert.That(caption, Is.Not.Empty, step.CaptionKey);
            Assert.That(caption, Does.Not.Contain("—").And.Not.Contain("–"), step.CaptionKey);
        }
        Assert.That(steps.Select(step => step.CaptionKey).Distinct().Count(), Is.EqualTo(steps.Count));
        Assert.That(UIStrings.SlideCaption(steps.Last().CaptionKey, CardArchetype.Industria), Does.Contain("20"));
        Assert.That(UIStrings.SkipButton, Is.Not.Empty);
    }

    [Test]
    public void Captions_DifferBetweenLanguagesAndNameTheDeck()
    {
        var key = UIStrings.IntroCaptionKey(5);
        LanguageField.SetValue(null, Language.Portuguese);
        var pt = UIStrings.SlideCaption(key, CardArchetype.Automacao);
        Assert.That(pt, Does.Contain(UIStrings.ArchetypeName(CardArchetype.Automacao)));
        LanguageField.SetValue(null, Language.English);
        var en = UIStrings.SlideCaption(key, CardArchetype.Automacao);
        Assert.That(en, Does.Contain(UIStrings.ArchetypeName(CardArchetype.Automacao)));
        Assert.That(en, Is.Not.EqualTo(pt));
    }

    [Test]
    public void ThemeMusic_HasEveryTrackAndTheMusicPlayerUsesIt()
    {
        var themes = AssetDatabase.LoadAssetAtPath<ThemeMusicData>("Assets/Data/Audio/ThemeMusic.asset");
        Assert.That(themes, Is.Not.Null);
        Assert.That(new[] { themes.Opening, themes.Defeat, themes.Victory, themes.Crisis }.All(clip => clip != null), Is.True);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Audio/MusicPlayer.prefab");
        Assert.That(prefab.GetComponent<MusicPlayer>().Themes, Is.EqualTo(themes));

        // Faixas longas tocam do disco, para nao ocupar memoria no celular.
        foreach (var clip in new[] { themes.Opening, themes.Crisis })
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
            Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), clip.name);
        }
    }
}
