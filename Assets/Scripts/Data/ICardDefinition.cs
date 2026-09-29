using System.Collections.Generic;

// 4 tiers, alinhados aos limiares de Pesquisa e aos tiers da planilha de balanceamento.
public enum CardTier
{
    Basica,
    CidadeDigital,
    CidadeConectada,
    SmartCity
}

// Baralho tematico da carta na planilha. Geral cobre tanto as cartas sem baralho proprio quanto o tier 0 compartilhado.
public enum CardArchetype
{
    Geral,
    Sustentabilidade,
    Industria,
    Automacao
}

// Regras de jogo de uma carta, sem nada de apresentacao, pra mesma logica rodar no jogo e no simulador.
public interface ICardDefinition
{
    // Identificador estavel, usado pra carregar recompensas entre fases.
    string Id { get; }
    float Cost { get; }
    CardTier Tier { get; }
    CardArchetype Archetype { get; }
    float RequiredPesquisa { get; }
    IReadOnlyList<StatModifier> StatEffects { get; }
}
