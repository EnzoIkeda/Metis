using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

// Confere as noticias reais do jornal: biblioteca completa, textos nos dois idiomas e todo gatilho possivel com noticia.
public class NewsStoriesTests
{
    private const string LibraryPath = "Assets/Data/News/NewsLibrary.asset";

    private static NewsLibraryData Library => AssetDatabase.LoadAssetAtPath<NewsLibraryData>(LibraryPath);

    private static IEnumerable<NewsStoryData> AllAssets()
    {
        return AssetDatabase.FindAssets("t:NewsStoryData")
            .Select(guid => AssetDatabase.LoadAssetAtPath<NewsStoryData>(AssetDatabase.GUIDToAssetPath(guid)));
    }

    private static string Field(NewsStoryData story, string name)
    {
        return new SerializedObject(story).FindProperty(name).stringValue;
    }

    [Test]
    public void Library_ListsEveryStoryAssetExactlyOnce()
    {
        var listed = Library.Stories.ToList();

        Assert.That(listed, Has.No.Member(null));
        Assert.That(listed.Distinct().Count(), Is.EqualTo(listed.Count), "noticia repetida na biblioteca");
        Assert.That(listed, Is.EquivalentTo(AllAssets().ToList()));
    }

    [Test]
    public void EveryStory_HasValidTextInBothLanguages()
    {
        foreach (var story in Library.Stories)
        {
            var dto = new NewsImportFormat.StoryDto
            {
                Id = story.name,
                Trigger = story.Trigger.ToString(),
                Parameter = NewsRules.UsesParameter(story.Trigger) ? story.Parameter.ToString() : string.Empty,
                HeadlinePt = Field(story, "_headline"),
                HeadlineEn = Field(story, "_headlineEn"),
                BodyPt = Field(story, "_body"),
                BodyEn = Field(story, "_bodyEn"),
            };
            Assert.That(NewsImportFormat.Validate(new NewsImportFormat.FileDto { Stories = new[] { dto } }), Is.Empty, story.name);
        }
    }

    [Test]
    public void EveryPossibleTrigger_HasAtLeastOneStory()
    {
        var stories = Library.Stories;
        foreach (NewsTrigger trigger in Enum.GetValues(typeof(NewsTrigger)))
        {
            if (NewsRules.UsesParameter(trigger) == false)
            {
                Assert.That(stories.Any(story => story.Trigger == trigger), Is.True, trigger.ToString());
                continue;
            }
            foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
            {
                if (NewsRules.Applies(trigger, parameter))
                    Assert.That(stories.Any(story => story.Trigger == trigger && story.Parameter == parameter), Is.True, $"{trigger}/{parameter}");
            }
        }
    }
}
