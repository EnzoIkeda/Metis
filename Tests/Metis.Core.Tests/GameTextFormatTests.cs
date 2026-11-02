using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Metis.Core.Tests;

public class GameTextFormatTests
{
    private static GameTextItem Pair(string pt, string en, GameTextOrigin origin = GameTextOrigin.Card)
    {
        return new GameTextItem { Origin = origin, Pt = pt, En = en };
    }

    private static string RepoFile(string relative, [CallerFilePath] string self = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(self)!, "..", "..", relative));
    }

    [Test]
    public void CleanPairHasNoIssues()
    {
        Assert.That(GameTextRules.Check(Pair("Métis tem praças", "Métis has squares")), Is.Empty);
    }

    [TestCase("", "Text", GameTextIssue.MissingPortuguese)]
    [TestCase("Texto", "", GameTextIssue.MissingEnglish)]
    [TestCase("Curitiba Brasil", "Curitiba Brasil", GameTextIssue.LikelyUntranslated)]
    [TestCase("Turno {turn}", "Turn {index}", GameTextIssue.PlaceholderMismatch)]
    [TestCase("a \u2014 b", "a b", GameTextIssue.Dash)]
    [TestCase("a \u2013 b", "a b", GameTextIssue.Dash)]
    [TestCase("Bem-vindo a Metis", "Welcome to Métis", GameTextIssue.MetisWithoutAccent)]
    [TestCase("A fase dura 20 anos", "The phase lasts 20 turns", GameTextIssue.YearsInsteadOfTurns)]
    [TestCase("dois  espacos", "two spaces", GameTextIssue.ExtraSpace)]
    [TestCase("fim ", "end", GameTextIssue.ExtraSpace)]
    public void DetectsIssue(string pt, string en, GameTextIssue expected)
    {
        Assert.That(GameTextRules.Check(Pair(pt, en)), Has.Member(expected));
    }

    [Test]
    public void NumbersWithoutTranslationAreNotMissing()
    {
        Assert.That(GameTextRules.Check(Pair("1.234", "")), Is.Empty);
    }

    [Test]
    public void UnpairedTextDoesNotNeedEnglish()
    {
        var item = new GameTextItem { Pt = "Jogar", IsPaired = false };
        Assert.That(GameTextRules.Check(item), Is.Empty);
    }

    [Test]
    public void YearsAreOnlyAFaultOutsideFactsWhenPhaseLength()
    {
        Assert.That(GameTextRules.Check(Pair("Em 10 anos a cidade cresceu", "In 10 years the city grew")), Is.Empty);
        Assert.That(GameTextRules.Check(Pair("Em 10 anos", "In 10 years", GameTextOrigin.Interface)), Has.Member(GameTextIssue.YearsInsteadOfTurns));
    }

    [Test]
    public void InterfaceSeparatorSpaceIsNotAFault()
    {
        Assert.That(GameTextRules.Check(Pair("Idioma: ", "Language: ", GameTextOrigin.Interface)), Is.Empty);
        Assert.That(GameTextRules.Check(Pair("dois  espacos", "two spaces", GameTextOrigin.Interface)), Has.Member(GameTextIssue.ExtraSpace));
    }

    [Test]
    public void TitleFontOnlyFlagsAccentWhenUsed()
    {
        var withFont = new GameTextItem { Pt = "Configurações", IsPaired = false, UsesTitleFont = true };
        var withoutFont = new GameTextItem { Pt = "Configurações", IsPaired = false };
        Assert.That(GameTextRules.Check(withFont), Has.Member(GameTextIssue.AccentInTitleFont));
        Assert.That(GameTextRules.Check(withoutFont), Is.Empty);
    }

    [Test]
    public void MatchesIgnoresCaseAndSearchesBothLanguages()
    {
        var item = Pair("Ciclovias", "Bike lanes");
        Assert.That(GameTextRules.Matches(item, "bike"), Is.True);
        Assert.That(GameTextRules.Matches(item, "CICLO"), Is.True);
        Assert.That(GameTextRules.Matches(item, "metro"), Is.False);
        Assert.That(GameTextRules.Matches(item, "  "), Is.True);
    }

    [Test]
    public void ParserReadsSimpleConcatenatedAndInterpolatedPairs()
    {
        const string source = """
            public static class T
            {
                private static bool IsPt => true;
                public static string Play => IsPt ? "Jogar" : "Play";
                public static string Long => IsPt
                    ? "Linha um\n" +
                      "linha dois"
                    : "Line one\n" +
                      "line two";
                public static string Counter(int turn) => IsPt ? $"Turno {turn}" : $"Turn {turn}";
                public static string Nested(bool s) => IsPt ? $"forte{(s ? "" : "a")}: fim" : "strong: end";
            }
            """;
        var items = InterfaceTextParser.Parse(source, "x.cs");

        Assert.That(items.Select(i => i.Id), Is.EqualTo(new[] { "Play", "Long", "Counter", "Nested" }));
        Assert.That(items[0].Pt, Is.EqualTo("Jogar"));
        Assert.That(items[0].En, Is.EqualTo("Play"));
        Assert.That(items[1].Pt, Is.EqualTo("Linha um\nlinha dois"));
        Assert.That(items[1].En, Is.EqualTo("Line one\nline two"));
        Assert.That(items[2].En, Is.EqualTo("Turn {turn}"));
        Assert.That(items[3].Pt, Is.EqualTo("forte{(s ? \"\" : \"a\")}: fim"));
        Assert.That(items[3].En, Is.EqualTo("strong: end"));
        Assert.That(items[0].Line, Is.EqualTo(4));
    }

    [Test]
    public void ParserKeepsCaseLabelsAndCodeParts()
    {
        const string source = """
            public static class T
            {
                public static string Name(int kind, string who)
                {
                    switch (kind)
                    {
                        case Kind.A: return IsPt ? "Um " + who : "One " + who;
                        case Kind.B: return IsPt ? "Dois" : "Two";
                        default: return "Sem par";
                    }
                }
            }
            """;
        var items = InterfaceTextParser.Parse(source, "x.cs");

        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(items[0].Field, Is.EqualTo("Kind.A"));
        Assert.That(items[0].Pt, Is.EqualTo("Um {...}"));
        Assert.That(items[0].En, Is.EqualTo("One {...}"));
        Assert.That(items[1].Field, Is.EqualTo("Kind.B"));
        Assert.That(items[2].IsPaired, Is.False);
        Assert.That(items[2].Pt, Is.EqualTo("Sem par"));
        Assert.That(items[2].Field, Is.EqualTo("default"));
    }

    [Test]
    public void ParserFindsEveryLanguagePairInTheRealTable()
    {
        var source = File.ReadAllText(RepoFile("Assets/Scripts/Localization/UIStrings.cs"));
        var expected = Regex.Matches(source, @"\bIsPt\s*\?").Count;
        var pairs = InterfaceTextParser.Parse(source, "UIStrings.cs").Where(i => i.IsPaired).ToList();

        Assert.That(pairs, Has.Count.EqualTo(expected));
        Assert.That(pairs.Where(i => GameTextRules.HasLetter(i.Pt) == false && GameTextRules.HasLetter(i.En) == false).Select(i => i.Id), Is.Empty);
    }

    [Test]
    public void SceneParserDecodesScalarsNamesAndFont()
    {
        const string yaml = """
            %YAML 1.1
            --- !u!1 &10
            GameObject:
              m_Name: Botao
            --- !u!114 &20
            MonoBehaviour:
              m_GameObject: {fileID: 10}
              m_text: "Configura\xE7\xF5es"
              m_isRightToLeft: 0
              m_fontAsset: {fileID: 1, guid: abc, type: 2}
            --- !u!114 &21
            MonoBehaviour:
              m_GameObject: {fileID: 99}
              m_text: 'It''s ok'
              m_fontAsset: {fileID: 1, guid: def, type: 2}
            --- !u!114 &22
            MonoBehaviour:
              m_GameObject: {fileID: 99}
              m_text: "Linha um\n
            
                segunda parte \
                colada"
              m_fontAsset: {fileID: 1, guid: def, type: 2}
            --- !u!114 &23
            MonoBehaviour:
              m_GameObject: {fileID: 99}
              m_text: 123
            """;
        var items = SceneTextParser.Parse(yaml, "c.unity", "abc");

        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(items[0].Pt, Is.EqualTo("Configurações"));
        Assert.That(items[0].Id, Is.EqualTo("Botao"));
        Assert.That(items[0].UsesTitleFont, Is.True);
        Assert.That(items[0].Line, Is.EqualTo(8));
        Assert.That(items[1].Pt, Is.EqualTo("It's ok"));
        Assert.That(items[1].UsesTitleFont, Is.False);
        Assert.That(items[2].Pt, Does.StartWith("Linha um\n"));
        Assert.That(items[2].Pt, Does.EndWith("segunda parte colada"));
    }
}
