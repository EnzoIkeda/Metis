namespace Metis.Core.Tests;

public class RealUnitConversionTests
{
    [TestCase(0f, 0d)]
    [TestCase(55f, 660_000_000d)]
    [TestCase(100f, 1_200_000_000d)]
    public void ToReal_Renda_MapsIndexLinearly(float index, double expected)
    {
        Assert.That(RealUnitConversion.ToReal(index, 0, 1_200_000_000), Is.EqualTo(expected).Within(0.5));
    }

    [Test]
    public void ToReal_EnergiaWithNegativeRange_MidpointIsZero()
    {
        Assert.That(RealUnitConversion.ToReal(50f, -2_000_000, 2_000_000), Is.EqualTo(0d).Within(0.001));
        Assert.That(RealUnitConversion.ToReal(10f, -2_000_000, 2_000_000), Is.EqualTo(-1_600_000d).Within(0.001));
    }

    [Test]
    public void ToRealDelta_IgnoresRangeOffset()
    {
        // Mobilidade: 8 a 35 km/h; 10 pontos de indice valem 2,7 km/h, sem somar o minimo.
        Assert.That(RealUnitConversion.ToRealDelta(10f, 8, 35), Is.EqualTo(2.7).Within(0.0001));
        Assert.That(RealUnitConversion.ToRealDelta(-5f, 0, 1_200_000_000), Is.EqualTo(-60_000_000d).Within(0.5));
    }

    [TestCase(660_000_000d, true, "660 mi")]
    [TestCase(660_000_000d, false, "660M")]
    [TestCase(1_200_000_000d, true, "1,2 bi")]
    [TestCase(5_000_000d, true, "5,0 mi")]
    [TestCase(270_000d, true, "270 mil")]
    [TestCase(270_000d, false, "270k")]
    [TestCase(-1_600_000d, true, "-1,6 mi")]
    public void FormatCompact_Scales(double value, bool portuguese, string expected)
    {
        Assert.That(RealUnitConversion.FormatCompact(value, 0, false, portuguese), Is.EqualTo(expected));
    }

    [Test]
    public void FormatCompact_Unscaled_UsesRequestedDecimals()
    {
        Assert.That(RealUnitConversion.FormatCompact(3.25, 1, false, true), Is.EqualTo("3,3"));
        Assert.That(RealUnitConversion.FormatCompact(3.25, 1, false, false), Is.EqualTo("3.3"));
        Assert.That(RealUnitConversion.FormatCompact(20.15, 0, false, true), Is.EqualTo("20"));
    }

    [Test]
    public void FormatCompact_Sign()
    {
        Assert.That(RealUnitConversion.FormatCompact(120_000, 0, true, true), Is.EqualTo("+120 mil"));
        Assert.That(RealUnitConversion.FormatCompact(-120_000, 0, true, true), Is.EqualTo("-120 mil"));
        Assert.That(RealUnitConversion.FormatCompact(0, 0, true, true), Is.EqualTo("0"));
        Assert.That(RealUnitConversion.FormatCompact(-0.2, 0, false, true), Is.EqualTo("0"));
    }

    [Test]
    public void FormatCompact_RoundingUpToThousand_MovesToNextScale()
    {
        Assert.That(RealUnitConversion.FormatCompact(999_960, 0, false, true), Is.EqualTo("1,0 mi"));
        Assert.That(RealUnitConversion.FormatCompact(999.6, 0, false, false), Is.EqualTo("1.0k"));
    }

    [TestCase(1350d, 0, true, "1.350")]
    [TestCase(1350d, 0, false, "1,350")]
    [TestCase(900d, 0, true, "900")]
    [TestCase(5.04, 1, true, "5,0")]
    [TestCase(12345.67, 1, true, "12.345,7")]
    public void FormatNumber_WithoutAbbreviation_GroupsThousands(double value, int decimals, bool portuguese, string expected)
    {
        Assert.That(RealUnitConversion.FormatNumber(value, decimals, false, portuguese, abbreviate: false), Is.EqualTo(expected));
    }

    [Test]
    public void FormatNumber_WithAbbreviation_MatchesCompact()
    {
        Assert.That(RealUnitConversion.FormatNumber(660_000_000, 0, false, true, abbreviate: true), Is.EqualTo(RealUnitConversion.FormatCompact(660_000_000, 0, false, true)));
    }
}
