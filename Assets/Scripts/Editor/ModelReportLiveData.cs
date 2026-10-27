using System;
using System.Linq;
using UnityEditor;

// Numeros atuais do modelo, lidos da cena da fase, das faixas de unidade e da matriz, no formato da impressao do relatorio.
public static class ModelReportLiveData
{
    public const string DisplayPath = "Assets/Data/UI/ParameterDisplay.asset";

    public static string Fingerprint()
    {
        return ModelReportFingerprint.Build(Numbers());
    }

    public static ModelReportNumbers Numbers()
    {
        var data = BalanceDataExporter.Collect();
        var parameters = (CityParameterType[])Enum.GetValues(typeof(CityParameterType));
        var numbers = new ModelReportNumbers
        {
            Initial = new double[parameters.Length],
            Min = new double[parameters.Length],
            Max = new double[parameters.Length],
            Critical = new double[parameters.Length],
            Deriva = new double[parameters.Length],
            RealMin = new double[parameters.Length],
            RealMax = new double[parameters.Length],
            Matrix = new double[parameters.Length * parameters.Length],
            HandSize = data.Rules.HandSize,
            VictoryTurns = data.Rules.VictoryTurnCount,
            Events = data.Events.Count,
            ConditionalEvents = data.Events.Count(e => e.Conditions.Count > 0),
        };

        foreach (var dto in data.Parameters)
        {
            var i = (int)Enum.Parse(typeof(CityParameterType), dto.Parameter);
            numbers.Initial[i] = dto.InitialValue;
            numbers.Min[i] = dto.MinValue;
            numbers.Max[i] = dto.MaxValue;
            numbers.Critical[i] = dto.CriticalLevel;
            numbers.Deriva[i] = dto.Deriva;
        }

        var interaction = data.Interaction;
        numbers.Interaction = new double[]
        {
            interaction.ValorNeutro, interaction.EscalaGlobal, interaction.ZonaCrise, interaction.ZonaExcesso,
            interaction.MultiplicadorPressaoCrise, interaction.MultiplicadorApoioCrise, interaction.MultiplicadorApoioExcesso,
            interaction.DerivaCorrecaoExcesso, interaction.PenalidadeColapso,
        };

        foreach (var target in parameters)
        {
            foreach (var source in parameters)
                numbers.Matrix[(int)target * parameters.Length + (int)source] = InteractionMatrix.GetCoefficient(target, source);
        }

        var display = new SerializedObject(AssetDatabase.LoadAssetAtPath<ParameterDisplayData>(DisplayPath));
        var entries = display.FindProperty("_entries");
        for (var i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var parameter = entry.FindPropertyRelative("Parameter").intValue;
            numbers.RealMin[parameter] = entry.FindPropertyRelative("RealMin").doubleValue;
            numbers.RealMax[parameter] = entry.FindPropertyRelative("RealMax").doubleValue;
        }

        var tiers = (CardTier[])Enum.GetValues(typeof(CardTier));
        numbers.TierCounts = tiers.Select(tier => data.Cards.Count(card => card.Tier == tier.ToString())).ToArray();
        return numbers;
    }
}
