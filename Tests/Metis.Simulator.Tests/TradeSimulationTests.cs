using Metis.Simulator;
using Metis.Simulator.Policies;

namespace Metis.Simulator.Tests;

// Trocas do mapa no simulador: a fase aplica a mesma regra do jogo e a rodada oferece uma por parada.
public class TradeSimulationTests
{
    private sealed class FixedTradePolicy : ITradePolicy
    {
        private readonly bool _accept;

        public FixedTradePolicy(bool accept)
        {
            _accept = accept;
        }

        public string Name => _accept ? "sempre" : "nunca";

        public bool Accept(TradeContext context) => _accept;
    }

    private static TradeTerm Effect(CityParameterType parameter, float amount) =>
        new TradeTerm { Kind = TradeTermKind.PhaseStartEffect, Effects = new[] { new StatModifier { Parameter = parameter, Amount = amount } } };

    private static BalanceData Data()
    {
        var obra = new SimCard { Id = "Obra", Cost = 4f, StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Saude, Amount = 1f } } };
        var lixo = new SimCard { Id = "Lixo", IsExtraOnly = true };
        return new BalanceData
        {
            Rules = new SimRules { HandSize = 5, VictoryTurnCount = 3, RewardOptionCount = 3 },
            Parameters = TestData.NeutralParameters(),
            Interaction = new SimInteraction { EscalaGlobal = 0f },
            Cards = new[] { obra, lixo },
            Advantages = new[]
            {
                new SimAdvantage { Id = "Va", StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Saude, Amount = 2f } } },
                new SimAdvantage { Id = "Vb", StatEffects = new[] { new StatModifier { Parameter = CityParameterType.Seguranca, Amount = 2f } } },
            },
            TradeOffers = new[]
            {
                new SimTradeOffer { Id = "Emprestimo", Benefit = Effect(CityParameterType.Renda, 15f), Drawback = new TradeTerm { Kind = TradeTermKind.CardCostOffset, Amount = 1 } },
                new SimTradeOffer { Id = "BigTech", Benefit = Effect(CityParameterType.Pesquisa, 6f), Drawback = new TradeTerm { Kind = TradeTermKind.ExtraCard, CardId = "Lixo" } },
            },
        };
    }

    [Test]
    public void Phase_AppliesStartEffectsExtraCardAndCostOffset()
    {
        var data = Data();
        var history = new PhaseHistory();
        var setup = new PhaseSetup { Data = data, Archetype = CardArchetype.Industria, FiniteDeck = true, AcceptedTradeIds = new[] { "Emprestimo", "BigTech" } };

        var game = PhaseSimulator.Run(setup, new GreedyPolicy(), new GameSeeds(1, 0), history);

        Assert.That(PhaseHistory.Read(history.StartValues, CityParameterType.Renda), Is.EqualTo(65f).Within(0.001f));
        Assert.That(PhaseHistory.Read(history.StartValues, CityParameterType.Pesquisa), Is.EqualTo(56f).Within(0.001f));
        Assert.That(game.Turns.SelectMany(turn => turn.PlayableIds), Has.Member("Lixo"), "a carta da troca entra no baralho");

        var deck = PhaseSimulator.WithCostOffset(DeckBuilder.Build(data.Cards, CardArchetype.Industria, Array.Empty<string>()), 1);
        Assert.That(deck.Single(card => card.Id == "Obra").Cost, Is.EqualTo(5f));
    }

    [Test]
    public void Run_AllDeclined_MatchesARunWithoutTrades()
    {
        var setup = new RunSetup { Data = Data(), Archetype = CardArchetype.Industria, PhaseCount = 3, FiniteDeck = true };

        for (var index = 0; index < 20; index++)
        {
            var without = RunSimulator.Run(setup, new GreedyPolicy(), new RandomRewardPolicy(), 7, index);
            var declined = RunSimulator.Run(setup, new GreedyPolicy(), new RandomRewardPolicy(), 7, index, new FixedTradePolicy(false));

            Assert.That(declined.PhasesWon, Is.EqualTo(without.PhasesWon), $"rodada {index}");
            Assert.That(declined.Phases.Select(phase => phase.ChosenId), Is.EqualTo(without.Phases.Select(phase => phase.ChosenId)), $"rodada {index}");
            Assert.That(declined.Phases.Select(phase => phase.Game.TurnsPlayed), Is.EqualTo(without.Phases.Select(phase => phase.Game.TurnsPlayed)), $"rodada {index}");
            Assert.That(declined.Phases.Take(declined.Phases.Count - 1).All(phase => phase.TradeOfferId != "" && phase.TradeAccepted == false), Is.True, $"rodada {index}");
        }
    }

    [Test]
    public void Run_OffersEachTradeOnceAndNotAfterTheLastPhase()
    {
        var setup = new RunSetup { Data = Data(), Archetype = CardArchetype.Industria, PhaseCount = 3, FiniteDeck = true };

        var run = RunSimulator.Run(setup, new GreedyPolicy(), new RandomRewardPolicy(), 3, 0, new FixedTradePolicy(true));

        Assert.That(run.PhasesWon, Is.EqualTo(3));
        var offered = run.Phases.Select(phase => phase.TradeOfferId).ToList();
        Assert.That(offered[2], Is.Empty, "depois da ultima fase nao ha parada no mapa");
        Assert.That(offered.Take(2), Is.EquivalentTo(new[] { "Emprestimo", "BigTech" }));
        Assert.That(run.Phases.Take(2).All(phase => phase.TradeAccepted), Is.True);
    }

    [Test]
    public void Run_RewardOptionOffset_ChangesHowManyOptionsAreOffered()
    {
        var data = Data();
        var consorcio = new SimTradeOffer
        {
            Id = "Consorcio",
            Benefit = new TradeTerm { Kind = TradeTermKind.CardCostOffset, Amount = -1 },
            Drawback = new TradeTerm { Kind = TradeTermKind.RewardOptionOffset, Amount = -1 },
        };
        var only = new BalanceData
        {
            Rules = data.Rules,
            Parameters = data.Parameters,
            Interaction = data.Interaction,
            Cards = data.Cards,
            Advantages = data.Advantages,
            TradeOffers = new[] { consorcio },
        };
        var setup = new RunSetup { Data = only, Archetype = CardArchetype.Industria, PhaseCount = 3, FiniteDeck = true };

        var run = RunSimulator.Run(setup, new GreedyPolicy(), new RandomRewardPolicy(), 3, 0, new FixedTradePolicy(true));

        Assert.That(run.Phases[0].OfferedIds, Has.Count.EqualTo(2), "a troca ainda nao foi aceita na primeira recompensa");
        Assert.That(run.Phases[1].OfferedIds, Has.Count.EqualTo(1), "com a troca, uma opcao a menos");
    }

    [Test]
    public void Parse_ReadsTradeOffersAndRejectsUnknownExtraCard()
    {
        const string json = @"{""Cards"":[{""Id"":""Lixo"",""Tier"":""Basica"",""Archetype"":""Geral"",""ExtraOnly"":true}],
            ""TradeOffers"":[{""Id"":""T"",""Benefit"":{""Kind"":""PhaseStartEffect"",""Effects"":[{""Parameter"":""Renda"",""Amount"":5}]},
            ""Drawback"":{""Kind"":""ExtraCard"",""CardId"":""Lixo""}}]}";

        var data = BalanceData.Parse(json);
        Assert.That(data.Cards.Single().IsExtraOnly, Is.True);
        Assert.That(data.TradeOffers.Single().Drawback.Kind, Is.EqualTo(TradeTermKind.ExtraCard));

        Assert.Throws<InvalidDataException>(() => BalanceData.Parse(json.Replace(@"""CardId"":""Lixo""", @"""CardId"":""Sumiu""")));
    }
}
