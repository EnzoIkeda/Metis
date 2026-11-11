using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

// Confere os dilemas reais: regras, textos, ficha, noticia e presenca na cidade.
public class DilemmaAssetsTests
{
    private static List<RandomEventData> Events()
    {
        return AssetDatabase.FindAssets("t:RandomEventData", new[] { "Assets/Data" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<RandomEventData>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
    }

    private static List<RandomEventData> Dilemmas() => Events().Where(item => item.Choices.Count > 0).ToList();

    private static List<RandomEventData> FollowUps() => Events().Where(item => item.IsFollowUpOnly).ToList();

    [Test]
    public void TheFirstBatchIsThere()
    {
        Assert.That(Dilemmas().Count, Is.EqualTo(6));
        Assert.That(FollowUps().Count, Is.EqualTo(6));
    }

    [Test]
    public void EveryDilemma_FollowsTheRules()
    {
        var followUps = FollowUps().Select(item => item.name).ToList();
        foreach (var dilemma in Dilemmas())
        {
            Assert.That(dilemma.Choices.Count, Is.InRange(2, 3), dilemma.name);
            Assert.That(dilemma.StatEffects, Is.Empty, dilemma.name);
            Assert.That(dilemma.MaxTurn + dilemma.Choices.Max(choice => choice.HasFollowUp ? choice.EffectiveDelay : 0), Is.LessThanOrEqualTo(20), dilemma.name);
            foreach (var choice in dilemma.Choices.Where(item => item.HasFollowUp))
                Assert.That(followUps, Has.Member(choice.FollowUpEventId), dilemma.name);
            Assert.That(dilemma.Choices.Count(choice => choice.HasFollowUp), Is.GreaterThan(0), dilemma.name);
        }
    }

    [Test]
    public void EveryFollowUp_IsReachedByAnOption()
    {
        var reached = Dilemmas().SelectMany(item => item.Choices).Where(choice => choice.HasFollowUp).Select(choice => choice.FollowUpEventId).ToList();
        foreach (var followUp in FollowUps())
            Assert.That(reached, Has.Member(followUp.name));
    }

    [Test]
    public void EveryOption_HasTextInBothLanguages()
    {
        foreach (var dilemma in Dilemmas())
        {
            foreach (var choice in dilemma.ChoiceData)
            {
                Assert.That(choice.Label, Is.Not.Empty, dilemma.name);
                Assert.That(choice.LabelEn, Is.Not.Empty, dilemma.name);
                Assert.That(choice.Detail, Is.Not.Empty, dilemma.name);
                Assert.That(choice.DetailEn, Is.Not.Empty, dilemma.name);
            }
        }
    }

    [Test]
    public void DilemmaAndItsFollowUp_ShareTheSameEntry()
    {
        var events = Events();
        foreach (var dilemma in Dilemmas())
        {
            Assert.That(dilemma.KnowledgeEntry, Is.Not.Null, dilemma.name);
            foreach (var choice in dilemma.Choices.Where(item => item.HasFollowUp))
                Assert.That(events.Single(item => item.name == choice.FollowUpEventId).KnowledgeEntry, Is.SameAs(dilemma.KnowledgeEntry), dilemma.name);
        }
    }

    [Test]
    public void EveryDilemmaAndFollowUp_HasANewsStory()
    {
        var targets = AssetDatabase.FindAssets("t:NewsStoryData", new[] { "Assets/Data" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<NewsStoryData>(AssetDatabase.GUIDToAssetPath(guid)).TargetId).ToList();
        foreach (var item in Dilemmas().Concat(FollowUps()))
            Assert.That(targets, Has.Member(item.name));
    }

    [Test]
    public void TheCity_CanDrawDilemmasAndFindFollowUps()
    {
        BalanceDataFormat.WithCityScene(scene =>
        {
            var pool = BalanceDataFormat.ReadObjectArray<RandomEventData>(new SerializedObject(BalanceDataFormat.FindInScene<TurnManager>(scene)).FindProperty("_eventPool")).ToList();
            foreach (var item in Dilemmas().Concat(FollowUps()))
                Assert.That(pool, Has.Member(item));
            return 0;
        });
    }

    [Test]
    public void Themes_ListDilemmasButNeverFollowUps()
    {
        var followUps = FollowUps().Select(item => item.name).ToList();
        var dilemmas = Dilemmas().Select(item => item.name).ToList();
        foreach (var guid in AssetDatabase.FindAssets("t:ThemedPhaseData", new[] { "Assets/Data" }))
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemedPhaseData>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(theme.AllowedEventIds.Intersect(followUps), Is.Empty, theme.name);
            Assert.That(theme.AllowedEventIds.Intersect(dilemmas).Count(), Is.GreaterThan(0), theme.name);
        }
    }
}
