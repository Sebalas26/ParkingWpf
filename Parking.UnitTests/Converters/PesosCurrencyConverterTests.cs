using System.Globalization;
using FluentAssertions;
using Parking.Core.Converters;
using Xunit;

namespace Parking.UnitTests.Converters;

public class PesosCurrencyConverterTests
{
    private readonly PesosCurrencyConverter _converter = new();

    [Fact]
    public void Convert_DecimalAmount_FormatsAsColombianPesos()
    {
        var result = _converter.Convert(400000m, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("$ 400.000");
    }

    [Fact]
    public void Convert_ZeroAmount_FormatsAsZeroPesos()
    {
        var result = _converter.Convert(0m, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("$ 0");
    }

    [Fact]
    public void Convert_NullValue_FormatsAsZeroPesos()
    {
        var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("$ 0");
    }

    [Theory]
    [InlineData(1000, "$ 1.000")]
    [InlineData(50000, "$ 50.000")]
    [InlineData(1250000, "$ 1.250.000")]
    public void Convert_VariousIntAmounts_FormatsCorrectly(int amount, string expected)
    {
        var result = _converter.Convert(amount, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(50000.0, "$ 50.000")]
    [InlineData(1000000L, "$ 1.000.000")]
    public void Convert_DoubleAndLong_FormatsCorrectly(object value, string expected)
    {
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("$ 400.000", 400000)]
    [InlineData("400000", 400000)]
    [InlineData("$400.000", 400000)]
    [InlineData("  $ 400.000  ", 400000)]
    [InlineData("$ 10.500", 10500)]
    [InlineData("$ 0", 0)]
    public void ConvertBack_FormattedStrings_ExtractsDecimalCorrectly(string input, decimal expected)
    {
        var result = _converter.ConvertBack(input, typeof(decimal), null, CultureInfo.InvariantCulture);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("$")]
    public void ConvertBack_InvalidOrEmptyStrings_ReturnsZero(string input)
    {
        var result = _converter.ConvertBack(input, typeof(decimal), null, CultureInfo.InvariantCulture);

        result.Should().Be(0m);
    }

    [Fact]
    public void ConvertBack_Null_ReturnsZero()
    {
        var result = _converter.ConvertBack(null, typeof(decimal), null, CultureInfo.InvariantCulture);

        result.Should().Be(0m);
    }

    [Fact]
    public void ConvertBack_WithSpecificTargetTypes_ConvertsAppropriately()
    {
        var resultDouble = _converter.ConvertBack("$ 50.000", typeof(double), null, CultureInfo.InvariantCulture);
        resultDouble.Should().Be(50000.0);

        var resultInt = _converter.ConvertBack("$ 25.000", typeof(int), null, CultureInfo.InvariantCulture);
        resultInt.Should().Be(25000);
    }
}
