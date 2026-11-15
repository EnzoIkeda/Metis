using System.Collections.Generic;

// Regra especial de cada chefe. So acrescentar valores no fim: a Unity grava enum pelo numero.
public enum BossMechanic
{
    None,

    // Cada carta jogada pelo chefe aumenta o dano das seguintes.
    GrowingDamage,

    // O dano na Energia se espalha na hora para quem depende dela na matriz.
    CascadeSpread,

    // Cada carta jogada pelo chefe poe copias de uma carta inutil na pilha de compra do jogador.
    ExtraCards,

    // Cada carta jogada pelo chefe encarece as cartas do jogador ate o fim da fase.
    CostIncrease,
}

// Regras de uma carta do chefe, sem nada de apresentacao.
public interface IBossCardDefinition
{
    string Id { get; }

    // Impeto que o chefe gasta para jogar a carta.
    int MomentumCost { get; }

    // So dano: todo efeito tira do parametro.
    IReadOnlyList<StatModifier> Effects { get; }
}

// Regras de um chefe de fim de rodada, sem nada de apresentacao, para a mesma logica rodar no jogo e no simulador.
public interface IBossDefinition
{
    string Id { get; }

    // Baralho que o chefe enfrenta, o da fraqueza que ele ataca.
    CardArchetype TargetArchetype { get; }

    int StartingMomentum { get; }
    int MomentumPerTurn { get; }

    BossMechanic Mechanic { get; }

    // Intensidade da regra especial: aumento por carta no dano crescente, fracao espalhada na cascata,
    // copias por carta nas cartas extras e aumento de custo por carta no encarecimento.
    float MechanicAmount { get; }

    // Carta posta no baralho do jogador, so nas cartas extras; vazio nas outras regras.
    string MechanicCardId { get; }

    IReadOnlyList<IBossCardDefinition> Cards { get; }
}
