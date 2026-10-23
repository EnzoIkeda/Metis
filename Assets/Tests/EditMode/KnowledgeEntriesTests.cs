using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

// Confere as fichas reais: toda carta e evento tem ficha, todo texto existe nos dois idiomas e toda ficha
// pode ser desbloqueada.
public class KnowledgeEntriesTests
{
    private const string LibraryPath = "Assets/Data/Knowledge/KnowledgeLibrary.asset";

    // Evento sem efeito, sem tema real para uma ficha.
    private static readonly string[] EventsWithoutEntry = { "EventTurnoTranquilo" };

    private static IEnumerable<T> LoadAll<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));
    }

    private static KnowledgeLibraryData Library => AssetDatabase.LoadAssetAtPath<KnowledgeLibraryData>(LibraryPath);

    [Test]
    public void Library_ListsEveryEntryAssetExactlyOnce()
    {
        var listed = Library.Entries.ToList();
        var assets = LoadAll<KnowledgeEntryData>().ToList();

        Assert.That(listed, Has.No.Member(null));
        Assert.That(listed.Distinct().Count(), Is.EqualTo(listed.Count), "ficha repetida no almanaque");
        Assert.That(listed, Is.EquivalentTo(assets));
    }

    [Test]
    public void EveryCard_HasAnEntryInTheLibrary()
    {
        foreach (var card in LoadAll<CardData>())
        {
            Assert.That(card.KnowledgeEntry, Is.Not.Null, card.name);
            Assert.That(Library.Entries, Has.Member(card.KnowledgeEntry), card.name);
        }
    }

    [Test]
    public void EveryEvent_HasAnEntryInTheLibrary_ExceptTheListedOnes()
    {
        foreach (var randomEvent in LoadAll<RandomEventData>())
        {
            if (EventsWithoutEntry.Contains(randomEvent.name))
                continue;
            Assert.That(randomEvent.KnowledgeEntry, Is.Not.Null, randomEvent.name);
            Assert.That(Library.Entries, Has.Member(randomEvent.KnowledgeEntry), randomEvent.name);
        }
    }

    [Test]
    public void EveryEntry_HasTitleAndSummaryInBothLanguages()
    {
        var previous = LocalizationManager.Current;
        try
        {
            foreach (var language in new[] { Language.Portuguese, Language.English })
            {
                LocalizationManager.Current = language;
                foreach (var entry in Library.Entries)
                {
                    Assert.That(entry.Title, Is.Not.Empty, $"{entry.name} ({language})");
                    Assert.That(entry.Summary, Is.Not.Empty, $"{entry.name} ({language})");
                }
            }

            // Fallback nao pode esconder traducao faltando: os textos dos dois idiomas tem de ser diferentes.
            foreach (var entry in Library.Entries)
            {
                LocalizationManager.Current = Language.Portuguese;
                var pt = entry.Summary;
                LocalizationManager.Current = Language.English;
                Assert.That(entry.Summary, Is.Not.EqualTo(pt), entry.name);
            }
        }
        finally
        {
            LocalizationManager.Current = previous;
        }
    }

    [Test]
    public void EveryEntry_HasAtLeastOneWebSource()
    {
        foreach (var entry in Library.Entries)
        {
            Assert.That(entry.Sources.Count, Is.GreaterThan(0), entry.name);
            foreach (var source in entry.Sources)
            {
                Assert.That(source.Label, Is.Not.Empty, entry.name);
                Assert.That(Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http"), Is.True, $"{entry.name}: {source.Url}");
            }
        }
    }

    [Test]
    public void EveryEntry_CanBeUnlocked()
    {
        var linked = new HashSet<KnowledgeEntryData>(LoadAll<CardData>().Select(card => card.KnowledgeEntry)
            .Concat(LoadAll<RandomEventData>().Select(randomEvent => randomEvent.KnowledgeEntry)));

        foreach (var entry in Library.Entries)
        {
            var byTrigger = entry.UnlockTrigger != KnowledgeUnlockTrigger.None;
            Assert.That(byTrigger || linked.Contains(entry), Is.True, entry.name);
        }
    }

    [Test]
    public void RelatedEntries_PointToOtherEntriesInTheLibrary()
    {
        foreach (var entry in Library.Entries)
        {
            foreach (var related in entry.RelatedEntries)
            {
                Assert.That(related, Is.Not.Null, entry.name);
                Assert.That(related, Is.Not.SameAs(entry), entry.name);
                Assert.That(Library.Entries, Has.Member(related), entry.name);
            }
        }
    }

    [Test]
    public void ParameterEntries_ShowTheParameterIcon()
    {
        var display = AssetDatabase.LoadAssetAtPath<ParameterDisplayData>("Assets/Data/UI/ParameterDisplay.asset");
        var icons = Enum.GetValues(typeof(CityParameterType)).Cast<CityParameterType>().Select(display.GetIcon).ToList();
        var parameterEntries = Library.EntriesIn(KnowledgeCategory.Parameter).ToList();

        Assert.That(parameterEntries.Count, Is.EqualTo(icons.Count));
        Assert.That(parameterEntries.Select(entry => entry.Image), Is.EquivalentTo(icons));
    }

    [Test]
    public void CardEntries_ShowTheCardArtwork_AndOtherEntriesHaveNoImage()
    {
        foreach (var card in LoadAll<CardData>())
        {
            if (card.Artwork != null)
                Assert.That(card.KnowledgeEntry.Image, Is.SameAs(card.Artwork), card.name);
        }
        foreach (var entry in Library.Entries.Where(entry => entry.Category == KnowledgeCategory.Event || entry.Category == KnowledgeCategory.Concept))
            Assert.That(entry.Image, Is.Null, entry.name);
    }

    [Test]
    public void EntriesFor_None_IsEmpty()
    {
        Assert.That(Library.EntriesFor(KnowledgeUnlockTrigger.None), Is.Empty);
        Assert.That(Library.EntriesFor(KnowledgeUnlockTrigger.GameStart).Count(), Is.EqualTo(9), "os 9 parametros");
    }
}
