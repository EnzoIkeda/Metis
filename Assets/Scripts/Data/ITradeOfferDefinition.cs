using System;
using System.Collections.Generic;

// Tipo de cada lado de uma troca do mapa. So acrescentar valores no fim: a Unity grava enum pelo numero.
public enum TradeTermKind
{
    // Efeito em parametros no inicio de cada fase, como uma vantagem.
    PhaseStartEffect,

    // Soma ao custo de todas as cartas.
    CardCostOffset,

    // Soma ao numero de opcoes de recompensa no fim de cada fase.
    RewardOptionOffset,

    // Acrescenta uma carta ao baralho de cada fase.
    ExtraCard,
}

// Um lado da troca: o que o jogador ganha ou o que ele perde.
[Serializable]
public struct TradeTerm
{
    public TradeTermKind Kind;
    public StatModifier[] Effects;
    public int Amount;
    public string CardId;
}

// Regras de uma oferta de troca, sem nada de apresentacao, pra mesma logica rodar no jogo e no simulador.
public interface ITradeOfferDefinition
{
    string Id { get; }
    TradeTerm Benefit { get; }
    TradeTerm Drawback { get; }
}

public static class TradeTermExtensions
{
    public static IReadOnlyList<StatModifier> EffectsOrEmpty(this TradeTerm term)
    {
        return term.Effects ?? Array.Empty<StatModifier>();
    }
}
