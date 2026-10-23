using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class KnowledgeImportFormatTests
{
    private static readonly HashSet<string> Cards = new HashSet<string> { "CardVLT", "CardHorta" };
    private static readonly HashSet<string> Events = new HashSet<string> { "EventEnchente" };

    private static KnowledgeImportFormat.EntryDto Entry(string id, string category = "Concept", string trigger = "FirstCollapse")
    {
        return new KnowledgeImportFormat.EntryDto
        {
            Id = id,
            Category = category,
            Trigger = trigger,
            TitlePt = "Titulo",
            TitleEn = "Title",
            SummaryPt = "Resumo",
            SummaryEn = "Summary",
            Sources = new[] { new KnowledgeImportFormat.SourceDto { Label = "Fonte", Url = "https://example.org/a" } },
        };
    }

    private static List<string> Validate(params KnowledgeImportFormat.EntryDto[] entries)
    {
        return KnowledgeImportFormat.Validate(new KnowledgeImportFormat.FileDto { Entries = entries }, Cards, Events);
    }

    [Test]
    public void Validate_ValidFile_HasNoErrors()
    {
        var card = Entry("Ficha_VLT", "Card", "None");
        card.Cards = new[] { "CardVLT" };
        var concept = Entry("Conceito_Colapso");
        concept.Related = new[] { "Ficha_VLT" };

        Assert.That(Validate(card, concept), Is.Empty);
    }

    [Test]
    public void Validate_EmptyFile_IsAnError()
    {
        Assert.That(KnowledgeImportFormat.Validate(new KnowledgeImportFormat.FileDto(), Cards, Events), Is.Not.Empty);
        Assert.That(KnowledgeImportFormat.Validate(null, Cards, Events), Is.Not.Empty);
    }

    [Test]
    public void Validate_RepeatedOrInvalidId_IsAnError()
    {
        Assert.That(Validate(Entry("A"), Entry("A")).Any(e => e.Contains("repetido")), Is.True);
        Assert.That(Validate(Entry("Ficha ção")).Any(e => e.Contains("Id so pode")), Is.True);
    }

    [Test]
    public void Validate_UnknownCategoryOrTrigger_IsAnError()
    {
        Assert.That(Validate(Entry("A", category: "Nada")).Any(e => e.Contains("categoria")), Is.True);
        Assert.That(Validate(Entry("A", trigger: "Nunca")).Any(e => e.Contains("gatilho")), Is.True);
        Assert.That(Validate(Entry("A", trigger: "3")).Any(e => e.Contains("gatilho")), Is.True, "numero nao vale como nome de enum");
    }

    [Test]
    public void Validate_MissingTextInEitherLanguage_IsAnError()
    {
        var noEnglishTitle = Entry("A");
        noEnglishTitle.TitleEn = "";
        var halfSection = Entry("B");
        halfSection.RealWorldPt = "Curitiba";

        Assert.That(Validate(noEnglishTitle).Any(e => e.Contains("TitleEn")), Is.True);
        Assert.That(Validate(halfSection).Any(e => e.Contains("RealWorld")), Is.True);
    }

    [Test]
    public void Validate_TextWithDash_IsAnError()
    {
        var entry = Entry("A");
        entry.SummaryPt = "Algo — outra coisa";

        Assert.That(Validate(entry).Any(e => e.Contains("travessao")), Is.True);
    }

    [TestCase("ftp://example.org")]
    [TestCase("example.org")]
    [TestCase("")]
    public void Validate_NonWebSourceUrl_IsAnError(string url)
    {
        var entry = Entry("A");
        entry.Sources = new[] { new KnowledgeImportFormat.SourceDto { Label = "Fonte", Url = url } };

        Assert.That(Validate(entry).Any(e => e.Contains("link")), Is.True);
    }

    [Test]
    public void Validate_UnknownOrSelfRelated_IsAnError()
    {
        var unknown = Entry("A");
        unknown.Related = new[] { "Fantasma" };
        var self = Entry("B");
        self.Related = new[] { "B" };

        Assert.That(Validate(unknown).Any(e => e.Contains("desconhecida 'Fantasma'")), Is.True);
        Assert.That(Validate(self).Any(e => e.Contains("si mesma")), Is.True);
    }

    [Test]
    public void Validate_UnknownCardOrEvent_IsAnError()
    {
        var entry = Entry("A", "Card", "None");
        entry.Cards = new[] { "CardInexistente" };
        var eventEntry = Entry("B", "Event", "None");
        eventEntry.Events = new[] { "EventInexistente" };

        Assert.That(Validate(entry).Any(e => e.Contains("carta desconhecida")), Is.True);
        Assert.That(Validate(eventEntry).Any(e => e.Contains("evento desconhecida")), Is.True);
    }

    [Test]
    public void Validate_CardLinkedToTwoEntries_IsAnError()
    {
        var first = Entry("A", "Card", "None");
        first.Cards = new[] { "CardVLT" };
        var second = Entry("B", "Card", "None");
        second.Cards = new[] { "CardVLT" };

        Assert.That(Validate(first, second).Any(e => e.Contains("ja aponta")), Is.True);
    }

    [Test]
    public void Validate_EntryThatCanNeverUnlock_IsAnError()
    {
        Assert.That(Validate(Entry("A", "Concept", "None")).Any(e => e.Contains("nunca seria")), Is.True);
    }

    [Test]
    public void Validate_CardOrEventCategoryWithoutLink_IsAnError()
    {
        Assert.That(Validate(Entry("A", "Card", "GameStart")).Any(e => e.Contains("sem carta")), Is.True);
        Assert.That(Validate(Entry("B", "Event", "GameStart")).Any(e => e.Contains("sem evento")), Is.True);
    }

    [Test]
    public void Validate_ParameterEntry_NeedsAKnownParameter()
    {
        var valid = Entry("Parametro_Renda", "Parameter", "GameStart");
        valid.Parameter = "Renda";
        var unknown = Entry("Parametro_X", "Parameter", "GameStart");
        unknown.Parameter = "Felicidade";
        var missing = Entry("Parametro_Y", "Parameter", "GameStart");

        Assert.That(Validate(valid), Is.Empty);
        Assert.That(Validate(unknown).Any(e => e.Contains("parametro desconhecido")), Is.True);
        Assert.That(Validate(missing).Any(e => e.Contains("parametro desconhecido")), Is.True);
    }

    [Test]
    public void Validate_ParameterOnOtherCategory_IsAnError()
    {
        var concept = Entry("Conceito_A");
        concept.Parameter = "Renda";

        Assert.That(Validate(concept).Any(e => e.Contains("so ficha de parametro")), Is.True);
    }

    [Test]
    public void Validate_SharedEntryForCardAndEvent_IsValid()
    {
        var shared = Entry("Ficha_Enchente", "Event", "None");
        shared.Cards = new[] { "CardHorta" };
        shared.Events = new[] { "EventEnchente" };

        Assert.That(Validate(shared), Is.Empty);
    }
}
