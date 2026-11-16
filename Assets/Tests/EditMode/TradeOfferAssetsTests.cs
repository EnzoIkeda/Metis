using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

// Confere as ofertas de troca reais: biblioteca completa, regras validas, ficha e noticia de cada uma.
public class TradeOfferAssetsTests
{
    private const string LibraryPath = "Assets/Data/Trades/TradeOfferLibrary.asset";
    private const string KnowledgeLibraryPath = "Assets/Data/Knowledge/KnowledgeLibrary.asset";
    private const string NewsLibraryPath = "Assets/Data/News/NewsLibrary.asset";

    private static TradeOfferLibraryData Library => AssetDatabase.LoadAssetAtPath<TradeOfferLibraryData>(LibraryPath);

    private static IEnumerable<T> LoadAll<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Data" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));
    }

    private static TradeOfferImportFormat.TermDto ToDto(TradeTerm term) => new TradeOfferImportFormat.TermDto
    {
        Kind = term.Kind.ToString(),
        Amount = term.Amount,
        CardId = term.CardId,
        Effects = term.EffectsOrEmpty().Select(effect => new TradeOfferImportFormat.ModifierDto { Parameter = effect.Parameter.ToString(), Amount = effect.Amount }).ToArray(),
    };

    [Test]
    public void Library_ListsEveryOfferAssetExactlyOnce()
    {
        var listed = Library.Offers.ToList();
        Assert.That(listed, Has.No.Member(null));
        Assert.That(listed.Distinct().Count(), Is.EqualTo(listed.Count));
        Assert.That(listed, Is.EquivalentTo(LoadAll<TradeOfferData>().ToList()));
        Assert.That(listed.Count, Is.EqualTo(6));
    }

    [Test]
    public void EveryOffer_PassesTheImportRules()
    {
        var extraOnly = new HashSet<string>(LoadAll<CardData>().Where(card => card.IsExtraOnly).Select(card => card.name));
        var previous = LocalizationManager.Current;
        try
        {
            foreach (var offer in Library.Offers)
            {
                LocalizationManager.Current = Language.Portuguese;
                var namePt = offer.OfferName;
                var summaryPt = offer.Summary;
                LocalizationManager.Current = Language.English;
                var dto = new TradeOfferImportFormat.OfferDto
                {
                    Id = offer.name, NamePt = namePt, NameEn = offer.OfferName, SummaryPt = summaryPt, SummaryEn = offer.Summary,
                    Benefit = ToDto(offer.Benefit), Drawback = ToDto(offer.Drawback),
                };
                Assert.That(TradeOfferImportFormat.Validate(new TradeOfferImportFormat.FileDto { Offers = new[] { dto } }, null, extraOnly), Is.Empty, offer.name);
                Assert.That(dto.SummaryEn, Is.Not.EqualTo(dto.SummaryPt), offer.name);
            }
        }
        finally
        {
            LocalizationManager.Current = previous;
        }
    }

    [Test]
    public void EveryOffer_HasAFichaUnlockedByTheOffer_AndANewsStory()
    {
        var knowledge = AssetDatabase.LoadAssetAtPath<KnowledgeLibraryData>(KnowledgeLibraryPath);
        var news = AssetDatabase.LoadAssetAtPath<NewsLibraryData>(NewsLibraryPath);
        foreach (var offer in Library.Offers)
        {
            Assert.That(offer.KnowledgeEntry, Is.Not.Null, offer.name);
            Assert.That(knowledge.Entries, Has.Member(offer.KnowledgeEntry), offer.name);
            Assert.That(offer.KnowledgeEntry.UnlockTrigger, Is.EqualTo(KnowledgeUnlockTrigger.TradeOffer), offer.name);
            Assert.That(news.Stories.Any(story => story.Trigger == NewsTrigger.TradeAccepted && story.TargetId == offer.name), Is.True, offer.name);
        }
    }

    [Test]
    public void CityScene_HasTheLibraryAndEveryExtraCardInThePool()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/City_Scene.unity", OpenSceneMode.Additive);
        try
        {
            var turnManager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TurnManager>(true)).Single();
            var so = new SerializedObject(turnManager);
            Assert.That(so.FindProperty("_trades").objectReferenceValue, Is.EqualTo(Library));
            foreach (var offer in Library.Offers)
            {
                foreach (var term in new[] { offer.Benefit, offer.Drawback })
                {
                    if (term.Kind == TradeTermKind.ExtraCard)
                        Assert.That(turnManager.CardPool.Any(card => card != null && card.name == term.CardId && card.IsExtraOnly), Is.True, offer.name);
                }
            }

            // Carta de troca nunca vem num baralho nem na recompensa.
            foreach (CardArchetype archetype in System.Enum.GetValues(typeof(CardArchetype)))
                Assert.That(DeckBuilder.Build(turnManager.CardPool, archetype, new List<string>()).Any(card => card.IsExtraOnly), Is.False, archetype.ToString());
            Assert.That(RewardOptionPicker.CardPool(turnManager.CardPool, new List<CardData>()).Any(card => card.IsExtraOnly), Is.False);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
