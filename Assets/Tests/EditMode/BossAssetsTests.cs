using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

// Confere os chefes reais: biblioteca completa, regras validas, ficha, noticias e as ligacoes nas cenas.
public class BossAssetsTests
{
    private const string LibraryPath = "Assets/Data/Bosses/BossLibrary.asset";
    private const string NewsLibraryPath = "Assets/Data/News/NewsLibrary.asset";

    private static BossLibraryData Library => AssetDatabase.LoadAssetAtPath<BossLibraryData>(LibraryPath);

    private static IEnumerable<T> LoadAll<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Data" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));
    }

    [Test]
    public void Library_ListsEveryBossExactlyOnce()
    {
        var listed = Library.Bosses.ToList();
        Assert.That(listed, Has.No.Member(null));
        Assert.That(listed, Is.EquivalentTo(LoadAll<BossData>().ToList()));
        Assert.That(listed.Select(boss => boss.TargetArchetype).Distinct().Count(), Is.EqualTo(listed.Count));
        foreach (var archetype in new[] { CardArchetype.Sustentabilidade, CardArchetype.Industria, CardArchetype.Automacao, CardArchetype.Comunitario })
            Assert.That(Library.ForArchetype(archetype), Is.Not.Null, $"todo baralho tem chefe: {archetype}");
    }

    [Test]
    public void EveryBoss_PassesTheImportRulesInBothLanguages()
    {
        var previous = LocalizationManager.Current;
        try
        {
            foreach (var boss in Library.Bosses)
            {
                LocalizationManager.Current = Language.Portuguese;
                var pt = (boss.BossName, boss.Summary, boss.Intro, boss.Victory, Cards: boss.BossCards.Select(card => (card.CardName, card.Description)).ToList());
                LocalizationManager.Current = Language.English;
                var dto = new BossImportFormat.BossDto
                {
                    Id = boss.name,
                    NamePt = pt.BossName, NameEn = boss.BossName,
                    SummaryPt = pt.Summary, SummaryEn = boss.Summary,
                    IntroPt = pt.Intro, IntroEn = boss.Intro,
                    VictoryPt = pt.Victory, VictoryEn = boss.Victory,
                    TargetArchetype = boss.TargetArchetype.ToString(),
                    StartingMomentum = boss.StartingMomentum,
                    MomentumPerTurn = boss.MomentumPerTurn,
                    Mechanic = boss.Mechanic.ToString(),
                    MechanicAmount = boss.MechanicAmount,
                    MechanicCardId = boss.MechanicCardId,
                    Cards = boss.BossCards.Select((card, i) => new BossImportFormat.CardDto
                    {
                        Id = card.Id,
                        NamePt = pt.Cards[i].CardName, NameEn = card.CardName,
                        DescriptionPt = pt.Cards[i].Description, DescriptionEn = card.Description,
                        MomentumCost = card.MomentumCost,
                        Effects = card.Effects.Select(effect => new BossImportFormat.ModifierDto { Parameter = effect.Parameter.ToString(), Amount = effect.Amount }).ToArray(),
                    }).ToArray(),
                };
                var extraOnly = LoadAll<CardData>().Where(card => card.IsExtraOnly).Select(card => card.name).ToList();
                Assert.That(BossImportFormat.Validate(new BossImportFormat.FileDto { Bosses = new[] { dto } }, null, extraOnly), Is.Empty, boss.name);
                Assert.That(dto.IntroEn, Is.Not.EqualTo(dto.IntroPt), boss.name);
            }
        }
        finally
        {
            LocalizationManager.Current = previous;
        }
    }

    [Test]
    public void EveryBoss_HasABossFichaAndANewsStoryPerCard()
    {
        var news = AssetDatabase.LoadAssetAtPath<NewsLibraryData>(NewsLibraryPath).Stories
            .Where(story => story.Trigger == NewsTrigger.BossCardPlayed).Select(story => story.TargetId).ToList();
        foreach (var boss in Library.Bosses)
        {
            Assert.That(boss.KnowledgeEntry, Is.Not.Null, boss.name);
            Assert.That(boss.KnowledgeEntry.UnlockTrigger, Is.EqualTo(KnowledgeUnlockTrigger.Boss), boss.name);
            foreach (var card in boss.BossCards)
                Assert.That(news, Has.Member(card.Id), card.Id);
        }
    }

    [Test]
    public void Scenes_ReferenceTheLibraryAndTheTutorialPointsAtTheHud()
    {
        var previous = EditorSceneManager.GetActiveScene().path;
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/City_Scene.unity");
            var turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            Assert.That(new SerializedObject(turnManager).FindProperty("_bosses").objectReferenceValue, Is.EqualTo(Library));
            foreach (var boss in Library.Bosses.Where(boss => boss.Mechanic == BossMechanic.ExtraCards))
                Assert.That(turnManager.CardPool.Select(card => card.name), Has.Member(boss.MechanicCardId), "a carta extra precisa estar no conjunto da cena para o save");
            var hud = UnityEngine.Object.FindFirstObjectByType<BossHudView>(UnityEngine.FindObjectsInactive.Include);
            var popup = UnityEngine.Object.FindFirstObjectByType<BossPlayPopupView>(UnityEngine.FindObjectsInactive.Include);
            Assert.That(hud, Is.Not.Null);
            Assert.That(popup, Is.Not.Null);

            var tutorial = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<TutorialTipView>(UnityEngine.FindObjectsInactive.Include));
            var panel = new SerializedObject(popup).FindProperty("_panelRoot").objectReferenceValue;
            var blocking = tutorial.FindProperty("_blockingPanels");
            Assert.That(Enumerable.Range(0, blocking.arraySize).Select(i => blocking.GetArrayElementAtIndex(i).objectReferenceValue), Has.Member(panel));
            var bindings = tutorial.FindProperty("_bindings");
            var bossBinding = Enumerable.Range(0, bindings.arraySize).Select(bindings.GetArrayElementAtIndex)
                .Single(binding => binding.FindPropertyRelative("Tip").enumValueIndex == (int)TutorialTip.Boss);
            Assert.That(bossBinding.FindPropertyRelative("Anchor").objectReferenceValue, Is.Not.Null);

            EditorSceneManager.OpenScene("Assets/Scenes/PhaseMap.unity");
            var map = UnityEngine.Object.FindFirstObjectByType<PhaseMapView>(UnityEngine.FindObjectsInactive.Include);
            Assert.That(new SerializedObject(map).FindProperty("_bosses").objectReferenceValue, Is.EqualTo(Library));
        }
        finally
        {
            if (string.IsNullOrEmpty(previous) == false)
                EditorSceneManager.OpenScene(previous);
        }
    }
}
