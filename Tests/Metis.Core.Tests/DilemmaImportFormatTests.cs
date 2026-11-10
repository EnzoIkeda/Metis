using System;
using System.Collections.Generic;
using System.Linq;

namespace Metis.Core.Tests;

public class DilemmaImportFormatTests
{
    private static DilemmaImportFormat.EffectDto[] Change(string parameter, float amount)
    {
        return new[] { new DilemmaImportFormat.EffectDto { Parameter = parameter, Amount = amount } };
    }

    private static DilemmaImportFormat.ChoiceDto Choice(string parameter = "Renda", float amount = 1f, string followUp = "", int delay = 0)
    {
        return new DilemmaImportFormat.ChoiceDto
        {
            LabelPt = "Opção",
            LabelEn = "Option",
            DetailPt = "Detalhe",
            DetailEn = "Detail",
            Effects = Change(parameter, amount),
            FollowUp = followUp,
            Delay = delay,
        };
    }

    private static DilemmaImportFormat.DilemmaDto Dilemma(params DilemmaImportFormat.ChoiceDto[] choices)
    {
        return new DilemmaImportFormat.DilemmaDto
        {
            Id = "D",
            TitlePt = "Título",
            TitleEn = "Title",
            DescriptionPt = "Descrição",
            DescriptionEn = "Description",
            MinTurn = 2,
            MaxTurn = 15,
            Choices = choices,
        };
    }

    private static DilemmaImportFormat.FollowUpDto FollowUp(string id = "F")
    {
        return new DilemmaImportFormat.FollowUpDto
        {
            Id = id,
            TitlePt = "Título",
            TitleEn = "Title",
            DescriptionPt = "Descrição",
            DescriptionEn = "Description",
            Effects = Change("Saude", -3f),
        };
    }

    private static List<string> Errors(DilemmaImportFormat.DilemmaDto dilemma, params DilemmaImportFormat.FollowUpDto[] followUps)
    {
        return DilemmaImportFormat.Validate(new DilemmaImportFormat.FileDto { Dilemmas = new[] { dilemma }, FollowUps = followUps });
    }

    private static DilemmaImportFormat.DilemmaDto Valid()
    {
        return Dilemma(Choice("Renda", 5f, "F", 3), Choice("Saude", 2f));
    }

    [Test]
    public void ValidFile_HasNoErrors()
    {
        Assert.That(Errors(Valid(), FollowUp()), Is.Empty);
    }

    [TestCase(1)]
    [TestCase(4)]
    public void WrongNumberOfOptions_IsRejected(int count)
    {
        var options = Enumerable.Range(0, count).Select(i => Choice(new[] { "Renda", "Saude", "Energia", "Seguranca" }[i], 1f)).ToArray();

        Assert.That(Errors(Dilemma(options)), Has.Some.Contains("opcoes"));
    }

    [Test]
    public void UnknownFollowUp_IsRejected()
    {
        Assert.That(Errors(Dilemma(Choice("Renda", 1f, "Sumiu", 2), Choice("Saude", 1f))), Has.Some.Contains("consequencia desconhecida"));
    }

    [Test]
    public void FollowUpWithoutDelay_IsRejected()
    {
        Assert.That(Errors(Dilemma(Choice("Renda", 1f, "F", 0), Choice("Saude", 1f)), FollowUp()), Has.Some.Contains("atraso"));
    }

    [Test]
    public void DelayWithoutFollowUp_IsRejected()
    {
        Assert.That(Errors(Dilemma(Choice("Renda", 1f, "", 2), Choice("Saude", 1f))), Has.Some.Contains("atraso sem consequencia"));
    }

    [Test]
    public void FollowUpThatDoesNotFitInThePhase_IsRejected()
    {
        var dilemma = Valid();
        dilemma.MaxTurn = 18;

        Assert.That(Errors(dilemma, FollowUp()), Has.Some.Contains("passa dos 20"));
    }

    [Test]
    public void FollowUpEndingOnTheLastTurn_Fits()
    {
        var dilemma = Valid();
        dilemma.MaxTurn = 17;

        Assert.That(Errors(dilemma, FollowUp()), Is.Empty);
    }

    [Test]
    public void DominatedOption_IsRejected()
    {
        var errors = Errors(Dilemma(Choice("Renda", 1f), Choice("Renda", 3f)));

        Assert.That(errors, Has.Some.Contains("Choices[0]: dominada"));
        Assert.That(errors, Has.None.Contains("Choices[1]: dominada"));
    }

    [Test]
    public void OptionWithAFollowUp_NeverCountsAsDominating()
    {
        Assert.That(DilemmaImportFormat.IsDominated(Choice("Renda", 1f), Choice("Renda", 9f, "F", 2)), Is.False);
        Assert.That(DilemmaImportFormat.IsDominated(Choice("Renda", 1f, "F", 2), Choice("Renda", 9f)), Is.False);
    }

    [Test]
    public void EmptyOption_IsNotDominatedByOneWithACost()
    {
        var empty = Choice();
        empty.Effects = Array.Empty<DilemmaImportFormat.EffectDto>();

        Assert.That(DilemmaImportFormat.IsDominated(empty, Choice("Renda", -2f)), Is.False);
    }

    [Test]
    public void TradeOffBetweenParameters_IsNotDominated()
    {
        var first = Choice("Renda", 3f);
        var second = Choice("Saude", 3f);

        Assert.That(DilemmaImportFormat.IsDominated(first, second), Is.False);
        Assert.That(DilemmaImportFormat.IsDominated(second, first), Is.False);
    }

    [Test]
    public void EffectOnTheAnchor_IsRejected()
    {
        Assert.That(Errors(Dilemma(Choice("BemEstar", 3f), Choice("Saude", 2f))), Has.Some.Contains("calculado"));
    }

    [Test]
    public void UnknownParameterAndZeroEffect_AreRejected()
    {
        Assert.That(Errors(Dilemma(Choice("Felicidade", 3f), Choice("Saude", 2f))), Has.Some.Contains("parametro desconhecido"));
        Assert.That(Errors(Dilemma(Choice("Renda", 0f), Choice("Saude", 2f))), Has.Some.Contains("efeito zero"));
    }

    [Test]
    public void LongOrDashedOrMissingText_IsRejected()
    {
        var dilemma = Valid();
        dilemma.Choices[0].LabelPt = new string('a', DilemmaImportFormat.MaxLabelLength + 1);
        dilemma.Choices[1].DetailEn = "com — travessao";
        dilemma.TitleEn = " ";

        var errors = Errors(dilemma, FollowUp());

        Assert.That(errors, Has.Some.Contains("LabelPt"));
        Assert.That(errors, Has.Some.Contains("travessao"));
        Assert.That(errors, Has.Some.Contains("TitleEn: vazio"));
    }

    [Test]
    public void WindowOutOfOrder_IsRejected()
    {
        var dilemma = Valid();
        dilemma.MinTurn = 10;
        dilemma.MaxTurn = 5;

        Assert.That(Errors(dilemma, FollowUp()), Has.Some.Contains("janela"));
    }

    [Test]
    public void FollowUpWithoutEffect_IsRejected()
    {
        var followUp = FollowUp();
        followUp.Effects = Array.Empty<DilemmaImportFormat.EffectDto>();

        Assert.That(Errors(Valid(), followUp), Has.Some.Contains("sem efeito"));
    }

    [Test]
    public void RepeatedIds_AndClashWithCommonEvent_AreRejected()
    {
        var file = new DilemmaImportFormat.FileDto { Dilemmas = new[] { Valid() }, FollowUps = new[] { FollowUp(), FollowUp("D") } };

        var errors = DilemmaImportFormat.Validate(file, new HashSet<string> { "F" });

        Assert.That(errors, Has.Some.Contains("D: Id repetido"));
        Assert.That(errors, Has.Some.Contains("F: ja existe um evento comum"));
    }

    [Test]
    public void UnusedFollowUp_OnlyWarns()
    {
        var file = new DilemmaImportFormat.FileDto { Dilemmas = new[] { Valid() }, FollowUps = new[] { FollowUp(), FollowUp("Solta") } };

        Assert.That(DilemmaImportFormat.Validate(file), Is.Empty);
        Assert.That(DilemmaImportFormat.Warnings(file).Single(), Does.Contain("Solta"));
    }

    [Test]
    public void ThemeList_RejectsAFollowUp()
    {
        var file = new ThemedPhaseImportFormat.FileDto
        {
            Themes = new[]
            {
                new ThemedPhaseImportFormat.ThemeDto
                {
                    Id = "T", NamePt = "a", NameEn = "a", SummaryPt = "a", SummaryEn = "a", OpeningPt = "a", OpeningEn = "a", VictoryPt = "a", VictoryEn = "a", InspirationPt = "a", InspirationEn = "a",
                    AllowedEvents = new[] { "F" },
                },
            },
        };

        Assert.That(ThemedPhaseImportFormat.Validate(file, followUps: new HashSet<string> { "F" }), Has.Some.Contains("so acontece como consequencia"));
    }

    [Test]
    public void JsonEditor_ChangesOnlyTheTextOfTheChosenOption()
    {
        const string json = "{\n  \"Dilemmas\": [ { \"Id\": \"D\", \"TitlePt\": \"T\", \"Choices\": [ { \"LabelPt\": \"A\" }, { \"LabelPt\": \"B\" } ] } ]\n}";
        var changes = new[] { new ContentJsonEditor.Change { Key = "LabelPt", Expected = "B", Value = "Novo" } };

        Assert.That(ContentJsonEditor.TryReplaceInNested(json, "Dilemmas", "D", "Choices", 1, changes, out var result, out var error), Is.True, error);

        Assert.That(result, Is.EqualTo(json.Replace("\"B\"", "\"Novo\"")));
    }

    [Test]
    public void JsonEditor_NestedIndexOutOfRange_Fails()
    {
        const string json = "{ \"Dilemmas\": [ { \"Id\": \"D\", \"Choices\": [ { \"LabelPt\": \"A\" } ] } ] }";
        var changes = new[] { new ContentJsonEditor.Change { Key = "LabelPt", Expected = "A", Value = "Novo" } };

        Assert.That(ContentJsonEditor.TryReplaceInNested(json, "Dilemmas", "D", "Choices", 4, changes, out _, out var error), Is.False);
        Assert.That(error, Does.Contain("Choices[4]"));
    }
}
