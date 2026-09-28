using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Parking.Data.Factories;
using Parking.Services.Contracts;
using Parking.Services.Implementations;
using Parking.ViewModels;
using Xunit;

namespace Parking.UnitTests.Services;

public class PrinterDiscoveryServiceTests
{
    [Fact]
    public void GetInstalledPrinters_ShouldNotThrowException_AndReturnNonNullCollection()
    {
        // Arrange
        var service = new PrinterDiscoveryService();

        // Act
        var printers = service.GetInstalledPrinters();

        // Assert
        printers.Should().NotBeNull();
    }

    [Fact]
    public void ResolveConnectedPrinter_ShouldHandleSystemPrintersGracefully()
    {
        // Arrange
        var service = new PrinterDiscoveryService();

        // Act
        var resolved = service.ResolveConnectedPrinter();

        // Assert
        // Debe retornar null o una cola válida, sin lanzar excepciones no controladas
        if (resolved != null)
        {
            resolved.Name.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void PrintVisualDirect_WhenVisualIsNull_ReturnsDefensiveFailure()
    {
        // Arrange
        var service = new PrinterDiscoveryService();

        // Act
        var (success, printerName, error) = service.PrintVisualDirect(null!, "Test Job");

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("no es válido");
    }

    [Fact]
    public void ReceiptPreviewViewModel_WhenPrinterDiscoveryInjected_RefreshesPrinterName()
    {
        // Arrange
        var printerMock = new Mock<IReceiptPrinterService>();
        var sessionMock = new Mock<ISessionService>();
        var connMock = new Mock<IDbConnectionManager>();
        var configMock = new Mock<IConfiguration>();
        var pricingMock = new Mock<IPricingCalculatorService>();
        var discoveryMock = new Mock<IPrinterDiscoveryService>();

        discoveryMock.Setup(d => d.ResolveConnectedPrinter())
            .Returns((System.Printing.PrintQueue?)null);

        // Act
        var vm = new ReceiptPreviewViewModel(
            printerMock.Object,
            sessionMock.Object,
            connMock.Object,
            configMock.Object,
            pricingMock.Object,
            discoveryMock.Object);

        // Assert
        vm.HasConnectedPrinter.Should().BeFalse();
        vm.DetectedPrinterName.Should().Be("Sin impresora conectada");
    }
}
