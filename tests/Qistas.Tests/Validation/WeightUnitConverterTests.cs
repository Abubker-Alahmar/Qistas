using Qistas.Domain.Validation;
using Xunit;

namespace Qistas.Tests.Validation;

public class WeightUnitConverterTests
{
    [Fact]
    public void ToKg_UnitKg_ReturnsSameValueRoundedToTwoDecimals()
    {
        Assert.Equal(500.00m, WeightUnitConverter.ToKg(500m, "KG"));
    }

    [Theory]
    [InlineData("TONNE")]
    [InlineData("TON")]
    [InlineData("T")]
    [InlineData("MT")]
    [InlineData("tonne")]
    public void ToKg_TonneVariants_MultipliesBy1000(string unit)
    {
        Assert.Equal(2500.00m, WeightUnitConverter.ToKg(2.5m, unit));
    }

    [Theory]
    [InlineData("G")]
    [InlineData("GRAM")]
    [InlineData("GRAMS")]
    public void ToKg_GramVariants_DividesBy1000(string unit)
    {
        Assert.Equal(2.50m, WeightUnitConverter.ToKg(2500m, unit));
    }

    [Fact]
    public void ToKg_NullUnit_DefaultsToKg()
    {
        Assert.Equal(750.00m, WeightUnitConverter.ToKg(750m, null));
    }

    [Fact]
    public void ToKg_UnknownUnit_AssumedAlreadyKg()
    {
        Assert.Equal(300.00m, WeightUnitConverter.ToKg(300m, "LB_UNSUPPORTED"));
    }

    [Fact]
    public void ToKg_RoundsToTwoDecimalsAwayFromZero()
    {
        Assert.Equal(2.35m, WeightUnitConverter.ToKg(2.345m, "KG"));
    }

    [Fact]
    public void ToKg_TonneConversionRoundsAfterMultiplication()
    {
        Assert.Equal(1234.57m, WeightUnitConverter.ToKg(1.234567m, "TONNE"));
    }
}
