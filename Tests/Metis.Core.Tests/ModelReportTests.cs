using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class ModelReportTests
{
    // Medida falsa: 10 por caractere de largura, linhas de 30 quebradas na largura dada.
    private static float Width(string text, ReportCellStyle style) => text.Length * 10f;

    private static float Height(string text, ReportCellStyle style, float width)
    {
        var lines = Math.Max(1, (int)Math.Ceiling(text.Length * 10f / Math.Max(1f, width)));
        return lines * 30f;
    }

    private static ReportText T(string pt, string en = null, string tag = null) => new ReportText { Pt = pt, En = en ?? pt + " EN", Tag = tag };

    private static ReportSheet Sheet(string id = "Aba", int columns = 3, string auto = "")
    {
        return new ReportSheet
        {
            Id = id,
            Tab = T("Aba"),
            Title = T("Titulo"),
            Intro = T("Introducao curta"),
            Auto = auto,
            Columns = Enumerable.Range(0, columns).Select(i => T($"Coluna {i}")).ToArray(),
            Rows = new[]
            {
                new ReportRow { Cells = Enumerable.Range(0, columns).Select(i => T(i == 0 ? "Linha" : $"+0,{i}")).ToArray() },
            },
            Notes = new[] { T("Primeira nota"), T("Segunda nota") },
            SeeAlso = new[] { "Ficha_A", ModelReportLinks.RelationMap },
        };
    }

    private static ReportFile File(params ReportSheet[] sheets)
    {
        return new ReportFile { Version = ModelReportImportFormat.CurrentVersion, ModelVersion = "1.0", Fingerprint = "P:1", Sheets = sheets };
    }

    private static ReportLayout Build(ReportSheet sheet, ReportLabels labels = null, ReportLayoutOptions options = null, bool english = false)
    {
        return ReportSheetLayout.Build(sheet, english, labels ?? new ReportLabels { AutoTag = "Auto" }, options ?? new ReportLayoutOptions(), Width, Height);
    }

    private static List<ReportCell> Content(ReportLayout layout) => layout.Cells;

    [Test]
    public void Text_FallsBackToTheOtherLanguage()
    {
        Assert.That(new ReportText { Pt = "a", En = "" }.Get(true), Is.EqualTo("a"));
        Assert.That(new ReportText { Pt = null, En = "b" }.Get(false), Is.EqualTo("b"));
        Assert.That(new ReportText().Get(false), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Layout_StartsWithTitleAcrossThePage()
    {
        var options = new ReportLayoutOptions { PageWidth = 800f };
        var cells = Content(Build(Sheet(), options: options));

        Assert.That(cells[0].Style, Is.EqualTo(ReportCellStyle.Title));
        Assert.That(cells[0].X, Is.EqualTo(0f));
        Assert.That(cells[0].Width, Is.EqualTo(800f));
    }

    [Test]
    public void Layout_UsesTheRequestedLanguage()
    {
        var cells = Content(Build(Sheet(), english: true));

        Assert.That(cells[0].Text, Is.EqualTo("Titulo EN"));
        Assert.That(cells.Any(c => c.Text == "Primeira nota EN"), Is.True);
    }

    [Test]
    public void Layout_PutsHeaderThenRowsThenNumberedNotes()
    {
        var labels = new ReportLabels { Notes = "Notas", NoteFormat = "Obs. {0}" };
        var cells = Content(Build(Sheet(), labels));

        var header = cells.Where(c => c.Style == ReportCellStyle.ColumnHeader).Select(c => c.Text);
        Assert.That(header, Is.EqualTo(new[] { "Coluna 0", "Coluna 1", "Coluna 2" }));
        var rowHeader = cells.Single(c => c.Style == ReportCellStyle.RowHeader);
        Assert.That(rowHeader.Text, Is.EqualTo("Linha"));
        Assert.That(cells.Where(c => c.Style == ReportCellStyle.NoteLabel).Select(c => c.Text), Is.EqualTo(new[] { "Obs. 1", "Obs. 2" }));
        Assert.That(cells.Single(c => c.Text == "Notas").Style, Is.EqualTo(ReportCellStyle.Label));
        Assert.That(rowHeader.Row, Is.GreaterThan(cells.First(c => c.Style == ReportCellStyle.ColumnHeader).Row));
    }

    [Test]
    public void Layout_HeaderFieldsComeRightAfterTheTitle_WithTheValueFillingTheLine()
    {
        var sheet = Sheet();
        sheet.Header = new[] { new ReportHeaderField { Field = T("Para"), Value = T("Gabinete") } };
        var options = new ReportLayoutOptions { PageWidth = 800f };
        var cells = Content(Build(sheet, options: options));

        var field = cells.Single(c => c.Text == "Para");
        var value = cells.Single(c => c.Text == "Gabinete");
        Assert.That(field.Row, Is.EqualTo(1));
        Assert.That(field.Style, Is.EqualTo(ReportCellStyle.Label));
        Assert.That(value.X, Is.EqualTo(field.Width).Within(1e-3));
        Assert.That(value.X + value.Width, Is.EqualTo(800f).Within(1e-3));
    }

    [Test]
    public void Layout_ShowsAutoTagOnlyForGeneratedTables()
    {
        Assert.That(Content(Build(Sheet(auto: "Parameters"))).Any(c => c.Style == ReportCellStyle.AutoTag), Is.True);
        Assert.That(Content(Build(Sheet())).Any(c => c.Style == ReportCellStyle.AutoTag), Is.False);
    }

    [Test]
    public void Layout_MatrixCellsCarrySignAndStrength()
    {
        var sheet = Sheet(auto: "Matrix");
        sheet.Rows = new[] { new ReportRow { Cells = new[] { T("Renda"), T("+0,5"), T("-1,0") } }, new ReportRow { Cells = new[] { T("Energia"), T("", ""), T("+0.25") } } };
        var cells = Content(Build(sheet)).Where(c => c.Style == ReportCellStyle.Matrix).ToList();

        Assert.That(cells.Single(c => c.Text == "+0,5").Tint, Is.EqualTo(0.5f).Within(1e-4));
        Assert.That(cells.Single(c => c.Text == "-1,0").Tint, Is.EqualTo(-1f).Within(1e-4));
        Assert.That(cells.Single(c => c.Text == "+0.25").Tint, Is.EqualTo(0.25f).Within(1e-4));
        Assert.That(cells.Single(c => c.Text == "").Tint, Is.EqualTo(0f));
    }

    [Test]
    public void Layout_MatrixHeadersStandUpExceptTheFirst()
    {
        var header = Content(Build(Sheet(auto: "Matrix"))).Where(c => c.Style == ReportCellStyle.ColumnHeader).ToList();

        Assert.That(header.Select(c => c.Vertical), Is.EqualTo(new[] { false, true, true }));
        Assert.That(header[1].Height, Is.EqualTo("Coluna 1".Length * 10f + 2f * 12f).Within(1e-3), "a altura e o comprimento do texto deitado");
        Assert.That(Content(Build(Sheet())).Any(c => c.Vertical), Is.False);
    }

    [Test]
    public void Layout_KeepsTagsOfTheCells()
    {
        var sheet = Sheet();
        sheet.Rows[0].Cells[2] = T("Conferido", tag: "source");
        Assert.That(Content(Build(sheet)).Single(c => c.Text == "Conferido").Tag, Is.EqualTo("source"));
    }

    [Test]
    public void Layout_SkipsLinksWithoutLabel_AndPutsTheLabelAboveThem()
    {
        var labels = new ReportLabels { SeeAlso = "Ver", LinkLabel = id => id == "Ficha_A" ? "Ficha A" : null };
        var cells = Content(Build(Sheet(), labels));

        var links = cells.Where(c => c.Style == ReportCellStyle.Link).ToList();
        Assert.That(links.Select(c => c.LinkId), Is.EqualTo(new[] { "Ficha_A" }));
        var label = cells.Single(c => c.Text == "Ver");
        Assert.That(links[0].Y, Is.EqualTo(label.Y + label.Height).Within(1e-3));
    }

    [Test]
    public void Layout_LinksShareLinesSideBySide()
    {
        var sheet = Sheet();
        sheet.SeeAlso = new[] { "A", "B", "C", "D" };
        var options = new ReportLayoutOptions { PageWidth = 900f, LinksPerRow = 3 };
        var links = Content(Build(sheet, new ReportLabels { LinkLabel = id => id }, options)).Where(c => c.Style == ReportCellStyle.Link).ToList();

        Assert.That(links.Select(c => c.X), Is.EqualTo(new[] { 0f, 300f, 600f, 0f }));
        Assert.That(links[3].Y, Is.GreaterThan(links[0].Y));
        Assert.That(links.All(c => Math.Abs(c.Width - 300f) < 1e-3), Is.True);
    }

    [Test]
    public void Layout_NothingCrossesTheRightEdge()
    {
        var sheet = Sheet(columns: 6);
        sheet.Rows[0].Cells[3] = T(string.Join(" ", Enumerable.Repeat("palavra", 40)));
        var options = new ReportLayoutOptions { PageWidth = 700f, PageHeight = 5000f };
        var layout = Build(sheet, options: options);

        Assert.That(layout.Cells.All(c => c.X + c.Width <= 700f + 1e-3), Is.True);
        var table = layout.Cells.Where(c => c.Style == ReportCellStyle.ColumnHeader).ToList();
        Assert.That(table.Sum(c => c.Width), Is.EqualTo(700f).Within(1e-2), "a tabela ocupa a largura da folha");
    }

    [Test]
    public void Layout_ColumnWithMoreTextGetsMoreWidth()
    {
        var sheet = Sheet(columns: 3);
        sheet.Rows[0].Cells[1] = T(string.Join(" ", Enumerable.Repeat("texto", 30)));
        var header = Content(Build(sheet)).Where(c => c.Style == ReportCellStyle.ColumnHeader).ToList();

        Assert.That(header[1].Width, Is.GreaterThan(header[0].Width));
        Assert.That(header[1].Width, Is.GreaterThan(header[2].Width));
    }

    [Test]
    public void Layout_ShortSheetKeepsTheFullSizeLetter()
    {
        var layout = Build(Sheet());

        Assert.That(layout.Fits, Is.True);
        Assert.That(layout.Scale, Is.EqualTo(1f));
    }

    [Test]
    public void Layout_LongSheetShrinksUntilItFitsThePage()
    {
        var sheet = Sheet();
        sheet.Notes = Enumerable.Range(0, 12).Select(i => T(string.Join(" ", Enumerable.Repeat("observacao", 12)))).ToArray();
        var options = new ReportLayoutOptions { PageWidth = 900f, PageHeight = 1200f, MinScale = 0.3f };
        var full = Build(sheet, options: new ReportLayoutOptions { PageWidth = 900f, PageHeight = 100000f });
        var layout = Build(sheet, options: options);

        Assert.That(full.Height, Is.GreaterThan(1200f), "o caso precisa mesmo encolher");
        Assert.That(layout.Fits, Is.True);
        Assert.That(layout.Scale, Is.LessThan(1f).And.GreaterThanOrEqualTo(0.3f));
        Assert.That(layout.Height, Is.LessThanOrEqualTo(1200.5f));
        Assert.That(Build(sheet, options: new ReportLayoutOptions { PageWidth = 900f, PageHeight = 1200f, MinScale = layout.Scale + 0.05f, MaxScale = layout.Scale + 0.05f }).Fits, Is.False, "um pouco maior ja nao cabe");
    }

    [Test]
    public void Layout_ReportsWhenNotEvenTheSmallestLetterFits()
    {
        var sheet = Sheet();
        sheet.Notes = Enumerable.Range(0, 200).Select(i => T("nota longa demais para a folha")).ToArray();
        var layout = Build(sheet, options: new ReportLayoutOptions { PageHeight = 500f, MinScale = 0.5f });

        Assert.That(layout.Fits, Is.False);
        Assert.That(layout.Scale, Is.EqualTo(0.5f));
    }

    [Test]
    public void Layout_WordWiderThanItsColumnDoesNotFit()
    {
        var sheet = Sheet(columns: 2);
        sheet.Rows[0].Cells[0] = T(new string('x', 200));
        var layout = Build(sheet, options: new ReportLayoutOptions { PageWidth = 600f, MinScale = 0.9f });

        Assert.That(layout.Fits, Is.False);
    }

    [Test]
    public void Layout_RowsStackWithoutGaps()
    {
        var layout = Build(Sheet());
        var rows = layout.Cells.GroupBy(c => c.Row).OrderBy(g => g.Key).Select(g => g.First()).ToList();

        for (var i = 1; i < rows.Count; i++)
            Assert.That(rows[i].Y, Is.GreaterThanOrEqualTo(rows[i - 1].Y + rows[i - 1].Height - 1e-3));
        var table = layout.Cells.Where(c => c.Column == 0).OrderBy(c => c.Row).ToList();
        for (var i = 1; i < table.Count; i++)
            Assert.That(table[i].Y, Is.EqualTo(table[i - 1].Y + table[i - 1].Height).Within(1e-3), "a tabela nao tem buraco entre as linhas");
        Assert.That(layout.Height, Is.EqualTo(layout.Cells.Max(c => c.Y + c.Height)).Within(1e-3));
    }
    [TestCase("+0,4", 0.4)]
    [TestCase("-1.3", -1.3)]
    [TestCase(" 12 ", 12)]
    public void TryParseSigned_ReadsBothDecimalMarks(string text, double expected)
    {
        Assert.That(ReportSheetLayout.TryParseSigned(text, out var value), Is.True);
        Assert.That(value, Is.EqualTo(expected).Within(1e-9));
    }

    [Test]
    public void TryParseSigned_RejectsText()
    {
        Assert.That(ReportSheetLayout.TryParseSigned("Renda", out _), Is.False);
        Assert.That(ReportSheetLayout.TryParseSigned("", out _), Is.False);
    }

    [Test]
    public void Fingerprint_HasFixedFormat()
    {
        var numbers = new ModelReportNumbers
        {
            Initial = new[] { 55.0, 10.0 },
            Min = new[] { 0.0, 0.0 },
            Max = new[] { 100.0, 100.0 },
            Critical = new[] { 10.0, -1.0 },
            Deriva = new[] { 1.0, -1.1f },
            Interaction = new[] { 50.0, 1.6f },
            Matrix = new[] { 0.0, 0.4f, -1.3, -0.0 },
            RealMin = new[] { 0.0, -2000000.0 },
            RealMax = new[] { 1.2e9, 2000000.0 },
            HandSize = 6,
            VictoryTurns = 20,
            TierCounts = new[] { 20, 14 },
            Events = 50,
            ConditionalEvents = 24,
        };

        Assert.That(ModelReportFingerprint.Build(numbers), Is.EqualTo(
            "P:55.000/0.000/100.000/10.000/1.000;10.000/0.000/100.000/-1.000/-1.100" +
            "|Z:50.000/1.600|M:0.000,0.400,-1.300,0.000" +
            "|R:0.000/1200000000.000;-2000000.000/2000000.000|T:6/20|C:20/14|E:50/24"));
    }

    [Test]
    public void Validate_AcceptsACompleteFile()
    {
        Assert.That(ModelReportImportFormat.Validate(File(Sheet()), new HashSet<string> { "Ficha_A" }), Is.Empty);
    }

    [Test]
    public void Validate_RejectsEmptyFileAndMissingMetadata()
    {
        Assert.That(ModelReportImportFormat.Validate(null, null), Is.Not.Empty);
        Assert.That(ModelReportImportFormat.Validate(new ReportFile(), null), Is.Not.Empty);

        var file = File(Sheet());
        file.Fingerprint = "";
        file.Version = 9;
        var errors = ModelReportImportFormat.Validate(file, null);
        Assert.That(errors.Any(e => e.Contains("Fingerprint")), Is.True);
        Assert.That(errors.Any(e => e.Contains("Versao")), Is.True);
    }

    [Test]
    public void Validate_RejectsRepeatedOrBadIds()
    {
        Assert.That(ModelReportImportFormat.Validate(File(Sheet("A"), Sheet("A")), null).Any(e => e.Contains("repetido")), Is.True);
        Assert.That(ModelReportImportFormat.Validate(File(Sheet("Relações")), null).Any(e => e.Contains("Id so pode")), Is.True);
    }

    [Test]
    public void Validate_RequiresBothLanguages()
    {
        var sheet = Sheet();
        sheet.Notes[1].En = "";
        sheet.Title.Pt = " ";
        var errors = ModelReportImportFormat.Validate(File(sheet), null);

        Assert.That(errors.Any(e => e.Contains("Notes[1] sem ingles")), Is.True);
        Assert.That(errors.Any(e => e.Contains("Title sem portugues")), Is.True);
    }

    [Test]
    public void Validate_AllowsEmptyMatrixCellButNotHalfTranslatedOne()
    {
        var sheet = Sheet();
        sheet.Rows[0].Cells[1] = new ReportText { Pt = "", En = "" };
        Assert.That(ModelReportImportFormat.Validate(File(sheet), null), Is.Empty);

        sheet.Rows[0].Cells[1] = new ReportText { Pt = "+0,4", En = "" };
        Assert.That(ModelReportImportFormat.Validate(File(sheet), null).Any(e => e.Contains("so tem um idioma")), Is.True);
    }

    [Test]
    public void Validate_RejectsDashesUnknownTagsAndAutoTables()
    {
        var sheet = Sheet(auto: "Inventada");
        sheet.Intro.Pt = "Texto — com travessao";
        sheet.Rows[0].Cells[2] = T("x", tag: "roxo");
        var errors = ModelReportImportFormat.Validate(File(sheet), null);

        Assert.That(errors.Any(e => e.Contains("travessao")), Is.True);
        Assert.That(errors.Any(e => e.Contains("marca desconhecida")), Is.True);
        Assert.That(errors.Any(e => e.Contains("tabela automatica")), Is.True);
    }

    [Test]
    public void Validate_RejectsRowsWiderThanTheTable()
    {
        var sheet = Sheet(columns: 2);
        sheet.Rows[0].Cells = new[] { T("a"), T("b"), T("c") };
        Assert.That(ModelReportImportFormat.Validate(File(sheet), null).Any(e => e.Contains("3 celulas")), Is.True);
    }

    [Test]
    public void Validate_ChecksLinksAgainstKnownEntries_ButAlwaysAcceptsTheMap()
    {
        var errors = ModelReportImportFormat.Validate(File(Sheet()), new HashSet<string>());

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Ficha_A"));
    }
}
