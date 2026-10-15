using System;
using System.Collections.Generic;

// Origem de uma parcela da variacao de um parametro no turno. So acrescentar valores no fim.
public enum ContributionSource
{
    Card,
    Event,
    Interaction,
    Deriva,
    ExcessCorrection,
    CollapsePenalty,
    Clamp,
    CardCost,
}

// Uma parcela da variacao: quanto um parametro mudou por uma origem.
// From so tem sentido nas interacoes, e diz qual parametro exerceu a influencia.
public struct Contribution
{
    public CityParameterType Target;
    public ContributionSource Source;
    public CityParameterType From;
    public float Amount;
}

// Composicao do Bem-estar no fim da resolucao, ja que ele e recalculado do zero a cada turno.
public struct WellbeingComposition
{
    public float PositiveAverage;
    public float Crowding;
    public float AnchorBonus;
    public float CollapsePenalty;
    public int CollapsedCount;

    // Diferenca do limite da escala, quando a soma passou do minimo ou do maximo.
    public float Clamp;

    public float Total => PositiveAverage - Crowding + AnchorBonus - CollapsePenalty + Clamp;
}

// Registro do que mudou cada parametro num turno, so pra mostrar ao jogador.
// Nao interfere em nenhuma conta: os parametros ficam iguais com ou sem registro.
public class TurnBreakdown
{
    private static readonly CityParameterType[] Parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));

    private readonly Dictionary<CityParameterType, float> _startValues = new Dictionary<CityParameterType, float>();
    private readonly Dictionary<CityParameterType, float> _endValues = new Dictionary<CityParameterType, float>();
    private readonly List<Contribution> _contributions = new List<Contribution>();

    public TurnBreakdown(int turnIndex)
    {
        TurnIndex = turnIndex;
    }

    public int TurnIndex { get; }

    // Origem atribuida ao que for aplicado de fora (carta, evento) enquanto o registro esta ligado.
    public ContributionSource CurrentSource { get; set; } = ContributionSource.Card;

    public bool HasWellbeing { get; private set; }

    public WellbeingComposition Wellbeing { get; private set; }

    public bool IsComplete { get; private set; }

    public IReadOnlyList<Contribution> Contributions => _contributions;

    public void CaptureStart(CityStats stats)
    {
        foreach (var parameter in Parameters)
            _startValues[parameter] = stats.GetValue(parameter);
    }

    public void CaptureEnd(CityStats stats)
    {
        foreach (var parameter in Parameters)
            _endValues[parameter] = stats.GetValue(parameter);
        IsComplete = true;
    }

    public void Add(CityParameterType target, ContributionSource source, float amount, CityParameterType from = default)
    {
        if (amount == 0f)
            return;

        _contributions.Add(new Contribution { Target = target, Source = source, From = from, Amount = amount });
    }

    public void SetWellbeing(WellbeingComposition composition)
    {
        Wellbeing = composition;
        HasWellbeing = true;
    }

    public float GetStartValue(CityParameterType parameter)
    {
        return _startValues.TryGetValue(parameter, out var value) ? value : 0f;
    }

    public float GetEndValue(CityParameterType parameter)
    {
        return _endValues.TryGetValue(parameter, out var value) ? value : 0f;
    }

    public float GetDelta(CityParameterType parameter)
    {
        return GetEndValue(parameter) - GetStartValue(parameter);
    }

    public List<Contribution> For(CityParameterType target)
    {
        var result = new List<Contribution>();
        foreach (var contribution in _contributions)
        {
            if (contribution.Target == target)
                result.Add(contribution);
        }
        return result;
    }

    public float Sum(CityParameterType target)
    {
        var sum = 0f;
        foreach (var contribution in _contributions)
        {
            if (contribution.Target == target)
                sum += contribution.Amount;
        }
        return sum;
    }

    // Soma das parcelas de uma origem, como a carta ou o evento, num parametro.
    public float Sum(CityParameterType target, ContributionSource source)
    {
        var sum = 0f;
        foreach (var contribution in _contributions)
        {
            if (contribution.Target == target && contribution.Source == source)
                sum += contribution.Amount;
        }
        return sum;
    }
}
