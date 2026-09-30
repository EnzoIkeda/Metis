using Metis.Simulator;

namespace Metis.Simulator.Tests;

// Monta dados de balanceamento minimos e neutros, pra cada teste mudar so o que importa pro caso.
internal static class TestData
{
    public const float NeutralValue = 50f;

    public static List<CityParameterConfig> NeutralParameters(float criticalLevel = 10f)
    {
        return PhaseSimulator.Parameters.Select(parameter => new CityParameterConfig
        {
            Parameter = parameter,
            InitialValue = NeutralValue,
            MinValue = 0f,
            MaxValue = 100f,
            CriticalLevel = parameter == CityParameterType.Pesquisa ? -1f : criticalLevel,
            Deriva = 0f,
        }).ToList();
    }

    public static SimCard Card(string id, params (CityParameterType Parameter, float Amount)[] effects)
    {
        return new SimCard
        {
            Id = id,
            StatEffects = effects.Select(effect => new StatModifier { Parameter = effect.Parameter, Amount = effect.Amount }).ToList(),
        };
    }

    public static BalanceData Build(
        IReadOnlyList<SimCard> cards = null,
        IReadOnlyList<SimEvent> events = null,
        IReadOnlyList<CityParameterConfig> parameters = null,
        int victoryTurnCount = 20)
    {
        return new BalanceData
        {
            Rules = new SimRules { HandSize = 5, VictoryTurnCount = victoryTurnCount, RewardOptionCount = 3 },
            Parameters = parameters ?? NeutralParameters(),
            Interaction = new SimInteraction { EscalaGlobal = 0f },
            Cards = cards ?? new[] { Card("Neutra") },
            Events = events ?? Array.Empty<SimEvent>(),
        };
    }

    public static PhaseSetup Setup(BalanceData data, CardArchetype archetype = CardArchetype.Sustentabilidade)
    {
        return new PhaseSetup { Data = data, Archetype = archetype };
    }
}
