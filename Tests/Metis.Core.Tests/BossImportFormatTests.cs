using System.Linq;

namespace Metis.Core.Tests;

// Validacao do arquivo de chefes.
public class BossImportFormatTests
{
    private static BossImportFormat.CardDto Card(string id, int cost = 4, string parameter = "Saude", float amount = -3f) => new BossImportFormat.CardDto
    {
        Id = id,
        NamePt = "Golpe",
        NameEn = "Blow",
        DescriptionPt = "Descricao.",
        DescriptionEn = "Description.",
        MomentumCost = cost,
        Effects = new[] { new BossImportFormat.ModifierDto { Parameter = parameter, Amount = amount } },
    };

    private static BossImportFormat.BossDto Boss(string id = "Chefe", string archetype = "Industria", int cards = 6) => new BossImportFormat.BossDto
    {
        Id = id,
        NamePt = "Chefe",
        NameEn = "Boss",
        SummaryPt = "Resumo.",
        SummaryEn = "Summary.",
        IntroPt = "Abertura.",
        IntroEn = "Opening.",
        VictoryPt = "Vitoria.",
        VictoryEn = "Victory.",
        TargetArchetype = archetype,
        StartingMomentum = 0,
        MomentumPerTurn = 2,
        Mechanic = "GrowingDamage",
        MechanicAmount = 0.1f,
        Cards = Enumerable.Range(0, cards).Select(i => Card($"{id}_C{i}")).ToArray(),
    };

    private static BossImportFormat.FileDto File(params BossImportFormat.BossDto[] bosses) => new BossImportFormat.FileDto { Bosses = bosses };

    [Test]
    public void Validate_WellFormedBoss_HasNoErrors()
    {
        Assert.That(BossImportFormat.Validate(File(Boss())), Is.Empty);
    }

    [Test]
    public void Validate_CardCountOutsideTheRange_Fails()
    {
        Assert.That(BossImportFormat.Validate(File(Boss(cards: 5))), Has.Some.Contains("cartas"));
        Assert.That(BossImportFormat.Validate(File(Boss(cards: 8))), Has.Some.Contains("cartas"));
    }

    [Test]
    public void Validate_CardThatHelps_Fails()
    {
        var boss = Boss();
        boss.Cards[0] = Card("Chefe_Bom", amount: 2f);

        Assert.That(BossImportFormat.Validate(File(boss)), Has.Some.Contains("nao e dano"));
    }

    [Test]
    public void Validate_DamageToWellbeingOrFreeCard_Fails()
    {
        var boss = Boss();
        boss.Cards[0] = Card("Chefe_BemEstar", parameter: "BemEstar");
        boss.Cards[1] = Card("Chefe_Gratis", cost: 0);

        var errors = BossImportFormat.Validate(File(boss));
        Assert.That(errors, Has.Some.Contains("Bem-estar"));
        Assert.That(errors, Has.Some.Contains("custo de Impeto"));
    }

    [Test]
    public void Validate_TwoBossesForTheSameDeck_Fails()
    {
        Assert.That(BossImportFormat.Validate(File(Boss("A"), Boss("B"))), Has.Some.Contains("ja tem chefe"));
    }

    [Test]
    public void Validate_UnknownMechanicOrDeck_Fails()
    {
        var boss = Boss(archetype: "Geral");
        boss.Mechanic = "Furacao";

        var errors = BossImportFormat.Validate(File(boss));
        Assert.That(errors, Has.Some.Contains("baralho desconhecido"));
        Assert.That(errors, Has.Some.Contains("mecanica desconhecida"));
    }

    [Test]
    public void Validate_RepeatedCardIdAndUnknownEntry_Fail()
    {
        var boss = Boss();
        boss.Cards[1] = Card(boss.Cards[0].Id);
        boss.KnowledgeEntry = "Sumiu";

        var errors = BossImportFormat.Validate(File(boss), new[] { "Outra" });
        Assert.That(errors, Has.Some.Contains("Id repetido"));
        Assert.That(errors, Has.Some.Contains("ficha desconhecida"));
    }

    [Test]
    public void Validate_EmDashInText_Fails()
    {
        var boss = Boss();
        boss.IntroPt = "Chuva \u2014 muita chuva.";

        Assert.That(BossImportFormat.Validate(File(boss)), Has.Some.Contains("travessao"));
    }

    [Test]
    public void Validate_Cascade_NeedsEnergyDamageInEveryCard()
    {
        var boss = Boss(archetype: "Sustentabilidade");
        boss.Mechanic = "CascadeSpread";
        boss.MechanicAmount = 0.5f;
        boss.Cards = boss.Cards.Select(card => Card(card.Id, parameter: "Energia")).ToArray();
        Assert.That(BossImportFormat.Validate(File(boss)), Is.Empty);

        boss.Cards[2] = Card(boss.Cards[2].Id, parameter: "Saude");
        boss.MechanicAmount = 1.5f;
        var errors = BossImportFormat.Validate(File(boss));
        Assert.That(errors, Has.Some.Contains("toda carta tira Energia"));
        Assert.That(errors, Has.Some.Contains("fracao entre 0 e 1"));
    }

    [Test]
    public void Validate_ExtraCards_NeedsAWholeCountAndAKnownExtraCard()
    {
        var boss = Boss(archetype: "Automacao");
        boss.Mechanic = "ExtraCards";
        boss.MechanicAmount = 2f;
        boss.MechanicCardId = "CardTermosDeUso";
        Assert.That(BossImportFormat.Validate(File(boss), null, new[] { "CardTermosDeUso" }), Is.Empty);

        Assert.That(BossImportFormat.Validate(File(boss), null, new[] { "Outra" }), Has.Some.Contains("nao existe"));
        boss.MechanicAmount = 1.5f;
        boss.MechanicCardId = "";
        var errors = BossImportFormat.Validate(File(boss));
        Assert.That(errors, Has.Some.Contains("numero inteiro"));
        Assert.That(errors, Has.Some.Contains("sem MechanicCardId"));
    }

    [Test]
    public void Validate_CostIncrease_NeedsAnAmountUpToOne()
    {
        var boss = Boss(archetype: "Comunitario");
        boss.Mechanic = "CostIncrease";
        boss.MechanicAmount = 0.5f;
        Assert.That(BossImportFormat.Validate(File(boss)), Is.Empty);

        boss.MechanicAmount = 2f;
        Assert.That(BossImportFormat.Validate(File(boss)), Has.Some.Contains("entre 0 e 1"));
    }

    [Test]
    public void Validate_MechanicCardOutsideExtraCards_Fails()
    {
        var boss = Boss();
        boss.MechanicCardId = "CardTermosDeUso";

        Assert.That(BossImportFormat.Validate(File(boss)), Has.Some.Contains("so vale nas cartas extras"));
    }
}
