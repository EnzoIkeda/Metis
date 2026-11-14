using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class TradeOfferImportFormatTests
{
    private static TradeOfferImportFormat.TermDto Effect(string parameter, float amount) => new TradeOfferImportFormat.TermDto
    {
        Kind = "PhaseStartEffect",
        Effects = new[] { new TradeOfferImportFormat.ModifierDto { Parameter = parameter, Amount = amount } },
    };

    private static TradeOfferImportFormat.TermDto Term(string kind, int amount = 0, string card = "") =>
        new TradeOfferImportFormat.TermDto { Kind = kind, Amount = amount, CardId = card };

    private static TradeOfferImportFormat.OfferDto Valid(string id = "Troca_A") => new TradeOfferImportFormat.OfferDto
    {
        Id = id,
        NamePt = "Empréstimo",
        NameEn = "Loan",
        SummaryPt = "Um banco empresta dinheiro.",
        SummaryEn = "A bank lends money.",
        Benefit = Effect("Renda", 15f),
        Drawback = Term("CardCostOffset", 1),
    };

    private static List<string> Check(params TradeOfferImportFormat.OfferDto[] offers) =>
        TradeOfferImportFormat.Validate(new TradeOfferImportFormat.FileDto { Offers = offers }, new HashSet<string> { "Ficha" }, new HashSet<string> { "CardLixo" });

    [Test]
    public void Validate_AcceptsAValidOffer()
    {
        Assert.That(Check(Valid()), Is.Empty);
    }

    [Test]
    public void Validate_RejectsBothSidesOfTheSameKind()
    {
        var offer = Valid();
        offer.Drawback = Effect("Saude", -4f);
        Assert.That(Check(offer).Any(error => error.Contains("mesmo tipo")), Is.True);
    }

    [Test]
    public void Validate_RejectsSidesGoingTheWrongWay()
    {
        var bonus = Valid();
        bonus.Benefit = Effect("Renda", -5f);
        Assert.That(Check(bonus).Any(error => error.Contains("sentido errado")), Is.True);

        var cheaper = Valid();
        cheaper.Drawback = Term("CardCostOffset", -1);
        Assert.That(Check(cheaper).Any(error => error.Contains("sentido errado")), Is.True);

        var moreRewards = Valid();
        moreRewards.Drawback = Term("RewardOptionOffset", 1);
        Assert.That(Check(moreRewards).Any(error => error.Contains("sentido errado")), Is.True);
    }

    [Test]
    public void Validate_ExtraCardOnlyAsDrawbackAndOnlyAnExtraOnlyCard()
    {
        var good = Valid();
        good.Drawback = Term("ExtraCard", card: "CardLixo");
        Assert.That(Check(good), Is.Empty);

        var asBenefit = Valid();
        asBenefit.Benefit = Term("ExtraCard", card: "CardLixo");
        Assert.That(Check(asBenefit).Any(error => error.Contains("so pode ser desvantagem")), Is.True);

        var normalCard = Valid();
        normalCard.Drawback = Term("ExtraCard", card: "CardComum");
        Assert.That(Check(normalCard).Any(error => error.Contains("fora dos baralhos")), Is.True);
    }

    [Test]
    public void Validate_RejectsRepeatedIdUnknownKindDashAndUnknownEntry()
    {
        var dash = Valid("Troca_B");
        dash.SummaryPt = "Um banco — empresta.";
        var kind = Valid("Troca_C");
        kind.Benefit = Term("Desconhecido");
        var entry = Valid("Troca_D");
        entry.KnowledgeEntry = "Inexistente";

        var errors = Check(Valid(), Valid(), dash, kind, entry);

        Assert.That(errors.Any(error => error.Contains("Id repetido")), Is.True);
        Assert.That(errors.Any(error => error.Contains("travessao")), Is.True);
        Assert.That(errors.Any(error => error.Contains("tipo desconhecido")), Is.True);
        Assert.That(errors.Any(error => error.Contains("ficha desconhecida")), Is.True);
    }
}
