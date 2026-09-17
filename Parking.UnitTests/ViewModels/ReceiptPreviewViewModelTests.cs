using System;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.ViewModels;
using Xunit;

namespace Parking.UnitTests.ViewModels;

public class ReceiptPreviewViewModelTests
{
    private readonly Mock<IReceiptPrinterService> _mockPrinterService;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IDbConnectionManager> _mockConnectionManager;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IPricingCalculatorService> _mockPricingCalculator;

    public ReceiptPreviewViewModelTests()
    {
        _mockPrinterService = new Mock<IReceiptPrinterService>();
        _mockSessionService = new Mock<ISessionService>();
        _mockConnectionManager = new Mock<IDbConnectionManager>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockPricingCalculator = new Mock<IPricingCalculatorService>();
    }

    private ReceiptPreviewViewModel CreateViewModel()
    {
        return new ReceiptPreviewViewModel(
            _mockPrinterService.Object,
            _mockSessionService.Object,
            _mockConnectionManager.Object,
            _mockConfiguration.Object,
            _mockPricingCalculator.Object);
    }

    [Fact]
    public void LoadTicket_ResolvesNitFromCompanyConfiguredInPwa_NotHardcoded()
    {
        // Arrange
        var branch = new BranchModel
        {
            Id = 1,
            Name = "Sede Principal",
            CompanyNit = "901.555.444-1"
        };
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(branch);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-C1-20260916-001",
            PlateNumber = "OEJ05G",
            VehicleType = VehicleType.Motorcycle
        };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.BranchNit.Should().Be("NIT. 901.555.444-1");
        vm.BranchNit.Should().NotBe("NIT. 900900900-9");
    }

    [Fact]
    public void LoadTicket_ResolvesPhoneFromBranchConfiguredInPwa_NotHardcoded()
    {
        // Arrange
        var branch = new BranchModel
        {
            Id = 1,
            Name = "Sede Calle 136",
            Phone = "300 9998877"
        };
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(branch);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-C1-20260916-001",
            PlateNumber = "OEJ05G",
            VehicleType = VehicleType.Motorcycle
        };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.BranchPhone.Should().Be("Tel. 300 9998877");
        vm.BranchPhone.Should().NotBe("Tel. 318 181818 - 301 301301301");
    }

    [Fact]
    public void LoadTicket_ResolvesAttendedByFromOperatorOrLoggedUser_NotMerlin()
    {
        // Arrange
        var user = new UserSessionModel
        {
            FullName = "Carlos Sánchez",
            Username = "csanchez"
        };
        _mockSessionService.Setup(s => s.CurrentUser).Returns(user);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-C1-20260916-001",
            PlateNumber = "OEJ05G",
            OperatorName = "Carlos Sánchez",
            VehicleType = VehicleType.Motorcycle
        };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.AttendedBy.Should().Be("CARLOS SÁNCHEZ");
        vm.AttendedBy.Should().NotBe("MERLIN");
    }

    [Fact]
    public void LoadTicket_ResolvesConfiguredRatesForBranchFromPwa_NotZero()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Motorcycle, null))
            .Returns(new VehicleRate
            {
                VehicleType = VehicleType.Motorcycle,
                HourRate = 2000m,
                MinuteRate = 40m
            });

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-C1-20260916-001",
            PlateNumber = "OEJ05G",
            VehicleType = VehicleType.Motorcycle,
            HourlyRate = 0m // Caso donde el tiquete venía en 0
        };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.FormattedRateText.Should().Contain("$ 2.000 / HORA");
        vm.FormattedRateText.Should().Contain("$ 40 / MIN");
        vm.FormattedRateText.Should().NotBe("TARIFA: $ 0 / HORA");
    }
}
