using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using Parking.Core.Helpers;
using Xunit;

namespace Parking.UnitTests.Helpers;

public class AutoShrinkFontHelperTests
{
    private readonly FontFamily _testFontFamily = new FontFamily("Consolas");

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CalculateFittingFontSize_WhenTextIsNullOrEmpty_ReturnsMaxFontSize(string? text)
    {
        // Arrange
        double availableWidth = 400;
        double maxFontSize = 84.0;
        double minFontSize = 28.0;

        // Act
        double result = AutoShrinkFontHelper.CalculateFittingFontSize(
            text!,
            availableWidth,
            maxFontSize,
            minFontSize,
            _testFontFamily,
            FontWeights.Black,
            FontStyles.Normal,
            FontStretches.Normal,
            FlowDirection.LeftToRight);

        // Assert
        result.Should().Be(maxFontSize);
    }

    [Fact]
    public void CalculateFittingFontSize_WhenTextIsShortAndFits_ReturnsMaxFontSize()
    {
        // Arrange: Placa corta convencional de 6 caracteres con ancho holgado (600px)
        string shortPlate = "ABC123";
        double availableWidth = 600;
        double maxFontSize = 84.0;
        double minFontSize = 28.0;

        // Act
        double result = AutoShrinkFontHelper.CalculateFittingFontSize(
            shortPlate,
            availableWidth,
            maxFontSize,
            minFontSize,
            _testFontFamily,
            FontWeights.Black,
            FontStyles.Normal,
            FontStretches.Normal,
            FlowDirection.LeftToRight);

        // Assert
        result.Should().Be(maxFontSize);
    }

    [Fact]
    public void CalculateFittingFontSize_WhenTextIsLongAndExceedsWidth_ShrinksFontSize()
    {
        // Arrange: Placa larga como la captura de usuario "343423323" en ancho disponible limitado
        string longPlate = "343423323";
        double availableWidth = 300;
        double maxFontSize = 84.0;
        double minFontSize = 28.0;

        // Act
        double result = AutoShrinkFontHelper.CalculateFittingFontSize(
            longPlate,
            availableWidth,
            maxFontSize,
            minFontSize,
            _testFontFamily,
            FontWeights.Black,
            FontStyles.Normal,
            FontStretches.Normal,
            FlowDirection.LeftToRight);

        // Assert
        result.Should().BeLessThan(maxFontSize);
        result.Should().BeGreaterThanOrEqualTo(minFontSize);
    }

    [Fact]
    public void CalculateFittingFontSize_WhenTextIsExtremelyLong_DoesNotDropBelowMinFontSize()
    {
        // Arrange: Cadena extremadamente larga de 50 caracteres
        string hugePlate = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890EXTRAOVERFLOW";
        double availableWidth = 150;
        double maxFontSize = 84.0;
        double minFontSize = 28.0;

        // Act
        double result = AutoShrinkFontHelper.CalculateFittingFontSize(
            hugePlate,
            availableWidth,
            maxFontSize,
            minFontSize,
            _testFontFamily,
            FontWeights.Black,
            FontStyles.Normal,
            FontStretches.Normal,
            FlowDirection.LeftToRight);

        // Assert
        result.Should().Be(minFontSize);
    }

    [Fact]
    public void CalculateFittingFontSize_WhenAvailableWidthIsNarrower_CalculatesSmallerOrEqualFontSize()
    {
        // Arrange: Mismo texto largo en resolución normal (500px) vs resolución baja (250px)
        string text = "343423323ABC";
        double wideWidth = 500;
        double narrowWidth = 250;
        double maxFontSize = 84.0;
        double minFontSize = 28.0;

        // Act
        double sizeInWide = AutoShrinkFontHelper.CalculateFittingFontSize(
            text,
            wideWidth,
            maxFontSize,
            minFontSize,
            _testFontFamily,
            FontWeights.Black,
            FontStyles.Normal,
            FontStretches.Normal,
            FlowDirection.LeftToRight);

        double sizeInNarrow = AutoShrinkFontHelper.CalculateFittingFontSize(
            text,
            narrowWidth,
            maxFontSize,
            minFontSize,
            _testFontFamily,
            FontWeights.Black,
            FontStyles.Normal,
            FontStretches.Normal,
            FlowDirection.LeftToRight);

        // Assert
        sizeInNarrow.Should().BeLessThan(sizeInWide);
    }

    [Fact]
    public void CalculateFittingFontSize_WhenMinFontSizeGreaterThanMax_ClampsToMax()
    {
        // Arrange
        string text = "TEST";
        double availableWidth = 400;
        double maxFontSize = 50.0;
        double minFontSize = 70.0; // Incorrect min > max

        // Act
        double result = AutoShrinkFontHelper.CalculateFittingFontSize(
            text,
            availableWidth,
            maxFontSize,
            minFontSize,
            _testFontFamily,
            FontWeights.Black,
            FontStyles.Normal,
            FontStretches.Normal,
            FlowDirection.LeftToRight);

        // Assert
        result.Should().BeLessThanOrEqualTo(maxFontSize);
    }
}
