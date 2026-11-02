using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Metis.Core.Tests;

public class GameTextEditingTests
{
    private const string News = "{\n  \"Stories\": [\n    {\n      \"Id\": \"A\",\n      \"HeadlinePt\": \"Caixa em {valor}\",\n      \"HeadlineEn\": \"Cash at {valor}\"\n    },\n    {\n      \"Id\": \"B\",\n      \"HeadlinePt\": \"Linha \\\"um\\\"\\nacentuação\",\n      \"HeadlineEn\": \"x\",\n      \"Sources\": [{\"Url\": \"http://a\"}],\n      \"Count\": 3,\n      \"Empty\": null\n    }\n  ]\n}\n";
    private const string Rows = "[\n [\"Energia\",\"Renda\",\"Texto\",\"Text\",\"\"],\n [\"Saude\",\"Renda\",\"Outro\",\"Other\",\"\"]\n]\n";

    private static ContentJsonEditor.Change Key(string key, string expected, string value)
    {
        return new ContentJsonEditor.Change { Key = key, Expected = expected, Value = value };
    }

    private static ContentJsonEditor.Change Cell(int cell, string expected, string value)
    {
        return new ContentJsonEditor.Change { Cell = cell, Expected = expected, Value = value };
    }

    [Test]
    public void ReplacingOnlyTouchesTheChosenValues()
    {
        var ok = ContentJsonEditor.TryReplaceInObject(News, "Stories", "A", new[] { Key("HeadlinePt", "Caixa em {valor}", "Novo"), Key("HeadlineEn", "Cash at {valor}", "New") }, out var result, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(result, Is.EqualTo(News.Replace("Caixa em {valor}", "Novo").Replace("Cash at {valor}", "New")));
    }

    [Test]
    public void EncodesQuotesLineBreaksAndKeepsAccents()
    {
        var ok = ContentJsonEditor.TryReplaceInObject(News, "Stories", "A", new[] { Key("HeadlinePt", "Caixa em {valor}", "Não \"sim\"\nfim\\") }, out var result, out _);

        Assert.That(ok, Is.True);
        Assert.That(result, Does.Contain("\"HeadlinePt\": \"Não \\\"sim\\\"\\nfim\\\\\""));
    }

    [Test]
    public void ExpectedValueIsComparedAfterDecoding()
    {
        var ok = ContentJsonEditor.TryReplaceInObject(News, "Stories", "B", new[] { Key("HeadlinePt", "Linha \"um\"\nacentuação", "ok") }, out var result, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(result, Does.Contain("\"HeadlinePt\": \"ok\""));
        Assert.That(result, Does.Contain("\"Count\": 3"));
    }

    [Test]
    public void RefusesWhenFileDiffersFromAsset()
    {
        var ok = ContentJsonEditor.TryReplaceInObject(News, "Stories", "A", new[] { Key("HeadlinePt", "outro", "Novo") }, out var result, out var error);

        Assert.That(ok, Is.False);
        Assert.That(result, Is.EqualTo(News));
        Assert.That(error, Does.Contain("difere"));
    }

    [Test]
    public void AllChangesAreAppliedOrNone()
    {
        var ok = ContentJsonEditor.TryReplaceInObject(News, "Stories", "A", new[] { Key("HeadlinePt", "Caixa em {valor}", "Novo"), Key("HeadlineEn", "errado", "New") }, out var result, out _);

        Assert.That(ok, Is.False);
        Assert.That(result, Is.EqualTo(News));
    }

    [TestCase("Z", "HeadlinePt")]
    [TestCase("A", "NaoExiste")]
    public void RefusesUnknownIdOrField(string id, string field)
    {
        Assert.That(ContentJsonEditor.TryReplaceInObject(News, "Stories", id, new[] { Key(field, "", "x") }, out _, out var error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void ReplacesCellsOfTheRightRow()
    {
        var ok = ContentJsonEditor.TryReplaceInRow(Rows, "Saude", "Renda", new[] { Cell(2, "Outro", "Novo"), Cell(3, "Other", "New") }, out var result, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(result, Is.EqualTo(Rows.Replace("Outro", "Novo").Replace("Other", "New")));
    }

    [Test]
    public void RefusesBrokenJson()
    {
        Assert.That(ContentJsonEditor.TryReplaceInObject("{\"Stories\": [", "Stories", "A", new[] { Key("X", "", "y") }, out _, out var error), Is.False);
        Assert.That(error, Does.Contain("JSON"));
    }

    private const string Table = "public static class T\n{\n    public static string Play => IsPt ? \"Jogar\" : \"Play\";\n    public static string Turn(int turn) => IsPt ? $\"Turno {turn}\" : $\"Turn {turn}\";\n    public static string Long => IsPt\n        ? \"Linha um\\n\" +\n          \"linha dois\"\n        : \"Line one\\n\" +\n          \"line two\";\n    public static string Mixed(string who) => IsPt ? \"Oi \" + who : \"Hi \" + who;\n    public static string Single => \"Sem par\";\n}\n";

    private static GameTextItem Parsed(string source, string id)
    {
        return InterfaceTextParser.Parse(source, "t.cs").First(item => item.Id == id);
    }

    private static string RealTable([CallerFilePath] string self = "")
    {
        return File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(self)!, "..", "..", "Assets/Scripts/Localization/UIStrings.cs")));
    }

    [Test]
    public void InterfaceEditChangesOnlyTheChosenLiteral()
    {
        var ok = InterfaceTextEditor.TryEdit(Table, "t.cs", Parsed(Table, "Play"), "Começar", "Play", out var result, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(result, Is.EqualTo(Table.Replace("\"Jogar\"", "\"Começar\"")));
    }

    [Test]
    public void InterfaceEditKeepsVariablesAndAllowsMovingThem()
    {
        var ok = InterfaceTextEditor.TryEdit(Table, "t.cs", Parsed(Table, "Turn"), "{turn}º turno", "Turn {turn}", out var result, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(result, Does.Contain("$\"{turn}º turno\""));
        Assert.That(result, Does.Contain("$\"Turn {turn}\""));
    }

    [TestCase("Turno {outra}", "variável")]
    [TestCase("Turno", "perdeu")]
    public void InterfaceEditRefusesChangedVariables(string pt, string message)
    {
        var ok = InterfaceTextEditor.TryEdit(Table, "t.cs", Parsed(Table, "Turn"), pt, "Turn {turn}", out var result, out var error);

        Assert.That(ok, Is.False);
        Assert.That(result, Is.EqualTo(Table));
        Assert.That(error, Does.Contain(message));
    }

    [Test]
    public void InterfaceEditWritesLineBreaksAndEscapes()
    {
        var ok = InterfaceTextEditor.TryEdit(Table, "t.cs", Parsed(Table, "Long"), "Um \"dois\"\ntrês", "Line one\nline two", out var result, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(result, Does.Contain("? \"Um \\\"dois\\\"\\n\" +\n          \"três\"\n"));
        Assert.That(Parsed(result, "Long").Pt, Is.EqualTo("Um \"dois\"\ntrês"));
        Assert.That(result, Does.Contain("\"Line one\\n\" +\n          \"line two\";"));
    }

    [Test]
    public void InterfaceEditKeepsParagraphBreaksTogetherAndWrapsLongText()
    {
        var longLine = string.Join(" ", Enumerable.Repeat("palavra", 40));
        var ok = InterfaceTextEditor.TryEdit(Table, "t.cs", Parsed(Table, "Long"), "Um\n\n" + longLine, "Line one\nline two", out var result, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(result, Does.Contain("\"Um\\n\\n\" +"));
        Assert.That(result.Split('\n').Max(line => line.Length), Is.LessThan(125));
        Assert.That(Parsed(result, "Long").Pt, Is.EqualTo("Um\n\n" + longLine));
    }

    [Test]
    public void InterfaceEditKeepsWindowsLineEndings()
    {
        var source = Table.Replace("\n", "\r\n").Replace("\\r\n", "\\n");
        var ok = InterfaceTextEditor.TryEdit(source, "t.cs", Parsed(source, "Long"), "a\nb", "Line one\nline two", out var result, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(result, Does.Contain("\"a\\n\" +\r\n          \"b\""));
    }

    [Test]
    public void MixedCodeIsReadOnlyAndCannotBeEdited()
    {
        var item = Parsed(Table, "Mixed");
        Assert.That(item.EditKind, Is.EqualTo(GameTextEditKind.ReadOnly));
        Assert.That(item.ReadOnlyReason, Is.Not.Empty);
        Assert.That(InterfaceTextEditor.TryEdit(Table, "t.cs", item, "x", "y", out var result, out _), Is.False);
        Assert.That(result, Is.EqualTo(Table));
    }

    [Test]
    public void UnpairedLiteralCanBeEdited()
    {
        var ok = InterfaceTextEditor.TryEdit(Table, "t.cs", Parsed(Table, "Single"), "Outro", null, out var result, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(result, Is.EqualTo(Table.Replace("\"Sem par\"", "\"Outro\"")));
    }

    [Test]
    public void InterfaceEditRefusesTextChangedOnDisk()
    {
        var stale = Parsed(Table, "Play");
        stale.Pt = "Antigo";
        Assert.That(InterfaceTextEditor.TryEdit(Table, "t.cs", stale, "Novo", "Play", out _, out var error), Is.False);
        Assert.That(error, Does.Contain("mudou"));
    }

    [Test]
    public void EveryEditableTextOfTheRealTableSurvivesAnEdit()
    {
        var source = RealTable();
        var items = InterfaceTextParser.Parse(source, "UIStrings.cs");
        var editable = items.Where(item => item.EditKind == GameTextEditKind.Interface).ToList();

        Assert.That(editable.Count, Is.GreaterThan(items.Count / 2));
        foreach (var item in editable)
        {
            var en = item.IsPaired ? item.En + " y" : null;
            var ok = InterfaceTextEditor.TryEdit(source, "UIStrings.cs", item, item.Pt + " x", en, out var result, out var error);
            Assert.That(ok, Is.True, $"{item.Id} {item.Field}: {error}");
            Assert.That(result, Is.Not.EqualTo(source));
        }
    }

    [Test]
    public void SavingTheSameTextOfTheRealTableChangesNothing()
    {
        var source = RealTable();
        foreach (var item in InterfaceTextParser.Parse(source, "UIStrings.cs").Where(item => item.EditKind == GameTextEditKind.Interface))
        {
            Assert.That(InterfaceTextEditor.TryEdit(source, "UIStrings.cs", item, item.Pt, item.En, out var result, out _), Is.True);
            Assert.That(result, Is.EqualTo(source));
        }
    }

    private static GameTextItem Editable(string pt = "Texto", string en = "Text")
    {
        return new GameTextItem { Origin = GameTextOrigin.Card, Id = "CardX", Field = "_a", Location = "a.asset", Pt = pt, En = en, EditKind = GameTextEditKind.Asset };
    }

    [Test]
    public void SaveRulesBlockDashEmptyingAndReadOnly()
    {
        Assert.That(GameTextSaveRules.Errors(Editable(), "Novo", "New"), Is.Empty);
        Assert.That(GameTextSaveRules.Errors(Editable(), "a \u2014 b", "New"), Has.Count.EqualTo(1));
        Assert.That(GameTextSaveRules.Errors(Editable(), " ", "New"), Has.Count.EqualTo(1));
        Assert.That(GameTextSaveRules.Errors(Editable(), "Novo", ""), Has.Count.EqualTo(1));
        Assert.That(GameTextSaveRules.Errors(new GameTextItem { Pt = "a", En = "b" }, "c", "d"), Has.Count.EqualTo(1));
    }

    [Test]
    public void SaveRulesAllowFillingAMissingTranslation()
    {
        Assert.That(GameTextSaveRules.Errors(Editable(en: ""), "Texto", ""), Is.Empty);
    }

    [Test]
    public void HasChangeIgnoresLineEndingStyleAndEnglishOfUnpaired()
    {
        Assert.That(GameTextSaveRules.HasChange(Editable("a\nb"), "a\r\nb", "Text"), Is.False);
        Assert.That(GameTextSaveRules.HasChange(Editable(), "Texto", "Other"), Is.True);
        var single = new GameTextItem { Pt = "a", IsPaired = false };
        Assert.That(GameTextSaveRules.HasChange(single, "a", "qualquer"), Is.False);
    }

    [Test]
    public void ReviewMarkLastsUntilTheTextChanges()
    {
        var review = new GameTextReview();
        var item = Editable();
        Assert.That(review.IsReviewed(item), Is.False);

        review.Mark(item, "2026-11-03");
        Assert.That(review.IsReviewed(item), Is.True);

        item.En = "Changed";
        Assert.That(review.IsReviewed(item), Is.False);
        item.En = "Text";
        Assert.That(review.IsReviewed(item), Is.True);

        review.Unmark(item);
        Assert.That(review.IsReviewed(item), Is.False);
    }

    [Test]
    public void ReviewSurvivesTheFileRoundTripAndKeysDifferByField()
    {
        var review = new GameTextReview();
        var item = Editable();
        review.Mark(item, "2026-11-03");

        var loaded = new GameTextReview(review.ToFile());
        Assert.That(loaded.IsReviewed(item), Is.True);

        var other = Editable();
        other.Field = "_b";
        Assert.That(loaded.IsReviewed(other), Is.False);
    }
}
