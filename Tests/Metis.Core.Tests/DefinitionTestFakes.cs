using System.Collections.Generic;

namespace Metis.Core.Tests;

// Implementacoes puras das definicoes de jogo, no lugar dos ScriptableObject que so existem dentro do Unity.
internal sealed class FakeCard : ICardDefinition
{
    public string Id { get; init; } = "Card";
    public float Cost { get; init; }
    public CardTier Tier { get; init; } = CardTier.Basica;
    public CardArchetype Archetype { get; init; } = CardArchetype.Geral;
    public float RequiredPesquisa { get; init; }
    public IReadOnlyList<StatModifier> StatEffects { get; init; } = new StatModifier[0];
}

internal sealed class FakeEvent : IRandomEventDefinition
{
    public string Id { get; init; } = "Event";
    public IReadOnlyList<TriggerCondition> TriggerConditions { get; init; }
    public IReadOnlyList<StatModifier> StatEffects { get; init; } = new StatModifier[0];
    public int MinTurn { get; init; } = 1;
    public int MaxTurn { get; init; } = 999;
}
