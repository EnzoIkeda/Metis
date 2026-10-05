using System.Runtime.CompilerServices;

namespace Metis.Simulator.Policies;

public sealed class MctsOptions
{
    // Simulacoes por decisao.
    public int Iterations { get; init; } = 1600;

    // Peso da exploracao no UCB, sobre recompensas em [0, 1].
    public double Exploration { get; init; } = 0.35;

    // Desinformado planeja so com a direcao de cada efeito, como quem le o texto da carta sem os numeros.
    public bool Informed { get; init; } = true;

    // Desinformado que joga a carta de revelacao sempre que ela esta jogavel, pra medir o valor dela ja descontado o custo.
    public bool RevealWhenUninformed { get; init; }
}

// MCTS por conjunto de informacao: cada simulacao sorteia mao e eventos futuros, e cada no conta quantas vezes cada carta estava disponivel.
public sealed class MctsPolicy : IPlayerPolicy
{
    private sealed class Node
    {
        public readonly Dictionary<string, Node> Children = new Dictionary<string, Node>();
        public int Visits;
        public int Availability;
        public double TotalReward;
    }

    // Peso da vitoria e das recompensas parciais: vencer sempre vale mais que perder.
    public const double VictoryBase = 0.8;
    public const double VictoryShaping = 0.2;
    public const double DefeatShaping = 0.3;

    private static readonly ConditionalWeakTable<BalanceData, Dictionary<string, SimCard>> PerceivedCache = new ConditionalWeakTable<BalanceData, Dictionary<string, SimCard>>();

    private readonly MctsOptions _options;
    private readonly BalancedPolicy _rollout = new BalancedPolicy();

    public MctsPolicy(MctsOptions options = null)
    {
        _options = options ?? new MctsOptions();
    }

    public string Name => _options.Informed ? "mcts" : _options.RevealWhenUninformed ? "mcts_desinformado_revela" : "mcts_desinformado";

    public MctsOptions Options => _options;

    public SimCard Choose(DecisionContext context)
    {
        // Revelacao fica fora da arvore: pra quem ja sabe os numeros nao vale nada, e pro desinformado e uma regra fixa.
        var knowsHand = _options.Informed || context.IsRevealed;
        var reveal = context.PlayableCards.FirstOrDefault(card => card.Ability == CardAbility.RevealHand);
        if (knowsHand == false && _options.RevealWhenUninformed && reveal != null)
            return reveal;

        var rootActions = PolicyHelpers.DistinctById(WithoutReveal(context.PlayableCards));
        if (rootActions.Count == 0)
            return context.PlayableCards[0];
        if (rootActions.Count == 1)
            return rootActions[0];

        var knowsDeck = _options.Informed || context.RevealedForPhase;
        var planningDeck = context.Deck.Select(card => Planned(card, context.Data, knowsDeck)).ToList();
        var plannedRoot = rootActions.Select(card => Planned(card, context.Data, knowsHand)).ToList();
        var root = new Node();

        for (int iteration = 0; iteration < _options.Iterations; iteration++)
            RunIteration(root, plannedRoot, planningDeck, context);

        var bestId = root.Children
            .OrderByDescending(entry => entry.Value.Visits)
            .ThenByDescending(entry => entry.Value.TotalReward)
            .First().Key;
        return rootActions.First(card => card.Id == bestId);
    }

    // Traz a carta que o proprio MCTS jogaria se pudesse escolher qualquer uma das candidatas.
    public SimCard ChooseSearch(DecisionContext context, IReadOnlyList<SimCard> candidates)
    {
        return Choose(new DecisionContext
        {
            Stats = context.Stats,
            PlayableCards = candidates,
            TurnIndex = context.TurnIndex,
            Data = context.Data,
            Random = context.Random,
            Deck = context.Deck,
            Events = context.Events,
            HandSize = context.HandSize,
            VictoryTurnCount = context.VictoryTurnCount,
            IsRevealed = true,
            RevealedForPhase = context.RevealedForPhase,
            FiniteDeck = context.FiniteDeck,
            DrawPile = context.DrawPile,
            DiscardPile = context.DiscardPile,
            HandCards = context.HandCards,
        });
    }

    // Com baralho finito, conta cartas: os futuros saem das cartas que de fato restam na pilha (so a ordem e sorteada),
    // e a mao atual vai pro descarte, que e onde ela termina no fim do turno.
    private CardHand<SimCard> PlanningHand(List<SimCard> planningDeck, DecisionContext context, Random random)
    {
        if (context.FiniteDeck == false)
            return new CardHand<SimCard>(planningDeck, random);

        SimCard Plan(SimCard card) => Planned(card, context.Data, _options.Informed || context.RevealedForPhase);
        var discard = context.DiscardPile.Concat(context.HandCards).Select(Plan);
        return CardHand<SimCard>.FromPiles(planningDeck, context.DrawPile.Select(Plan), discard, random);
    }

    private static List<SimCard> WithoutReveal(IReadOnlyList<SimCard> cards)
    {
        return cards.Where(card => card.Ability != CardAbility.RevealHand).ToList();
    }

    private void RunIteration(Node root, List<SimCard> rootActions, List<SimCard> planningDeck, DecisionContext context)
    {
        var random = new Random(context.Random.Next());
        var stats = context.Stats.Clone();
        var hand = PlanningHand(planningDeck, context, random);
        var events = new RandomEventPool<SimEvent>(context.Events, random);
        var path = new List<Node> { root };
        var node = root;
        var turn = context.TurnIndex;
        var actions = rootActions;
        var outcome = GameOutcome.None;

        // Selecao e expansao: desce pela arvore ate criar um no novo.
        var expanded = false;
        while (outcome == GameOutcome.None && expanded == false)
        {
            SimCard chosen = null;
            if (actions.Count > 0)
            {
                var distinct = PolicyHelpers.DistinctById(actions);
                foreach (var action in distinct)
                {
                    if (node.Children.TryGetValue(action.Id, out var known))
                        known.Availability++;
                }

                var untried = distinct.Where(action => node.Children.ContainsKey(action.Id) == false).ToList();
                if (untried.Count > 0)
                {
                    chosen = untried[random.Next(untried.Count)];
                    node.Children[chosen.Id] = new Node { Availability = 1 };
                    expanded = true;
                }
                else
                {
                    chosen = SelectUcb(node, distinct);
                }
                node = node.Children[chosen.Id];
                path.Add(node);
            }

            outcome = ForwardModel.StepTurnWithAbilities(stats, chosen, hand, turn, context.VictoryTurnCount, events, context.Data);
            if (outcome == GameOutcome.None)
            {
                turn++;
                actions = WithoutReveal(ForwardModel.DrawPlayable(hand, context.HandSize, stats));
            }
        }

        // Simulacao ate o fim da fase com a politica equilibrada, barata e sem simular dentro da simulacao.
        while (outcome == GameOutcome.None)
        {
            SimCard chosen = null;
            if (actions.Count > 0)
            {
                var plain = PolicyHelpers.PlainCards(actions);
                chosen = plain.Count == 0 ? null : _rollout.Choose(new DecisionContext
                {
                    Stats = stats,
                    PlayableCards = plain,
                    TurnIndex = turn,
                    Data = context.Data,
                    Random = random,
                });
            }

            outcome = ForwardModel.StepTurn(stats, chosen, turn, context.VictoryTurnCount, events);
            if (outcome == GameOutcome.None)
            {
                turn++;
                actions = ForwardModel.DrawPlayable(hand, context.HandSize, stats);
            }
        }

        var reward = Reward(outcome, stats, turn, context);
        foreach (var visited in path)
        {
            visited.Visits++;
            visited.TotalReward += reward;
        }
    }

    private SimCard SelectUcb(Node node, IReadOnlyList<SimCard> available)
    {
        SimCard best = null;
        var bestValue = double.NegativeInfinity;
        foreach (var card in available)
        {
            var child = node.Children[card.Id];
            var value = child.TotalReward / child.Visits
                + _options.Exploration * Math.Sqrt(Math.Log(Math.Max(child.Availability, 1)) / child.Visits);
            if (value > bestValue)
            {
                bestValue = value;
                best = card;
            }
        }
        return best;
    }

    // Vitoria entre 0,8 e 1 conforme a folga da cidade no fim; derrota entre 0 e 0,3 conforme o quanto durou.
    public static double Reward(GameOutcome outcome, CityStats stats, int turn, DecisionContext context)
    {
        if (outcome == GameOutcome.Victory)
        {
            var normalized = Math.Clamp(StateScore.Evaluate(stats, context.Data) / StateScore.MaxScore(context.Data), 0.0, 1.0);
            return VictoryBase + VictoryShaping * normalized;
        }
        return DefeatShaping * turn / context.VictoryTurnCount;
    }

    private static SimCard Planned(SimCard card, BalanceData data, bool informed)
    {
        if (informed)
            return card;
        return PerceivedCache.GetValue(data, BuildPerceived)[card.Id];
    }

    // Cada efeito vira so a direcao dele vezes a magnitude media de efeito das cartas: o jogador sabe o que sobe e o que desce, nao quanto.
    private static Dictionary<string, SimCard> BuildPerceived(BalanceData data)
    {
        var magnitudes = data.Cards.SelectMany(card => card.StatEffects).Select(effect => Math.Abs(effect.Amount)).Where(amount => amount > 0f).ToList();
        var average = magnitudes.Count > 0 ? magnitudes.Average() : 0f;
        return data.Cards.ToDictionary(card => card.Id, card => new SimCard
        {
            Id = card.Id,
            Name = card.Name,
            Cost = card.Cost,
            Tier = card.Tier,
            Archetype = card.Archetype,
            RequiredPesquisa = card.RequiredPesquisa,
            PlacesStructure = card.PlacesStructure,
            Ability = card.Ability,
            Copies = card.Copies,
            StatEffects = card.StatEffects
                .Select(effect => new StatModifier { Parameter = effect.Parameter, Amount = Math.Sign(effect.Amount) * average })
                .ToList(),
        });
    }
}
