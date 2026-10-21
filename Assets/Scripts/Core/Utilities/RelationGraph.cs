using System;
using System.Collections.Generic;

// Tipo de ligacao entre dois parametros no mapa de relacoes (so acrescentar).
public enum RelationKind
{
    // Termo da matriz, aplicado a cada turno.
    Interaction,

    // Parte da formula do Bem-estar, recalculado do zero todo turno.
    WellbeingPart,
}

public enum RelationStrength
{
    Fraca,
    Media,
    Forte,
}

// Uma ligacao do mapa, do parametro que influencia para o influenciado.
public struct RelationEdge
{
    public CityParameterType From;
    public CityParameterType To;
    public RelationKind Kind;

    // Na matriz, pontos por turno com a fonte em 100; no Bem-estar, peso na formula.
    public float Coefficient;

    // Fator da zona atual da fonte (1 sem estado de partida).
    public float ZoneFactor;

    // Falso no mapa estatico, sem cidade em jogo.
    public bool HasState;

    // Matriz: o quanto a fonte soma ao alvo por turno agora; Bem-estar: a parte atual dele.
    public float CurrentAmount;

    public bool IsSupport => Coefficient > 0f;

    // Forca efetiva, ja com o fator de zona.
    public float Strength => Math.Abs(Coefficient * ZoneFactor);
}

// Monta as ligacoes do mapa de relacoes a partir da matriz e, se houver, da cidade atual.
public static class RelationGraph
{
    // Partes positivas do Bem-estar, cada uma com peso de um quarto na media.
    public static readonly CityParameterType[] WellbeingAverageParts =
    {
        CityParameterType.Mobilidade,
        CityParameterType.Saude,
        CityParameterType.Seguranca,
        CityParameterType.Sustentabilidade,
    };

    // Adensamento: Populacao acima da metade da faixa desconta ponto a ponto.
    public const CityParameterType WellbeingCrowdingPart = CityParameterType.Populacao;

    // stats nulo gera o mapa estatico, so com os coeficientes.
    public static List<RelationEdge> Build(CityStats stats)
    {
        var matrix = stats?.Interactions;
        var hasState = matrix != null;
        var edges = new List<RelationEdge>();

        foreach (var alvo in InteractionMatrix.Order)
        {
            foreach (CityParameterType fonte in Enum.GetValues(typeof(CityParameterType)))
            {
                var coeficiente = InteractionMatrix.GetCoefficient(alvo, fonte);
                if (coeficiente == 0f)
                    continue;

                var valorFonte = hasState ? stats.GetValue(fonte) : 0f;
                edges.Add(new RelationEdge
                {
                    From = fonte,
                    To = alvo,
                    Kind = RelationKind.Interaction,
                    Coefficient = coeficiente,
                    ZoneFactor = hasState ? matrix.GetZoneFactor(valorFonte, coeficiente) : 1f,
                    HasState = hasState,
                    CurrentAmount = hasState ? matrix.GetTerm(alvo, fonte, valorFonte) : 0f,
                });
            }
        }

        var media = 0f;
        var adensamento = 0f;
        if (stats != null)
            stats.GetWellbeingParts(out media, out adensamento);

        var peso = 1f / WellbeingAverageParts.Length;
        foreach (var parte in WellbeingAverageParts)
        {
            edges.Add(new RelationEdge
            {
                From = parte,
                To = CityStats.AnchorParameter,
                Kind = RelationKind.WellbeingPart,
                Coefficient = peso,
                ZoneFactor = 1f,
                HasState = stats != null,
                CurrentAmount = stats != null ? stats.GetValue(parte) * peso : 0f,
            });
        }
        edges.Add(new RelationEdge
        {
            From = WellbeingCrowdingPart,
            To = CityStats.AnchorParameter,
            Kind = RelationKind.WellbeingPart,
            Coefficient = -1f,
            ZoneFactor = 1f,
            HasState = stats != null,
            CurrentAmount = -adensamento,
        });

        return edges;
    }

    public static RelationStrength GetStrength(float strength)
    {
        if (strength < 0.4f)
            return RelationStrength.Fraca;
        if (strength < 0.8f)
            return RelationStrength.Media;
        return RelationStrength.Forte;
    }

    // Ligacoes que chegam no parametro, da mais forte pra mais fraca.
    public static List<RelationEdge> Incoming(IEnumerable<RelationEdge> edges, CityParameterType parameter)
    {
        return Filter(edges, edge => edge.To == parameter);
    }

    // Ligacoes que saem do parametro, da mais forte pra mais fraca.
    public static List<RelationEdge> Outgoing(IEnumerable<RelationEdge> edges, CityParameterType parameter)
    {
        return Filter(edges, edge => edge.From == parameter);
    }

    // As duas direcoes entre um par, se existirem.
    public static List<RelationEdge> Between(IEnumerable<RelationEdge> edges, CityParameterType a, CityParameterType b)
    {
        return Filter(edges, edge => (edge.From == a && edge.To == b) || (edge.From == b && edge.To == a));
    }

    private static List<RelationEdge> Filter(IEnumerable<RelationEdge> edges, Func<RelationEdge, bool> keep)
    {
        var result = new List<RelationEdge>();
        foreach (var edge in edges)
        {
            if (keep(edge))
                result.Add(edge);
        }
        result.Sort((a, b) => b.Strength.CompareTo(a.Strength));
        return result;
    }
}
