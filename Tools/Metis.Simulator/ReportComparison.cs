using System.Globalization;
using System.Text;

namespace Metis.Simulator;

// Compara os dois criterios de "decisao de maior impacto" do relatorio de fim de fase nas mesmas partidas simuladas.
public static class ReportComparison
{
    private const int TopCount = 3;
    private const int ExampleGames = 4;

    public static void Run(SimulationOptions options, TextWriter output)
    {
        var data = BalanceData.Load(options.DataPath);
        var cardsById = data.Cards.GroupBy(card => card.Id).ToDictionary(group => group.Key, group => (ICardDefinition)group.First());
        ICardDefinition FindCard(string id) => cardsById.TryGetValue(id, out var card) ? card : null;

        Directory.CreateDirectory(options.OutputDirectory);
        var summary = new StringBuilder();
        var examples = new StringBuilder();
        summary.AppendLine("setup,arquetipo,politica,partidas,derrotas,top1_igual,top3_sobreposicao,spearman,sinal_igual,partidas_com_alternativa,ganho_medio_alternativa");

        foreach (var job in SimulationRunner.BuildJobs(data, options))
        {
            var template = new CityStats(data.BuildParameterConfigs(job.Setup.Preset), data.BuildInteractionMatrix(), data.Interaction.PenalidadeColapso);
            var evaluator = new TurnEvaluator(template, data.ScoreRules, FindCard);

            int games = 0, defeats = 0, top1Same = 0, signPairs = 0, signSame = 0, withMiss = 0;
            double overlapSum = 0.0, spearmanSum = 0.0, missGainSum = 0.0;
            int spearmanGames = 0, examplesWritten = 0;

            for (var gameIndex = 0; gameIndex < options.GamesPerJob; gameIndex++)
            {
                var history = new PhaseHistory();
                var game = PhaseSimulator.Run(job.Setup, job.Policy, new GameSeeds(options.BaseSeed, gameIndex), history);
                games++;
                if (game.Outcome == GameOutcome.GameOver)
                    defeats++;

                var versus = PhaseReport.Impacts(history, ImpactCriterion.VersusPass, evaluator);
                var direct = PhaseReport.Impacts(history, ImpactCriterion.DirectEffect, evaluator);
                var topVersus = PhaseReport.TopDecisions(history, TopCount, ImpactCriterion.VersusPass, evaluator);
                var topDirect = PhaseReport.TopDecisions(history, TopCount, ImpactCriterion.DirectEffect, evaluator);

                if (topVersus.Count > 0 && topDirect.Count > 0 && topVersus[0].TurnIndex == topDirect[0].TurnIndex)
                    top1Same++;
                var shown = Math.Min(TopCount, Math.Min(topVersus.Count, topDirect.Count));
                if (shown > 0)
                    overlapSum += topVersus.Select(item => item.TurnIndex).Intersect(topDirect.Select(item => item.TurnIndex)).Count() / (double)shown;

                var directByTurn = direct.ToDictionary(item => item.TurnIndex, item => item.Impact);
                var pairs = versus.Where(item => directByTurn.ContainsKey(item.TurnIndex))
                    .Select(item => (Versus: item.Impact, Direct: directByTurn[item.TurnIndex])).ToList();
                foreach (var pair in pairs)
                {
                    if (Math.Abs(pair.Versus) < 1e-9 || Math.Abs(pair.Direct) < 1e-9)
                        continue;
                    signPairs++;
                    if (Math.Sign(pair.Versus) == Math.Sign(pair.Direct))
                        signSame++;
                }
                if (pairs.Count >= 3)
                {
                    spearmanSum += Spearman(pairs.Select(pair => pair.Versus).ToList(), pairs.Select(pair => pair.Direct).ToList());
                    spearmanGames++;
                }

                var miss = PhaseReport.BiggestMiss(history, evaluator);
                if (miss.HasValue)
                {
                    withMiss++;
                    missGainSum += miss.Value.Gain;
                }

                if (examplesWritten < ExampleGames && job.Setup.Preset == null)
                {
                    examplesWritten++;
                    WriteExample(examples, job, gameIndex, game, history, evaluator, topVersus, topDirect, miss);
                }
            }

            summary.AppendLine(string.Join(",",
                job.SetupName,
                job.Setup.Archetype,
                job.Policy.Name,
                games,
                defeats,
                Format(top1Same / (double)games),
                Format(overlapSum / games),
                Format(spearmanGames > 0 ? spearmanSum / spearmanGames : double.NaN),
                Format(signPairs > 0 ? signSame / (double)signPairs : double.NaN),
                Format(withMiss / (double)games),
                Format(withMiss > 0 ? missGainSum / withMiss : 0.0)));
        }

        File.WriteAllText(Path.Combine(options.OutputDirectory, "relatorio_criterios.csv"), summary.ToString());
        File.WriteAllText(Path.Combine(options.OutputDirectory, "relatorio_exemplos.txt"), examples.ToString());
        output.WriteLine(summary.ToString());
        output.WriteLine($"Saida em '{options.OutputDirectory}'.");
    }

    private static void WriteExample(StringBuilder text, SimulationJob job, int gameIndex, GameRecord game, PhaseHistory history,
        TurnEvaluator evaluator, List<DecisionImpact> topVersus, List<DecisionImpact> topDirect, MissedOpportunity? miss)
    {
        text.AppendLine($"== {job.SetupName} / {job.Setup.Archetype} / {job.Policy.Name} / partida {gameIndex}: {game.Outcome} em {game.TurnsPlayed} turnos");
        text.AppendLine("turno  carta                          contra_passar  efeito_direto  bem_estar_fim  colapsos_na_resolucao");
        foreach (var turn in history.Turns)
        {
            var versus = PhaseReport.Impact(turn, ImpactCriterion.VersusPass, evaluator);
            var direct = PhaseReport.Impact(turn, ImpactCriterion.DirectEffect, evaluator);
            var card = turn.PlayedCard ? turn.CardId : "(passou)";
            text.AppendLine($"{turn.TurnIndex,5}  {card,-30} {Format(versus),13}  {Format(direct),13}  {Format(turn.GetEnd(CityParameterType.BemEstar)),13}  {string.Join("+", turn.CollapsedAtResolution)}");
        }
        text.AppendLine("top contra_passar: " + string.Join(", ", topVersus.Select(item => $"T{item.TurnIndex} {item.CardId} ({Format(item.Impact)})")));
        text.AppendLine("top efeito_direto: " + string.Join(", ", topDirect.Select(item => $"T{item.TurnIndex} {item.CardId} ({Format(item.Impact)})")));
        var chain = PhaseReport.CollapseChain(history, evaluator.Rules);
        text.AppendLine("colapsos: " + (chain.Count == 0 ? "nenhum" : string.Join(", ", chain.Select(span => $"{span.Parameter} desde T{span.FirstTurn}, {span.TurnsPenalized} turnos, -{Format(span.WellbeingPenalty)} no Bem-estar"))));
        text.AppendLine("alternativa: " + (miss.HasValue ? $"T{miss.Value.TurnIndex} {miss.Value.BetterCardId} em vez de {miss.Value.PlayedCardId} (+{Format(miss.Value.Gain)})" : "nenhuma"));
        text.AppendLine();
    }

    // Correlacao de postos, com empate pela media dos postos.
    private static double Spearman(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        var rankA = Ranks(a);
        var rankB = Ranks(b);
        var meanA = rankA.Average();
        var meanB = rankB.Average();
        double covariance = 0.0, varianceA = 0.0, varianceB = 0.0;
        for (var i = 0; i < rankA.Length; i++)
        {
            covariance += (rankA[i] - meanA) * (rankB[i] - meanB);
            varianceA += (rankA[i] - meanA) * (rankA[i] - meanA);
            varianceB += (rankB[i] - meanB) * (rankB[i] - meanB);
        }
        return varianceA == 0.0 || varianceB == 0.0 ? 0.0 : covariance / Math.Sqrt(varianceA * varianceB);
    }

    private static double[] Ranks(IReadOnlyList<double> values)
    {
        var order = Enumerable.Range(0, values.Count).OrderBy(index => values[index]).ToArray();
        var ranks = new double[values.Count];
        for (var start = 0; start < order.Length;)
        {
            var end = start;
            while (end + 1 < order.Length && values[order[end + 1]] == values[order[start]])
                end++;
            var rank = (start + end) / 2.0 + 1.0;
            for (var k = start; k <= end; k++)
                ranks[order[k]] = rank;
            start = end + 1;
        }
        return ranks;
    }

    private static string Format(double value)
    {
        return double.IsNaN(value) ? "-" : value.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
