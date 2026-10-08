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

    [Fact]
    public void LoadTicket_ExitTicket_WithAgreement_ShowsAgreementNameAndDiscount()
    {
        // Arrange
        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-C1-20260924-010",
            PlateNumber = "ABC123",
            VehicleType = VehicleType.Car,
            HourlyRate = 3000m,
            GrossAmount = 10000m,
            DiscountAmount = 3000m,
            NetAmount = 7000m,
            AmountPaid = 7000m,
            ExitTimeUtc = DateTime.UtcNow,
            Status = TicketStatus.Completed
        };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.IsExitReceipt.Should().BeTrue();
        vm.HasAgreement.Should().BeTrue();
        vm.HasDiscount.Should().BeTrue();
        vm.DiscountAmountStr.Should().Contain("3.000");
        vm.AgreementDisplayName.Should().NotBe("NO APLICA");
        vm.SubtotalStr.Should().Contain("10.000");
    }

    [Fact]
    public void LoadTicket_ExitTicket_WithoutAgreement_ShowsNoAplicaAndZeroDiscount()
    {
        // Arrange
        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-C1-20260924-011",
            PlateNumber = "XYZ789",
            VehicleType = VehicleType.Car,
            HourlyRate = 3000m,
            GrossAmount = 6000m,
            DiscountAmount = 0m,
            NetAmount = 6000m,
            AmountPaid = 6000m,
            ExitTimeUtc = DateTime.UtcNow,
            Status = TicketStatus.Completed
        };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.IsExitReceipt.Should().BeTrue();
        vm.HasAgreement.Should().BeFalse();
        vm.HasDiscount.Should().BeFalse();
        vm.AgreementDisplayName.Should().Be("NO APLICA");
        vm.DiscountAmountStr.Should().Be("$ 0");
    }

    [Fact]
    public void LoadTicket_FormatsTimesIn12HourFormat_WithAmPmAndNoSeconds()
    {
        // Arrange
        var vm = CreateViewModel();
        var entryTime = new DateTime(2026, 9, 25, 0, 21, 39); // 12:21 AM
        var exitTime = new DateTime(2026, 9, 25, 11, 13, 44); // 11:13 AM
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-TEST-001",
            PlateNumber = "PPD33",
            VehicleType = VehicleType.Car,
            HourlyRate = 3000m,
            GrossAmount = 162500m,
            NetAmount = 162500m,
            AmountPaid = 162500m,
            EntryTimeUtc = entryTime.ToUniversalTime(),
            ExitTimeUtc = exitTime.ToUniversalTime(),
            CreatedAtUtc = entryTime.ToUniversalTime(),
            Status = TicketStatus.Completed
        };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.EntryTimeStr.Should().Be("12:21 AM");
        vm.ExitTimeStr.Should().Be("11:13 AM");
        vm.InvoiceTimeStr.Should().Be("11:13 AM");
    }

    [Theory]
    [InlineData(13, null, "CC:")]
    [InlineData(1, null, "CC:")]
    [InlineData(31, null, "NIT:")]
    [InlineData(3, null, "NIT:")]
    [InlineData(22, null, "CE:")]
    [InlineData(2, null, "CE:")]
    [InlineData(12, null, "TI:")]
    [InlineData(41, null, "PAS:")]
    [InlineData(4, null, "PAS:")]
    [InlineData(42, null, "DIE:")]
    [InlineData(5, null, "DIE:")]
    [InlineData(99, "Company", "NIT:")]
    [InlineData(99, "Person", "CC:")]
    public void ResolveIdTypeLabel_ReturnsExpectedPrefix(int idType, string? personType, string expectedPrefix)
    {
        // Act
        var result = ReceiptPreviewViewModel.ResolveIdTypeLabel(idType, personType);

        // Assert
        result.Should().Be(expectedPrefix);
    }

    [Fact]
    public void LoadTicket_EntryTicket_SetsInvoiceNumberText_WithHashPrefix()
    {
        // Arrange
        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-C1-20260924-011",
            PlateNumber = "XYZ789",
            VehicleType = VehicleType.Car,
            Status = TicketStatus.Active
        };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.InvoiceNumberText.Should().Be("#PKF-C1-20260924-011");
    }

    [Theory]
    [InlineData(58, 7.5, 8.0, 168)]
    [InlineData(80, 10.0, 12.0, 265)]
    public void LoadTicket_SetsTicketFontSizes_BasedOnPaperWidth(int paperWidth, double expectedHeaderSize, double expectedNumberSize, double expectedPrintableWidth)
    {
        // Arrange
        var branch = new BranchModel { Id = 1, Name = "Sede Test", PaperWidth = paperWidth };
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(branch);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket { TicketNumber = "PKF-001", PlateNumber = "XYZ789", VehicleType = VehicleType.Car };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.TicketHeaderFontSize.Should().Be(expectedHeaderSize);
        vm.TicketNumberFontSize.Should().Be(expectedNumberSize);
        vm.PrintableContentWidth.Should().Be(expectedPrintableWidth);
    }

    [Fact]
    public void TogglePaperWidthCommand_SwitchesBetween58And80()
    {
        // Arrange
        var branch = new BranchModel { Id = 1, Name = "Sede Test", PaperWidth = 58 };
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(branch);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket { TicketNumber = "PKF-001", PlateNumber = "XYZ789", VehicleType = VehicleType.Car };
        vm.LoadTicket(ticket);

        vm.PaperWidth.Should().Be(58);
        vm.Is58Mm.Should().BeTrue();

        // Act 1: Toggle to 80
        vm.TogglePaperWidthCommand.Execute(null);

        // Assert 1
        vm.PaperWidth.Should().Be(80);
        vm.Is58Mm.Should().BeFalse();
        vm.PrintableContentWidth.Should().Be(265);

        // Act 2: Toggle back to 58
        vm.TogglePaperWidthCommand.Execute(null);

        // Assert 2
        vm.PaperWidth.Should().Be(58);
        vm.Is58Mm.Should().BeTrue();
        vm.PrintableContentWidth.Should().Be(168);
    }

    [Fact]
    public void LoadTicket_WhenPrinterNameContains58_Forces58MmEvenIfBranchConfiguredAs80()
    {
        // Arrange
        var branch = new BranchModel { Id = 1, Name = "Sede Test", PaperWidth = 80 };
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(branch);

        var vm = CreateViewModel();
        vm.DetectedPrinterName = "POS-58 Series";
        var ticket = new ParkingTicket { TicketNumber = "PKF-001", PlateNumber = "XYZ789", VehicleType = VehicleType.Car };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.PaperWidth.Should().Be(58);
        vm.Is58Mm.Should().BeTrue();
        vm.PrintableContentWidth.Should().Be(168);
    }

    [Fact]
    public void LoadTicket_EntryTicket_DoesNotShowPolicy_WhenPrintPolicyOnEntryIsFalse()
    {
        // Arrange
        var branch = new BranchModel
        {
            Id = 1,
            Name = "Sede Test",
            TicketPolicy = "POLIZA DE SEGURIDAD 123",
            PrintPolicyOnEntry = false,
            TicketAdditionalInfo = "INFO ADICIONAL 456",
            PrintAdditionalInfoOnEntry = false
        };
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(branch);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-001",
            PlateNumber = "ABC123",
            VehicleType = VehicleType.Car,
            Status = TicketStatus.Active
        };

        // Act
        vm.LoadTicket(ticket);

        // Assert
        vm.TicketPolicy.Should().BeNull();
        vm.TicketAdditionalInfo.Should().BeNull();
        vm.HasTicketPolicy.Should().BeFalse();
        vm.HasTicketAdditionalInfo.Should().BeFalse();
    }

    [Fact]
    public void LoadTicket_ExitTicket_DoesNotShowPolicyOrInfo_WhenResolutionPrintFlagsAreFalse()
    {
        // Arrange
        var branch = new BranchModel
        {
            Id = 1,
            Name = "Sede Test",
            TicketPolicy = "POLIZA DE LA SEDE",
            TicketAdditionalInfo = "INFO ADICIONAL SEDE",
            TicketSchedule = "L-V 8-6"
        };
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(branch);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-001",
            PlateNumber = "ABC123",
            VehicleType = VehicleType.Car,
            Status = TicketStatus.Completed,
            ExitTimeUtc = DateTime.UtcNow
        };

        var resolution = new BillingResolution
        {
            Name = "Factura Venta",
            PrintPolicyOnExit = false,
            PrintAdditionalInfoOnExit = false,
            PrintScheduleOnExit = false,
            TicketPolicy = "POLIZA RESOLUCION",
            TicketAdditionalInfo = "INFO RESOLUCION"
        };

        // Act
        vm.LoadTicket(ticket, resolution);

        // Assert
        vm.TicketPolicy.Should().BeNull();
        vm.TicketAdditionalInfo.Should().BeNull();
        vm.BranchSchedule.Should().BeNull();
        vm.HasTicketPolicy.Should().BeFalse();
        vm.HasTicketAdditionalInfo.Should().BeFalse();
        vm.HasBranchSchedule.Should().BeFalse();
    }

    [Fact]
    public void LoadTicket_ExitTicket_ShowsPolicyAndInfo_WhenResolutionPrintFlagsAreTrue()
    {
        // Arrange
        var branch = new BranchModel
        {
            Id = 1,
            Name = "Sede Test",
            TicketPolicy = "POLIZA DE LA SEDE",
            TicketAdditionalInfo = "INFO ADICIONAL SEDE"
        };
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(branch);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketNumber = "PKF-001",
            PlateNumber = "ABC123",
            VehicleType = VehicleType.Car,
            Status = TicketStatus.Completed,
            ExitTimeUtc = DateTime.UtcNow
        };

        var resolution = new BillingResolution
        {
            Name = "Factura Venta",
            PrintPolicyOnExit = true,
            PrintAdditionalInfoOnExit = true,
            TicketPolicy = "POLIZA RESOLUCION",
            TicketAdditionalInfo = null // Fallback a la sede
        };

        // Act
        vm.LoadTicket(ticket, resolution);

        // Assert
        vm.TicketPolicy.Should().Be("POLIZA RESOLUCION");
        vm.TicketAdditionalInfo.Should().Be("INFO ADICIONAL SEDE");
        vm.HasTicketPolicy.Should().BeTrue();
        vm.HasTicketAdditionalInfo.Should().BeTrue();
    }

    [Fact]
    public void AppVersionDisplay_ReturnsCleanAssemblyVersion_WithoutLeadingV()
    {
        // Arrange & Act
        var vm = new LoginViewModel(
            new Mock<IAuthService>().Object,
            new Mock<ISessionService>().Object,
            new Mock<IApiClientService>().Object,
            new Mock<ISyncEngineService>().Object,
            new Mock<IPermissionService>().Object);

        // Assert: la versión debe ser numérica limpia (ej: "1.0.3"), sin prefijo "v"
        vm.AppVersionDisplay.Should().NotStartWith("v");
        vm.AppVersionDisplay.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }
}

