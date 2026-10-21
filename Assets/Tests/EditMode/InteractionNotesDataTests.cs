using NUnit.Framework;
using UnityEditor;

public class InteractionNotesDataTests
{
    private const string AssetPath = "Assets/Data/UI/InteractionNotes.asset";

    // Toda ligacao do mapa precisa de texto de lastro nos dois idiomas.
    [Test]
    public void RealAsset_HasANoteInBothLanguagesForEveryRelation()
    {
        var notes = AssetDatabase.LoadAssetAtPath<InteractionNotesData>(AssetPath);
        Assert.That(notes, Is.Not.Null);

        foreach (var edge in RelationGraph.Build(null))
        {
            Assert.That(notes.TryGetNote(edge.From, edge.To, out var note), Is.True, $"{edge.From} -> {edge.To}");
            Assert.That(note.TextPt, Is.Not.Empty, $"{edge.From} -> {edge.To} (PT)");
            Assert.That(note.TextEn, Is.Not.Empty, $"{edge.From} -> {edge.To} (EN)");
        }
    }

    [Test]
    public void GetText_FollowsTheCurrentLanguage()
    {
        var notes = AssetDatabase.LoadAssetAtPath<InteractionNotesData>(AssetPath);
        var previous = LocalizationManager.Current;
        try
        {
            notes.TryGetNote(CityParameterType.BemEstar, CityParameterType.Populacao, out var note);
            LocalizationManager.Current = Language.Portuguese;
            Assert.That(notes.GetText(CityParameterType.BemEstar, CityParameterType.Populacao), Is.EqualTo(note.TextPt));
            LocalizationManager.Current = Language.English;
            Assert.That(notes.GetText(CityParameterType.BemEstar, CityParameterType.Populacao), Is.EqualTo(note.TextEn));
        }
        finally
        {
            LocalizationManager.Current = previous;
        }
    }
}
