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
