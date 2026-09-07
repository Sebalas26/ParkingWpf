using System;
using System.Globalization;
using FluentAssertions;
using Parking.Core.Converters;
using Parking.Core.Enums;
using Xunit;

namespace Parking.UnitTests.Converters;

public class ConvertersTests
{
    [Fact]
    public void CurrencyConverter_Convert_FormatsAmountCorrectly()
    {
        var converter = new CurrencyConverter();

        var resultDecimal = converter.Convert(15000.50m, typeof(string), null, CultureInfo.InvariantCulture);
        resultDecimal.Should().Be("$15,000.50");

        var resultZero = converter.Convert(0m, typeof(string), null, CultureInfo.InvariantCulture);
        resultZero.Should().Be("$0.00");

        var resultNull = converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
        resultNull.Should().Be("$0.00");
    }

    [Fact]
    public void CurrencyConverter_ConvertBack_ParsesFormattedString()
    {
        var converter = new CurrencyConverter();

        var parsed = converter.ConvertBack("$25,000.00", typeof(decimal), null, CultureInfo.InvariantCulture);
        parsed.Should().Be(25000.00m);
    }

    [Fact]
    public void DurationConverter_Convert_FormatsTimeSpanAppropriately()
    {
        var converter = new DurationConverter();

        // Menos de 1 hora
        var underHour = converter.Convert(TimeSpan.FromMinutes(25).Add(TimeSpan.FromSeconds(30)), typeof(string), null, CultureInfo.InvariantCulture);
        underHour.Should().Be("25min 30seg");

        // Más de 1 hora
        var multiHour = converter.Convert(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(15)).Add(TimeSpan.FromSeconds(10)), typeof(string), null, CultureInfo.InvariantCulture);
        multiHour.Should().Be("2h 15min 10seg");

        // Más de 1 día
        var multiDay = converter.Convert(TimeSpan.FromDays(2).Add(TimeSpan.FromHours(4)).Add(TimeSpan.FromMinutes(30)), typeof(string), null, CultureInfo.InvariantCulture);
        multiDay.Should().Be("2d 4h 30min");
    }

    [Fact]
    public void InverseBooleanConverter_InvertsBooleanValue()
    {
        var converter = new InverseBooleanConverter();

        converter.Convert(true, typeof(bool), null, CultureInfo.InvariantCulture).Should().Be(false);
        converter.Convert(false, typeof(bool), null, CultureInfo.InvariantCulture).Should().Be(true);
        converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture).Should().Be(true);
    }

    [Fact]
    public void TicketStatusToStringConverter_TranslatesToSpanish()
    {
        var converter = new TicketStatusToStringConverter();

        converter.Convert(TicketStatus.Active, typeof(string), null, CultureInfo.InvariantCulture).Should().Be("Activo");
        converter.Convert(TicketStatus.Completed, typeof(string), null, CultureInfo.InvariantCulture).Should().Be("Completado");
        converter.Convert(TicketStatus.Cancelled, typeof(string), null, CultureInfo.InvariantCulture).Should().Be("Cancelado");
    }
}
