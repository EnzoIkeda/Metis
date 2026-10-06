using System.Collections.Generic;

// Regras de jogo de uma vantagem passiva, sem nada de apresentacao.
public interface IAdvantageDefinition
{
    string Id { get; }
    IReadOnlyList<StatModifier> StatEffects { get; }
}
