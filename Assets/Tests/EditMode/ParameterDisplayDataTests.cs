using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ParameterDisplayDataTests
{
    private ParameterDisplayData _data;
    private Language _previousLanguage;
    private bool _previousRealUnits;

    [SetUp]
    public void SetUp()
    {
        _previousLanguage = LocalizationManager.Current;
        _previousRealUnits = UnitDisplaySettings.ShowRealUnits;

        _data = ScriptableObject.CreateInstance<ParameterDisplayData>();
        var entries = new[]
        {
            new ParameterDisplayEntry { Parameter = CityParameterType.Renda, RealMin = 0, RealMax = 1_200_000_000, UnitPt = "R$ {0}", UnitEn = "R$ {0}" },
            new ParameterDisplayEntry { Parameter = CityParameterType.Saude, RealMin = 0.5, RealMax = 6, Decimals = 1, UnitPt = "{0} leitos/mil", UnitEn = "{0} beds/1k" },
            new ParameterDisplayEntry { Parameter = CityParameterType.BemEstar, ShowAsIndex = true },
        };
        typeof(ParameterDisplayData).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_data, entries);
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationManager.Current = _previousLanguage;
        UnitDisplaySettings.ShowRealUnits = _previousRealUnits;
        Object.DestroyImmediate(_data);
    }

    [Test]
    public void FormatValue_RealUnitsInPortuguese_UsesUnitTemplate()
    {
        LocalizationManager.Current = Language.Portuguese;
        UnitDisplaySettings.ShowRealUnits = true;

        Assert.That(_data.FormatValue(CityParameterType.Renda, 55f, smallUnit: false), Is.EqualTo("R$ 660 mi"));
        Assert.That(_data.FormatValue(CityParameterType.Saude, 50f, smallUnit: false), Is.EqualTo("3,3 leitos/mil"));
    }

    [Test]
    public void FormatValue_English_UsesEnglishTemplateAndSuffix()
    {
        LocalizationManager.Current = Language.English;
        UnitDisplaySettings.ShowRealUnits = true;

        Assert.That(_data.FormatValue(CityParameterType.Renda, 55f, smallUnit: false), Is.EqualTo("R$ 660M"));
        Assert.That(_data.FormatValue(CityParameterType.Saude, 50f, smallUnit: false), Is.EqualTo("3.3 beds/1k"));
    }

    [Test]
    public void FormatValue_SmallUnit_WrapsOnlyTheUnitText()
    {
        LocalizationManager.Current = Language.Portuguese;
        UnitDisplaySettings.ShowRealUnits = true;

        Assert.That(_data.FormatValue(CityParameterType.Saude, 50f, smallUnit: true), Is.EqualTo("3,3<size=60%> leitos/mil</size>"));
    }

    [Test]
    public void FormatValue_IndexPreferenceOrIndexOnlyParameter_ShowsIndex()
    {
        LocalizationManager.Current = Language.Portuguese;
        UnitDisplaySettings.ShowRealUnits = true;
        Assert.That(_data.FormatValue(CityParameterType.BemEstar, 48.6f, smallUnit: true), Is.EqualTo("49"));

        UnitDisplaySettings.ShowRealUnits = false;
        Assert.That(_data.FormatValue(CityParameterType.Renda, 55f, smallUnit: true), Is.EqualTo("55"));
    }

    [Test]
    public void FormatAmount_Cost_IgnoresRangeOffset()
    {
        LocalizationManager.Current = Language.Portuguese;
        UnitDisplaySettings.ShowRealUnits = true;

        Assert.That(_data.FormatAmount(CityParameterType.Renda, 5f, smallUnit: false), Is.EqualTo("R$ 60 mi"));
        // Saude comeca em 0,5: um efeito de 20 pontos vale 1,1 leito/mil, sem somar o minimo.
        Assert.That(_data.FormatAmount(CityParameterType.Saude, 20f, smallUnit: false), Is.EqualTo("1,1 leitos/mil"));
    }

    [Test]
    public void FormatValue_WithoutUnit_ReturnsOnlyTheNumber()
    {
        LocalizationManager.Current = Language.Portuguese;
        UnitDisplaySettings.ShowRealUnits = true;

        Assert.That(_data.FormatValue(CityParameterType.Saude, 50f, smallUnit: false, withUnit: false), Is.EqualTo("3,3"));
    }
}
